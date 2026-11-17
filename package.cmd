@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\package.ps1"
if errorlevel 1 (
  echo Packaging failed. See the error above.
  pause
  exit /b 1
)
