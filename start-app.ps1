$ErrorActionPreference = "Stop"
$url = "http://localhost:5187"
$project = Join-Path $PSScriptRoot "ConsoleApp2.csproj"
Write-Host "Starting Smart Outfit Assistant App..." -ForegroundColor Cyan
$server = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList @("run", "--project", $project, "--urls", $url)
try {
  Start-Sleep -Seconds 3
  $edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
  if (Test-Path $edge) { Start-Process -FilePath $edge -ArgumentList @("--app=$url") } else { Start-Process $url }
  Write-Host "App is running at $url . Press Ctrl+C to stop." -ForegroundColor Green
  Wait-Process -Id $server.Id
}
finally { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
