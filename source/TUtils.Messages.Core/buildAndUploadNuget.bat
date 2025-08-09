@echo off
setlocal

REM Verzeichnis der Batchdatei ermitteln
set "SCRIPT_DIR=%~dp0"

REM PowerShell 7 Skript ausführen
pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%buildAndUploadNuget.ps1"

endlocal
pause
