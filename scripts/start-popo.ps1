$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

function Stop-WithMessage([string]$message) {
    Write-Host $message
    Read-Host "Нажмите Enter для выхода"
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Stop-WithMessage "Docker Desktop не найден. Установите Docker Desktop и запустите его."
}

cmd /d /c "docker info >nul 2>&1"
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage "Docker Desktop не запущен. Запустите его и повторите запуск Popo."
}

if (-not (Test-Path -LiteralPath ".env")) {
    Copy-Item -LiteralPath ".env.example" -Destination ".env"
    Write-Host "Создан файл .env с настройками по умолчанию."
}

$lanIpScript = Join-Path $PSScriptRoot "get-lan-ip.ps1"
$lanHost = & $lanIpScript | Select-Object -First 1
if ($null -eq $lanHost) {
    $lanHost = ""
}
else {
    $lanHost = $lanHost.Trim()
}
$env:POPO_LAN_HOST = $lanHost

if ($lanHost) {
    Write-Host "LAN-адрес для телефона: http://${lanHost}:7955"
}
else {
    Write-Host "LAN-адрес не определён. Ссылка для телефона будет недоступна."
}

Write-Host "Запуск Popo. Первый запуск может занять несколько минут."
docker compose up --build -d --wait
if ($LASTEXITCODE -ne 0) {
    Write-Host "Не удалось запустить Popo. Подробности:"
    docker compose ps
    Read-Host "Нажмите Enter для выхода"
    exit 1
}

Write-Host "Popo запущен: http://localhost:7955"
Start-Process "http://localhost:7955"
