param(
    [Parameter(Mandatory = $true)]
    [string]$ApiKey
)

$CsprojPath = ".\TUtils.Messages.Core.csproj"
$PackageId = "TUtils2.Messages.Core"
$Major = 0

$ErrorActionPreference = 'Stop'

function Get-HighestNuGetVersion {
    param([string]$Id)

    $lcId = $Id.ToLowerInvariant()
    $indexUrl = "https://api.nuget.org/v3-flatcontainer/$lcId/index.json"

    try {
        $resp = Invoke-RestMethod -Uri $indexUrl -Method GET -TimeoutSec 20
    }
    catch {
        if ($_.Exception.Response -and ($_.Exception.Response.StatusCode.Value__ -eq 404)) {
            return $null
        }
        throw
    }

    if (-not $resp.versions) { return $null }

    # Nur stabile Versionen (ohne -prerelease)
    $stable = $resp.versions | Where-Object { $_ -notmatch '-' }
    if (-not $stable) { return $null }

    $parsed = @()
    foreach ($v in $stable) {
        if ($v -match '^\d+(\.\d+){1,3}$') {
            try { $parsed += [version]$v } catch {}
        }
    }
    if (-not $parsed) { return $null }

    ($parsed | Sort-Object -Descending)[0]
}

function Compose-NextVersion {
    param(
        [version]$CurrentHighest, # kann $null sein
        [int]$Major
    )

    if ($null -eq $CurrentHighest) {
        return [version]"$Major.0.1"
    }

    # Originalstring behalten, damit 4 Segmente erhalten bleiben
    $s = $CurrentHighest.ToString()      # z.B. "2020.9.25.213911" oder "0.10.234"
    $parts = $s.Split('.')
    # setze Major neu
    $parts[0] = [string]$Major
    # letztes Segment +1
    $last = [int]$parts[-1]
    $parts[-1] = ([string]($last + 1))
    # wieder zusammensetzen
    $new = $parts -join '.'
    return [version]$new
}

function Ensure-DotNetCli {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet SDK nicht gefunden. Bitte .NET SDK installieren und PATH prüfen."
    }
}

function Build-And-Pack {
    param(
        [string]$CsprojPath,
        [string]$PackageId,
        [string]$Version,
        [string]$OutRoot # optional: eigener Root-Pfad für Ausgaben
    )

    if (-not (Test-Path $CsprojPath)) {
        throw ".csproj nicht gefunden: $CsprojPath"
    }

    $projDir = Split-Path -Parent $CsprojPath

    if ([string]::IsNullOrWhiteSpace($OutRoot)) {
        # -> Standard in <proj>\bin\NuGet\pkg_<guid>
        $OutRoot = Join-Path $projDir "bin\NuGet"
    }

    $outDir = Join-Path $OutRoot ("pkg_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    $packArgs = @(
        "pack", $CsprojPath,
        "-c", "Release",
        "-o", $outDir,
        "-p:PackageVersion=$Version",
        "-p:PackageId=$PackageId",
        "-p:IncludeSymbols=true",
        "-p:SymbolPackageFormat=snupkg",
        "-p:ContinuousIntegrationBuild=true"
    )

    Write-Host "dotnet $($packArgs -join ' ')" -ForegroundColor DarkGray
    & dotnet @packArgs | Write-Host

    $nupkg  = Get-ChildItem -Path $outDir -Filter "*.nupkg"  | Where-Object { $_.Name -notlike "*.snupkg" } | Select-Object -First 1
    $snupkg = Get-ChildItem -Path $outDir -Filter "*.snupkg" | Select-Object -First 1

    if (-not $nupkg)  { throw "Kein .nupkg im Ausgabeordner gefunden." }
    [PSCustomObject]@{ NupkgPath = $nupkg.FullName; SnupkgPath = $snupkg?.FullName }
}


function Push-To-NuGet {
    param([string]$PackagePath,[string]$ApiKey)
    $source = "https://api.nuget.org/v3/index.json"
    $args = @("nuget","push",$PackagePath,"--source",$source,"--api-key",$ApiKey,"--skip-duplicate")
    Write-Host "dotnet $($args -join ' ')" -ForegroundColor DarkGray
    & dotnet @args | Write-Host
}

