@echo off
setlocal
call "%~dp0Build.bat" -Profile "iOS Game" %*
exit /b %ERRORLEVEL%
