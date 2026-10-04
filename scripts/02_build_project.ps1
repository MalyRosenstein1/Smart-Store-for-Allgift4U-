$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root "backend\SmartStore.sln"
$frontend = Join-Path $root "frontend\smart-store"
$buildOutput = Join-Path $env:TEMP "smartstore-build\"

if (-not (Test-Path $buildOutput)) {
    New-Item -ItemType Directory -Force -Path $buildOutput | Out-Null
}

Write-Host "Building backend solution to temp output to avoid conflicts with running API..." -ForegroundColor Cyan
dotnet build $backend /p:BaseOutputPath="$buildOutput"

if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
    Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
    npm install --prefix $frontend
}

Write-Host "Building frontend..." -ForegroundColor Cyan
npm run build --prefix $frontend

Write-Host "Build completed successfully." -ForegroundColor Green
