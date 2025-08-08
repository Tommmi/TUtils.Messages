<#  
.SYNOPSIS
  Lokales Build & Pack (SourceLink bleibt), optional Push von .nupkg und .snupkg,
  plus Warten auf Verfügbarkeit beider Artefakte (NuGet V3 / symbols.nuget.org).

.PARAMETER CsprojPath
  Pfad zur .csproj.

.PARAMETER Configuration
  Build-Konfiguration (Default: Release).

.PARAMETER ForceLocalBuild
  Baut lokal neu, aber ohne CI-Flag (ContinuousIntegrationBuild=false).

.PARAMETER IncludeSymbols
  Erzeuge Symbolpaket (.snupkg). Standard: AUS; für VS-Quellnavigation AN machen.

.PARAMETER Push
  Führt Upload der .nupkg (und .snupkg, falls -IncludeSymbols) aus.

.PARAMETER ApiKey
  API-Key für -Push.

.PARAMETER PackageSource
  V3 Service Index URL (z. B. https://api.nuget.org/v3/index.json). Pflicht bei -Push/-WaitForAvailability.

.PARAMETER VersionSuffix
  Optionaler Versionssuffix (z. B. beta1).

.PARAMETER WaitForAvailability
  Warte nach Push auf Verfügbarkeit: .nupkg (Flat-Container) und .snupkg (symbols.nuget.org).

.PARAMETER AvailabilityTimeoutSeconds
  Max. Wartezeit (Default: 600).

.PARAMETER AvailabilityPollSeconds
  Poll-Intervall (Default: 10).
#>

[CmdletBinding(PositionalBinding = $false)]
param(
  [Parameter(Mandatory = $true)]
  [string]$CsprojPath,

  [string]$Configuration = "Release",

  [switch]$ForceLocalBuild,

  [switch]$IncludeSymbols,   # <— neu: .snupkg erzeugen
  [switch]$Push,
  [string]$ApiKey,
  [string]$PackageSource,

  [switch]$AutoVersion,
  [int]$Major = 0,
  [string]$PackageId,

  [string]$VersionSuffix,

  [switch]$WaitForAvailability,
  [int]$AvailabilityTimeoutSeconds = 600,
  [int]$AvailabilityPollSeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'


function Resolve-Csproj([string]$InputPath) {
  if ([string]::IsNullOrWhiteSpace($InputPath)) { throw "CsprojPath ist leer." }

  $resolved = Resolve-Path -LiteralPath $InputPath -ErrorAction SilentlyContinue
  if (-not $resolved) { throw "Pfad nicht gefunden: $InputPath" }

  $item = Get-Item -LiteralPath $resolved.Path
  if ($item.PSIsContainer) {
    $candidates = Get-ChildItem -LiteralPath $item.FullName -Filter *.csproj -File
    if ($candidates.Count -eq 1) { return $candidates[0].FullName }
    if ($candidates.Count -gt 1) {
      throw "Mehrere .csproj in '$($item.FullName)' gefunden. Bitte Pfad zur gewünschten .csproj angeben."
    }
    throw "Keine .csproj in '$($item.FullName)' gefunden."
  } else {
    if ($item.Name -notmatch '\.csproj$') { throw "Datei ist keine .csproj: $($item.FullName)" }
    return $item.FullName
  }
}



# Normiere CsprojPath (Ordner erlaubt)
$CsprojPath = Resolve-Csproj -InputPath $CsprojPath


function Write-Header([string]$text) {
  Write-Host ""
  Write-Host "=== $text ===" -ForegroundColor Cyan
}

# Nuget-Id (just the name)
function Read-PackageIdFromCsproj {
  param([string]$CsprojPath)

  if ([string]::IsNullOrWhiteSpace($CsprojPath)) { throw "CsprojPath ist leer." }
  [xml]$xml = Get-Content -LiteralPath $CsprojPath -Raw

  # Helper: sichere Property-Lesung unter StrictMode
  function Get-Prop([object]$obj, [string]$name) {
    if (-not $obj) { return $null }
    $p = $obj.PSObject.Properties[$name]
    if ($p) { return $p.Value }
    return $null
  }

  $pkgId = $null

  # 1) Direkter, StrictMode-sicherer Zugriff über alle PropertyGroup-Knoten
  $propGroups = @()
  $pg = Get-Prop $xml.Project 'PropertyGroup'
  if ($pg) {
    if ($pg -is [System.Array]) { $propGroups = $pg } else { $propGroups = @($pg) }
  }

  foreach ($grp in $propGroups) {
    $val = Get-Prop $grp 'PackageId'
    if ($val) { $pkgId = [string]$val; break }
  }

  # 2) XPath ohne Namespace
  if (-not $pkgId) {
    try {
      $nodes = $xml.SelectNodes('//Project/PropertyGroup/PackageId')
      if ($nodes -and $nodes.Count -gt 0) { $pkgId = [string]$nodes[0].InnerText }
    } catch {}
  }

  # 3) XPath mit MSBuild-Namespace
  if (-not $pkgId) {
    try {
      $nsm = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
      $nsm.AddNamespace('msb','http://schemas.microsoft.com/developer/msbuild/2003')
      $nodes = $xml.SelectNodes('//msb:Project/msb:PropertyGroup/msb:PackageId', $nsm)
      if ($nodes -and $nodes.Count -gt 0) { $pkgId = [string]$nodes[0].InnerText }
    } catch {}
  }

  # 4) Fallbacks: AssemblyName → Dateiname
  if (-not $pkgId) {
    $assemblyName = $null
    foreach ($grp in $propGroups) {
      $val = Get-Prop $grp 'AssemblyName'
      if ($val) { $assemblyName = [string]$val; break }
    }
    if ($assemblyName) { return $assemblyName }

    return [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath)
  }

  return $pkgId
}

# Erzwinge TLS 1.2/1.3, sonst liefern manche Endpunkte HTML-Fehlerseiten
try {
  [Net.ServicePointManager]::SecurityProtocol = `
    [Net.SecurityProtocolType]::Tls12 `
    -bor 3072   # Tls13, falls vorhanden
} catch {}

# current highest version of Nuget package in nuget feed
# Returns type [version]
function Get-HighestNuGetVersion {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)][string]$Id,
    [string]$Source  # z.B. https://api.nuget.org/v3/index.json oder dein V3-Feed
  )

  function Get-Json([string]$url) {
    try {
      $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
      $txt = $r.Content
      try {
        $obj = $txt | ConvertFrom-Json -ErrorAction Stop
        return @{ ok = $true; json = $obj; status = $r.StatusCode; url = $url }
      } catch {
        return @{ ok = $false; status = $r.StatusCode; url = $url; text = $txt; err = $_.Exception.Message }
      }
    } catch {
      return @{ ok = $false; url = $url; err = $_.Exception.Message }
    }
  }

  function Get-PackageBaseAddress([string]$serviceIndexUrl) {
    $res = Get-Json $serviceIndexUrl
    if (-not $res.ok) {
      Write-Warning "Konnte Service-Index '$($res.url)' nicht lesen: $($res.err)"
      return $null
    }
    $idx = $res.json
    if (-not $idx.resources) { return $null }
    $pba = $idx.resources | Where-Object { $_.'@type' -like 'PackageBaseAddress*' } | Select-Object -First 1
    if ($pba -and $pba.'@id') {
      $base = [string]$pba.'@id'
      if ($base -notmatch '/$') { $base += '/' }
      return $base
    }
    return $null
  }

  # 1) Service-Index bestimmen
  $serviceIndex =
    if ([string]::IsNullOrWhiteSpace($Source)) {
      'https://api.nuget.org/v3/index.json'
    } elseif ($Source -match '\.json($|\?)') {
      $Source
    } elseif ($Source -match '/v3/?$') {
      ($Source.TrimEnd('/')) + '/index.json'
    } elseif ($Source -match 'nuget\.org($|/)') {
      'https://api.nuget.org/v3/index.json'
    } else {
      ($Source.TrimEnd('/')) + '/v3/index.json'
    }

  # 2) PackageBaseAddress ermitteln (mit Fallback auf nuget.org)
  $pba = Get-PackageBaseAddress $serviceIndex
  if (-not $pba -and $serviceIndex -ne 'https://api.nuget.org/v3/index.json') {
    Write-Warning "Kein PackageBaseAddress in '$serviceIndex' – Fallback auf nuget.org."
    $pba = Get-PackageBaseAddress 'https://api.nuget.org/v3/index.json'
  }
  if (-not $pba) { return $null }

  # 3) Versionsindex abrufen
  $lcId = $Id.ToLowerInvariant()
  $indexUrl = "$pba$lcId/index.json"
  $vres = Get-Json $indexUrl
  if (-not $vres.ok) {
    Write-Warning "Versionsindex '$($vres.url)' nicht verfügbar: $($vres.err)"
    return $null
  }

  $versions = $vres.json.versions
  if ($null -eq $versions) {
    Write-Warning "Versionsindex liefert kein 'versions'-Feld für '$lcId'."
    return $null
  }

  # Sicherstellen, dass es ein Array ist
  if ($versions -isnot [System.Array]) { $versions = @($versions) }

  # In [version] konvertieren (ignoriere fehlerhafte Einträge)
  $verObjs = @()
  foreach ($v in $versions) {
    try { $verObjs += [version]([string]$v) } catch {}
  }

  if ($verObjs.Count -gt 0) {
    return ($verObjs | Sort-Object)[-1]
  }

  return $null
}

