@echo off
rem Double-click to build. The work is in tools\build.ps1; this file exists
rem because a .bat opens on a double-click and a .ps1 does not. Keep this
rem file ASCII: cmd misreads a .bat that holds non-ASCII text.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build.ps1" %*
echo.
pause
