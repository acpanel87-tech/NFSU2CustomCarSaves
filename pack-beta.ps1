# Build a self-contained portable beta zip (win-x64, .NET 8 runtime included).
# Layout:
#   NFSU2CustomCarSaves/
#     NFSU2CustomCarSaves.exe
#     README.txt
#     Assets/  Presets/  DYNO SAVES/  Templates/  Stats/  Data/
#     (+ runtime DLLs)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$rid = "win-x64"
$publishDir = Join-Path $root "publish\$rid"
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

Write-Host "Publishing self-contained $rid (runtime included)..."
dotnet publish .\NFSU2CustomCarSaves.csproj `
    -c Release `
    -r $rid `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:SatelliteResourceLanguages=en `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$stage = Join-Path $env:TEMP ("CCS_beta_" + [guid]::NewGuid().ToString("N"))
$app = Join-Path $stage "NFSU2CustomCarSaves"
New-Item -ItemType Directory -Force -Path $app | Out-Null

Write-Host "Staging portable folder from $publishDir"
Copy-Item -Recurse -Force (Join-Path $publishDir "*") $app\

# Runtime junk users never need
Remove-Item (Join-Path $app "createdump.exe") -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path $app -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
# Drop leftover satellite folders if publish still emitted them
$cultureDirs = @("cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant")
foreach ($c in $cultureDirs) {
    $p = Join-Path $app $c
    if (Test-Path $p) { Remove-Item -Recurse -Force $p }
}

# Ensure stock libraries are present for first-run seed + Presets merge
$presets = Join-Path $app "Presets"
New-Item -ItemType Directory -Force -Path $presets | Out-Null
Copy-Item -Force (Join-Path $root "Presets\dyno_library.json") (Join-Path $presets "dyno_library.json")
Copy-Item -Force (Join-Path $root "Presets\livery_library.json") (Join-Path $presets "livery_library.json")
Copy-Item -Force (Join-Path $root "Presets\save_rack.json") (Join-Path $presets "save_rack.json") -ErrorAction SilentlyContinue

# Pre-seed Data so first launch already has libraries (no empty start)
$data = Join-Path $app "Data"
New-Item -ItemType Directory -Force -Path $data | Out-Null
Copy-Item -Force (Join-Path $presets "dyno_library.json") (Join-Path $data "dyno_library.json")
Copy-Item -Force (Join-Path $presets "livery_library.json") (Join-Path $data "livery_library.json")
Copy-Item -Force (Join-Path $presets "save_rack.json") (Join-Path $data "save_rack.json") -ErrorAction SilentlyContinue

# Don't ship personal clipboard / roam pick
Remove-Item (Join-Path $data "dyno_clipboard.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $data "roam_flag.txt") -Force -ErrorAction SilentlyContinue

$readme = @"
NFSU2 Custom Car Saves (CCS)
============================

1. Keep this whole folder together (do not move only the .exe).
2. Double-click NFSU2CustomCarSaves.exe

No separate .NET install needed — the Windows x64 runtime is included.

Stock dyno / livery libraries and save-rack seed load on first run.
Not affiliated with EA / Black Box.
"@
Set-Content -Path (Join-Path $app "README.txt") -Value $readme -Encoding UTF8

$exe = Join-Path $app "NFSU2CustomCarSaves.exe"
if (-not (Test-Path $exe)) {
    throw "Missing NFSU2CustomCarSaves.exe in staged folder."
}

$desk = [Environment]::GetFolderPath("Desktop")
$zip = Join-Path $desk ("CustomCarSaves_Beta_" + (Get-Date -Format "yyyyMMdd_HHmm") + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "Zipping -> $zip"
Compress-Archive -Path $app -DestinationPath $zip -Force
Remove-Item -Recurse -Force $stage

Write-Host ""
Write-Host "DONE: $zip"
Write-Host "Share that zip. Recipients extract the NFSU2CustomCarSaves folder and run NFSU2CustomCarSaves.exe"
Write-Host "Self-contained win-x64: includes .NET runtime + dyno/livery/rack seed, DYNO SAVES, Presets, flags."
