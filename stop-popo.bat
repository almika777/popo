@echo off
setlocal
cd /d "%~dp0"

where docker >nul 2>&1
if errorlevel 1 (
  echo Docker Desktop не найден.
  pause
  exit /b 1
)

docker compose down
if errorlevel 1 (
  pause
  exit /b 1
)

echo Popo остановлен. Данные PostgreSQL сохранены в Docker volume.
endlocal