function Test-NuGetVersionAvailable {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$Version
    )

    $lcId = $PackageId.ToLowerInvariant()
    $indexUrl = "https://api.nuget.org/v3-flatcontainer/$lcId/index.json"
    $nupkgUrl = "https://api.nuget.org/v3-flatcontainer/$lcId/$Version/$lcId.$Version.nupkg"

    try {
        $resp = Invoke-RestMethod -Uri $indexUrl -Method GET -TimeoutSec 15
        $hasInIndex = $resp.versions -contains $Version
    } catch {
        $hasInIndex = $false
    }

    # HEAD auf das eigentliche .nupkg (ist oft früher verfügbar)
    try {
        $req = [System.Net.HttpWebRequest]::Create($nupkgUrl)
        $req.Method = "HEAD"
        $req.Timeout = 15000
        $resp2 = $req.GetResponse()
        $hasNupkg = $true
        $resp2.Close()
    } catch {
        $hasNupkg = $false
    }

    return ($hasInIndex -or $hasNupkg)
}

function Wait-ForNuGetAvailability {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$Version,
        [int]$TimeoutMinutes = 10
    )

    $stopAt = (Get-Date).AddMinutes($TimeoutMinutes)
    $attempt = 0
    while ((Get-Date) -lt $stopAt) {
        $attempt++
        if (Test-NuGetVersionAvailable -PackageId $PackageId -Version $Version) {
            Write-Host "NuGet zeigt $PackageId $Version als verfügbar." -ForegroundColor Green
            return
        }
        Write-Host "[$attempt] Noch nicht indexiert – warte 10s..." -ForegroundColor Yellow
        Start-Sleep -Seconds 10
    }
    Write-Warning "Timeout: $PackageId $Version ist nach $TimeoutMinutes Min. noch nicht im Index sichtbar."
}





# --- Main ---
Write-Host "== NuGet Publish Script ==" -ForegroundColor Cyan
Write-Host "PackageId: $PackageId"
Write-Host "Major:     $Major"
Write-Host ""

Ensure-DotNetCli

Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
[void][System.Console]::ReadKey($true)

# 1) Existenz & höchste stabile Version ermitteln (Prereleases werden ignoriert)
$highest = Get-HighestNuGetVersion -Id $PackageId
if ($null -eq $highest) {
    Write-Host "Paket '$PackageId' existiert auf nuget.org noch nicht (stabile Versionen: keine)." -ForegroundColor Yellow
} else {
    Write-Host "Aktuell höchste stabile Version auf nuget.org: $($highest.ToString())"
}

Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
[void][System.Console]::ReadKey($true)

# 2) Neue Version bilden
$newVersion = (Compose-NextVersion -CurrentHighest $highest -Major $Major).ToString()
Write-Host "Neue Version wird: $newVersion" -ForegroundColor Green

Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
[void][System.Console]::ReadKey($true)

# 3) Packen (inkl. .snupkg)
$pkg = Build-And-Pack -CsprojPath $CsprojPath -PackageId $PackageId -Version $newVersion `
                      -OutRoot (Join-Path (Split-Path -Parent $CsprojPath) "bin\Packages")

Write-Host "Paket erstellt: $($pkg.NupkgPath)" -ForegroundColor Green
if ($pkg.SnupkgPath) { Write-Host "Symbols:       $($pkg.SnupkgPath)" -ForegroundColor Green }

Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
[void][System.Console]::ReadKey($true)

# 4) Pushen (.nupkg und .snupkg)
Push-To-NuGet -PackagePath $pkg.NupkgPath -ApiKey $ApiKey

Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
[void][System.Console]::ReadKey($true)

if ($pkg.SnupkgPath) { Push-To-NuGet -PackagePath $pkg.SnupkgPath -ApiKey $ApiKey }


Write-Host "Uploaded" -ForegroundColor Cyan

Wait-ForNuGetAvailability -PackageId $PackageId -Version $newVersion -TimeoutMinutes 10

Write-Host "Fertig." -ForegroundColor Cyan

Write-Host "Drücke eine beliebige Taste, um fortzufahren..."
[void][System.Console]::ReadKey($true)

