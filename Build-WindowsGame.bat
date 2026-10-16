@echo off
setlocal
call "%~dp0Build.bat" -Profile "Windows Steam Game" %*
exit /b %ERRORLEVEL%
