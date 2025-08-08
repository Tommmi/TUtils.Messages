@echo off
setlocal

REM Verzeichnis der Batchdatei ermitteln
set "SCRIPT_DIR=%~dp0"

set "CsProjFilePath=.\TUtils.Messages.Core.csproj"
set "packagesource=https://api.nuget.org/v3/index.json"
set "PackageId=TUtils2.Messages.Core"


REM PowerShell 7 Skript ausführen
pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%buildAndUploadNuget.ps1" -PackageId "%PackageId%" -CsprojPath "%CsProjFilePath%" -ForceLocalBuild -PackageSource "%packagesource%" -WaitForAvailability -Push -IncludeSymbols -AutoVersion

endlocal
pause