# returns new calculated [version]
function Compose-NextVersion {
  param([version]$CurrentHighest, [int]$Major)
  if ($null -eq $CurrentHighest) {
    $maj = if ($PSBoundParameters.ContainsKey('Major')) { [int]$Major } else { 0 }
    if ($maj -lt 0) { $maj = 0 }
    return [version]"$maj.0.1"
  }
  $effectiveMajor = $CurrentHighest.Major
  if ($PSBoundParameters.ContainsKey('Major') -and [int]$Major -gt [int]$CurrentHighest.Major) {
    return [version]"$Major.0.1"
  }
  $parts = $CurrentHighest.ToString().Split('.')
  if ($parts.Length -lt 3) { $parts = @($effectiveMajor.ToString(), "0", "1") }
  $parts[0] = [string]$effectiveMajor
  $parts[-1] = ([int]$parts[-1] + 1).ToString()
  return [version]([string]::Join('.', $parts))
}

# opens zip file $ZipPath and returns content of a containing file named $EntryName
function Get-ZipEntryContent {
  param([string]$ZipPath, [string]$EntryName)
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $fs = [System.IO.File]::OpenRead($ZipPath)
  try {
    $za = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Read, $false)
    $entry = $za.Entries | Where-Object { $_.FullName -ieq $EntryName }
    if (-not $entry) { return $null }
    $sr = New-Object System.IO.StreamReader($entry.Open())
    try { return $sr.ReadToEnd() } finally { $sr.Dispose() }
  } finally { $fs.Dispose() }
}

