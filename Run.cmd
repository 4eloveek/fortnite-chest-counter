@echo off
setlocal
set "APP=%~dp0FortniteChestCounter-v0.2.0-win-x64.exe"
if not exist "%APP%" set "APP=%~dp0release\v0.2.0-win-x64\FortniteChestCounter-v0.2.0-win-x64.exe"
if not exist "%APP%" set "APP=%~dp0dist\FortniteChestCounter\FortniteChestCounter.exe"
if not exist "%APP%" (
    echo The app is missing. Run Build.cmd or tools\build-release.ps1 first.
    pause
    exit /b 1
)
for %%I in ("%APP%") do start "" /D "%%~dpI" "%APP%"
exit /b 0
