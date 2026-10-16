@echo off
setlocal
call "%~dp0Build.bat" -Profile "Windows Level Editor" %*
exit /b %ERRORLEVEL%
