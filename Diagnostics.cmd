@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\diagnose.ps1"
set "RESULT=%ERRORLEVEL%"
pause
exit /b %RESULT%
