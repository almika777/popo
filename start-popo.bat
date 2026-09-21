@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-popo.ps1"
exit /b %errorlevel%
