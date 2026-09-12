@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-app.ps1" %*
exit /b %ERRORLEVEL%
