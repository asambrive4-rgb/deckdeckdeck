@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0measure-win-startup.ps1" %*
exit /b %ERRORLEVEL%