# reads package version from *.nuspec inside package $NupkgPath
# returns @{ Id = $id; Version = $ver } 
# Id: string
# Version: string
function Get-NupkgMetadata {
  param([string]$NupkgPath)
  $nuspecName = [System.IO.Path]::GetFileNameWithoutExtension($NupkgPath) + ".nuspec"
  $content = Get-ZipEntryContent -ZipPath $NupkgPath -EntryName $nuspecName
  if (-not $content) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $fs = [System.IO.File]::OpenRead($NupkgPath)
    try {
      $za = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Read, $false)
      $entry = $za.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
      if ($entry) {
        $sr = New-Object System.IO.StreamReader($entry.Open())
        try { $content = $sr.ReadToEnd() } finally { $sr.Dispose() }
      }
    } finally { $fs.Dispose() }
  }
  if (-not $content) { throw "Konnte .nuspec in $NupkgPath nicht finden." }
  [xml]$xml = $content
  $id = $null
  if ($xml.package -and $xml.package.metadata -and $xml.package.metadata.id) { $id = $xml.package.metadata.id }
  if (-not $id -and $xml.Project -and $xml.Project.Metadata -and $xml.Project.Metadata.Id) { $id = $xml.Project.Metadata.Id }
  $ver = $null
  if ($xml.package -and $xml.package.metadata -and $xml.package.metadata.version) { $ver = $xml.package.metadata.version }
  if (-not $ver -and $xml.Project -and $xml.Project.Metadata -and $xml.Project.Metadata.Version) { $ver = $xml.Project.Metadata.Version }
  if (-not $id -or -not $ver) { throw "Id/Version aus .nuspec nicht lesbar." }
  [pscustomobject]@{ Id = $id; Version = $ver }
}

