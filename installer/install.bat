@echo off
setlocal
cd /d "%~dp0"
echo Vampire Hunters VR - installer
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set "code=%errorlevel%"
echo.
if "%code%"=="0" (echo [OK] install finished.) else (echo [FAILED] install exit code %code%)
echo.
pause
exit /b %code%