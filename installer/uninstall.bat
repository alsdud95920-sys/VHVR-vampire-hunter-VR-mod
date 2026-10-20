@echo off
setlocal
cd /d "%~dp0"
echo Vampire Hunters VR - uninstaller (Force enabled by default)
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" -Force %*
set "code=%errorlevel%"
echo.
if "%code%"=="0" (echo [OK] uninstall finished.) else (echo [FAILED] uninstall exit code %code%)
echo.
pause
exit /b %code%