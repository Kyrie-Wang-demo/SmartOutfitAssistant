$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$url = "http://0.0.0.0:5187"
$project = Join-Path $root "ConsoleApp2.csproj"

Write-Host "Starting Smart Outfit Assistant for LAN..." -ForegroundColor Cyan
Write-Host "Binding: $url"

try {
    $ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
        $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" -and $_.PrefixOrigin -ne "WellKnown"
    } | Select-Object -ExpandProperty IPAddress
    if ($ips) {
        Write-Host "Other devices on the same Wi-Fi/LAN can open:" -ForegroundColor Green
        foreach ($ip in $ips) { Write-Host "  http://$ip`:5187" -ForegroundColor Yellow }
    }
} catch {
    Write-Host "Could not list LAN IP automatically. Run ipconfig and use your IPv4 address with :5187." -ForegroundColor Yellow
}

Write-Host "If Windows Firewall asks for permission, choose Allow." -ForegroundColor Yellow
dotnet run --project $project --urls $url
