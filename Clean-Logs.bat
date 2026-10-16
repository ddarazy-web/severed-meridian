@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Build\Clean-Logs.ps1" %*
exit /b %ERRORLEVEL%
