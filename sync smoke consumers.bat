@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync smoke consumers.ps1"
pause
