$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "ConsoleApp2.csproj"
$tools = Join-Path $root "tools"
$cloudflared = Join-Path $tools "cloudflared.exe"
$outLog = Join-Path $root "cloudflared.out.log"
$errLog = Join-Path $root "cloudflared.err.log"
$appOut = Join-Path $root "public-app.out.log"
$appErr = Join-Path $root "public-app.err.log"
$url = "http://127.0.0.1:5187"

New-Item -ItemType Directory -Force $tools | Out-Null

# Start local app if it is not already running.
$running = $false
try {
    $r = Invoke-WebRequest -Uri "$url/api/health" -UseBasicParsing -TimeoutSec 2
    if ($r.StatusCode -eq 200) { $running = $true }
} catch {}

if (-not $running) {
    Write-Host "Starting local website..." -ForegroundColor Cyan
    Start-Process -WindowStyle Hidden -FilePath "dotnet" -ArgumentList @("run", "--project", $project, "--urls", $url) -RedirectStandardOutput $appOut -RedirectStandardError $appErr | Out-Null
    Start-Sleep -Seconds 5
}

if (-not (Test-Path $cloudflared)) {
    Write-Host "Downloading Cloudflare Tunnel client..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile $cloudflared -UseBasicParsing
}

Remove-Item -LiteralPath $outLog,$errLog -Force -ErrorAction SilentlyContinue
Write-Host "Starting public tunnel..." -ForegroundColor Cyan
Start-Process -WindowStyle Hidden -FilePath $cloudflared -ArgumentList @("tunnel", "--url", $url, "--no-autoupdate") -RedirectStandardOutput $outLog -RedirectStandardError $errLog | Out-Null

$public = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    $text = ""
    if (Test-Path $outLog) { $text += Get-Content $outLog -Raw -ErrorAction SilentlyContinue }
    if (Test-Path $errLog) { $text += Get-Content $errLog -Raw -ErrorAction SilentlyContinue }
    $m = [regex]::Match($text, "https://[a-zA-Z0-9-]+\.trycloudflare\.com")
    if ($m.Success) { $public = $m.Value; break }
}

if ($public) {
    Set-Content -Encoding UTF8 (Join-Path $root "PUBLIC_URL.txt") $public
    Write-Host "Public website URL:" -ForegroundColor Green
    Write-Host $public -ForegroundColor Yellow
    Write-Host "Share this link with others. Keep this PowerShell window/process running while they use it." -ForegroundColor Green
    Start-Process $public
} else {
    Write-Host "Tunnel started, but public URL was not detected yet. Check logs:" -ForegroundColor Yellow
    Write-Host "  $outLog"
    Write-Host "  $errLog"
}
