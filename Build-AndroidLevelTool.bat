@echo off
setlocal
call "%~dp0Build.bat" -Profile "Android Level Editor" %*
exit /b %ERRORLEVEL%
