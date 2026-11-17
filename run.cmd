@echo off
cd /d "%~dp0"
if not exist "%~dp0dist\LANPUT-win-x64\LANPUT.exe" (
  call "%~dp0build-and-run.cmd" --rebuild
  exit /b
)
start "" /d "%~dp0dist\LANPUT-win-x64" "%~dp0dist\LANPUT-win-x64\LANPUT.exe"
