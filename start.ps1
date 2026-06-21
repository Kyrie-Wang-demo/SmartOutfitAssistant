$ErrorActionPreference = "Stop"
Write-Host "Starting Smart Outfit Assistant..." -ForegroundColor Cyan
dotnet run --urls http://localhost:5187
