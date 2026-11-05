# Build a public beta zip with stock dynos / liveries / rack / flags / DYNO SAVES.
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

Write-Host "Building Release..."
dotnet build .\NFSU2CustomCarSaves.csproj -c Release -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$out = Join-Path $root "bin\Release\net8.0-windows"
$stage = Join-Path $env:TEMP ("CCS_beta_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Write-Host "Staging from $out"
Copy-Item -Recurse -Force $out\* $stage\

# Ensure stock libraries are present for first-run seed + Presets merge
Copy-Item -Force (Join-Path $root "Presets\dyno_library.json") (Join-Path $stage "Presets\dyno_library.json")
Copy-Item -Force (Join-Path $root "Presets\livery_library.json") (Join-Path $stage "Presets\livery_library.json")
Copy-Item -Force (Join-Path $root "Presets\save_rack.json") (Join-Path $stage "Presets\save_rack.json") -ErrorAction SilentlyContinue

# Pre-seed Data so first launch already has your libraries (no empty start)
$data = Join-Path $stage "Data"
New-Item -ItemType Directory -Force -Path $data | Out-Null
Copy-Item -Force (Join-Path $stage "Presets\dyno_library.json") (Join-Path $data "dyno_library.json")
Copy-Item -Force (Join-Path $stage "Presets\livery_library.json") (Join-Path $data "livery_library.json")
Copy-Item -Force (Join-Path $stage "Presets\save_rack.json") (Join-Path $data "save_rack.json") -ErrorAction SilentlyContinue

# Don't ship personal clipboard / roam pick
Remove-Item (Join-Path $data "dyno_clipboard.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $data "roam_flag.txt") -Force -ErrorAction SilentlyContinue

$desk = [Environment]::GetFolderPath("Desktop")
$zip = Join-Path $desk ("CustomCarSaves_Beta_" + (Get-Date -Format "yyyyMMdd_HHmm") + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "Zipping -> $zip"
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
Remove-Item -Recurse -Force $stage

Write-Host ""
Write-Host "DONE: $zip"
Write-Host "Share that zip. Recipients extract and run NFSU2CustomCarSaves.exe"
Write-Host "Includes: dyno library, livery library, save rack seed, DYNO SAVES, Presets, flags."
