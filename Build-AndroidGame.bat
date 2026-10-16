@echo off
setlocal
call "%~dp0Build.bat" -Profile "Android Game" %*
exit /b %ERRORLEVEL%
