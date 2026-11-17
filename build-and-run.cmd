@echo off
cd /d "%~dp0"
if /i "%~1"=="--rebuild" goto build
if exist "%~dp0dist\LANPUT-win-x64\LANPUT.exe" (
  start "" /d "%~dp0dist\LANPUT-win-x64" "%~dp0dist\LANPUT-win-x64\LANPUT.exe"
  exit /b 0
)
:build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Run
if errorlevel 1 (
  echo.
  echo Build failed. See the error above.
  pause
)
