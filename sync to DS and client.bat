@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync to DS and client.ps1"
if errorlevel 1 (
    echo.
    echo Sync failed. Read the error above.
)
pause