# calls the service description of the NuGet feed.
# Tries to read the information "PackageBaseAddress/3.0.0"
function Get-V3FlatContainerBase {
  param([Parameter(Mandatory)][string]$ServiceIndexUrl)
  $idx = Invoke-RestMethod -Method Get -Uri $ServiceIndexUrl -TimeoutSec 30
  $flat = $idx.resources | Where-Object { $_.'@type' -like "PackageBaseAddress/3.0.0*" } | Select-Object -First 1
  if (-not $flat) { throw "Kein 'PackageBaseAddress/3.0.0' im Service-Index gefunden." }
  $flat.'@id'.TrimEnd('/')
}

# calls over Web : "$FlatBase/$idLower/$verLower/$idLower.$verLower.nupkg"
# true if succeeded
function Test-PackageAvailableFlat {
  param(
    [Parameter(Mandatory)][string]$FlatBase,
    [Parameter(Mandatory)][string]$PackageId,
    [Parameter(Mandatory)][string]$PackageVersion
  )
  $idLower = $PackageId.ToLowerInvariant()
  $verLower = $PackageVersion.ToLowerInvariant()
  $url = "$FlatBase/$idLower/$verLower/$idLower.$verLower.nupkg"
  try {
    $resp = Invoke-WebRequest -Method Head -Uri $url -TimeoutSec 15 -ErrorAction Stop
    return ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300)
  } catch { return $false }
}

function Wait-ForNuGetPackage {
  param(
    [Parameter(Mandatory)][string]$ServiceIndexUrl,
    [Parameter(Mandatory)][string]$PackageId,
    [Parameter(Mandatory)][string]$PackageVersion,
    [int]$TimeoutSec = 600,
    [int]$PollSec = 10
  )
  $flatBase = Get-V3FlatContainerBase -ServiceIndexUrl $ServiceIndexUrl
  Write-Host "flatBase=$flatBase"
  $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSec)
  Write-Host "Warte auf .nupkg-Verfügbarkeit ($PackageId $PackageVersion) …"
  while ([DateTimeOffset]::UtcNow -lt $deadline) {
    if (Test-PackageAvailableFlat -FlatBase $flatBase -PackageId $PackageId -PackageVersion $PackageVersion) {
      Write-Host ".nupkg ist verfügbar." -ForegroundColor Green
      return $true
    }
    Start-Sleep -Seconds $PollSec
    Write-Host "." -NoNewLine
  }
  Write-Warning ".nupkg nach $TimeoutSec s noch nicht sichtbar."
  $false
}


# --- Hauptlogik ---------------------------------------------------------------
$CsprojPath = (Resolve-Path $CsprojPath).Path
$ProjectDir = Split-Path -Parent $CsprojPath
Push-Location $ProjectDir

try {
  Write-Header "Kontext"
  Write-Host "Projekt:        $CsprojPath"
  Write-Host "Konfiguration:  $Configuration"
  Write-Host "Arbeitsverz.:   $(Get-Location)"

  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI wurde nicht gefunden. Bitte .NET SDK installieren und PATH prüfen."
  }


  $commonProps = @()
  # AutoVersion: Paket-ID ermitteln (falls nicht gesetzt), höchste Version abfragen und neue Version setzen
  if ($AutoVersion) {
    if (-not $PackageId -or [string]::IsNullOrWhiteSpace($PackageId)) {
      $PackageId = Read-PackageIdFromCsproj -CsprojPath $CsprojPath
    }
    Write-Host "=== AutoVersion aktiv ===" -ForegroundColor Yellow
    Write-Host "Resolved PackageId:       $PackageId"

    $highest = $null
    if ($PackageId -and -not [string]::IsNullOrWhiteSpace($PackageId)) {
      $highest = Get-HighestNuGetVersion -Id $PackageId -Source $PackageSource
    } else {
      Write-Warning "Keine PackageId aus Parametern/Projekt ableitbar – verwende Standard-Startversion."
    }

    if ($highest) { Write-Host "Remote Highest:           $($highest.ToString())" } else { Write-Host "Remote Highest:           <none>" }
    $newVersion = Compose-NextVersion -CurrentHighest $highest -Major $Major

    Write-Host "Neue Version:             $($newVersion.ToString())"
    $commonProps += "-p:Version=$($newVersion.ToString())"
  }

  Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
  [void][System.Console]::ReadKey($true)


  if ($VersionSuffix) { $commonProps += "-p:VersionSuffix=$VersionSuffix" }

  # Symbole je nach Schalter
  if ($IncludeSymbols) {
    $commonProps += "-p:IncludeSymbols=true"
    $commonProps += "-p:SymbolPackageFormat=snupkg"
  } else {
    $commonProps += "-p:IncludeSymbols=false"
  }

  if ($ForceLocalBuild) {
    Write-Header "Modus: Lokaler Neubuild (ohne CI-Flag)"
    dotnet restore $CsprojPath | Out-Null
    $buildProps = @(
      "-p:ContinuousIntegrationBuild=false",
      "-p:EmbedUntrackedSources=true"
    ) + $commonProps
    Write-Host "dotnet build $CsprojPath -c $Configuration $($buildProps -join ' ')"
    dotnet build $CsprojPath -c $Configuration @buildProps
    $packArgs = @(
      $CsprojPath, "-c", $Configuration, "-o", "out", "--no-build"
    ) + $buildProps
    Write-Host "dotnet pack $($packArgs -join ' ')"
    dotnet pack @packArgs
  }
  else {
    Write-Header "Modus: Standard (lokal) – Neubuild ohne CI-Flag"
    dotnet restore $CsprojPath | Out-Null
    $buildProps = @(
      "-p:ContinuousIntegrationBuild=false",
      "-p:EmbedUntrackedSources=true"
    ) + $commonProps
    Write-Host "dotnet build $CsprojPath -c $Configuration $($buildProps -join ' ')"
    dotnet build $CsprojPath -c $Configuration @buildProps
    $packArgs = @(
      $CsprojPath, "-c", $Configuration, "-o", "out", "--no-build"
    ) + $buildProps
    Write-Host "dotnet pack $($packArgs -join ' ')"
    dotnet pack @packArgs
  }

  Write-Header "Paket-Ermittlung"
  $pkg = Get-ChildItem -Path "$ProjectDir\out" -Filter "*.nupkg" -File |
         Where-Object { $_.Name -notmatch "\.symbols\.nupkg$" } |
         Sort-Object LastWriteTime -Descending |
         Select-Object -First 1
  if (-not $pkg) { throw "Kein .nupkg in 'out' gefunden." }
  Write-Host "Gefundenes .nupkg: $($pkg.FullName)"
  $meta = Get-NupkgMetadata -NupkgPath $pkg.FullName
  $chosenVersion = $meta.Version
  if ($AutoVersion -and $newVersion) {
    if ($meta.Version -ne $newVersion.ToString()) { 
        Write-Warning "Gepackte Version ($($meta.Version)) weicht von AutoVersion ($($newVersion)) ab." 
        return
    }
    $chosenVersion = $newVersion.ToString()
  }
  Write-Host ("Metadaten → Id={0}, Version={1}" -f $meta.Id, $meta.Version)

  # evtl. vorhandenes .snupkg ermitteln
  $snupkg = $null
  if ($IncludeSymbols) {
    $snupkg = Get-ChildItem -Path "$ProjectDir\out" -Filter "*.snupkg" -File |
              Sort-Object LastWriteTime -Descending |
              Select-Object -First 1
    if (-not $snupkg) {
      Write-Warning "IncludeSymbols war aktiv, aber kein .snupkg gefunden."
    } else {
      Write-Host "Gefundenes .snupkg: $($snupkg.FullName)"
    }
  }

  if ($Push) {
    if (-not $ApiKey) {
        # Interaktiv abfragen, falls nicht angegeben
        $ApiKey = Read-Host -Prompt "Bitte gib den NuGet API Key ein"
    }
    if (-not $PackageSource) { throw "Für -Push ist -PackageSource erforderlich (z. B. https://api.nuget.org/v3/index.json)." }

    Write-Header "NuGet Push (.nupkg)"
    $pushArgs = @($pkg.FullName, "--api-key", $ApiKey, "--source", $PackageSource, "--skip-duplicate")
    Write-Host "dotnet nuget push $($pushArgs -join ' ')"
    dotnet nuget push @pushArgs
    Write-Host ".nupkg Upload abgeschlossen." -ForegroundColor Green

    if ($snupkg) {
      Write-Header "NuGet Push (.snupkg → Symbols)"
      $pushSymArgs = @($snupkg.FullName, "--api-key", $ApiKey, "--source", $PackageSource, "--skip-duplicate")
      Write-Host "dotnet nuget push $($pushSymArgs -join ' ')"
      dotnet nuget push @pushSymArgs
      Write-Host ".snupkg Upload abgeschlossen." -ForegroundColor Green
    }

    if ($WaitForAvailability) {
      Write-Header "Warte auf Verfügbarkeit (.nupkg)"
      $ok1 = Wait-ForNuGetPackage -ServiceIndexUrl $PackageSource `
                                  -PackageId $meta.Id `
                                  -PackageVersion $meta.Version `
                                  -TimeoutSec $AvailabilityTimeoutSeconds `
                                  -PollSec $AvailabilityPollSeconds
    }
  }
  else {
    Write-Host "Push übersprungen. Pakete liegen unter: $ProjectDir\out" -ForegroundColor Yellow
  }

  Write-Header "Hinweise"
  Write-Host "- Für VS-Quellnavigation: In der .csproj sollten stehen: "
  Write-Host "    <PublishRepositoryUrl>true</PublishRepositoryUrl>"
  Write-Host "    <RepositoryUrl>https://dein.git.repo/url</RepositoryUrl>"
  Write-Host "  + SourceLink-Paket (z. B. Microsoft.SourceLink.GitHub) als PackageReference."
  Write-Host "- In Visual Studio: Tools → Options → Debugging:"
  Write-Host "    * Enable Source Link support: ON"
  Write-Host "    * Symbol servers: Microsoft Symbol Servers: ON (oder nuget.org), Cache geleert"
  Write-Host "- Für „F12“ in Paketcode ohne Debugger: „Enable navigation to decompiled sources“ an;"
  Write-Host "  echte Quellansicht klappt v. a. im Debugger mit geladenen PDBs (aus .snupkg)."

} finally {
  Pop-Location | Out-Null
}