# Build a self-contained single-file portable zip (win-x64, .NET runtime included).
# Layout:
#   NFSU2CustomCarSaves/
#     NFSU2CustomCarSaves.exe
#     Assets/
#     Data/
#     Stock/   (Presets, DynoSaves, Templates, Stats, car-donors.json)
#     Tools/   (Wipe-All.bat)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$rid = "win-x64"
$publishDir = Join-Path $root "publish\$rid"
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

Write-Host "Publishing self-contained single-file $rid..."
dotnet publish .\NFSU2CustomCarSaves.csproj `
    -c Release `
    -r $rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
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

# Runtime / publish leftovers users never need at root
Remove-Item (Join-Path $app "createdump.exe") -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path $app -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
$cultureDirs = @("cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant")
foreach ($c in $cultureDirs) {
    $p = Join-Path $app $c
    if (Test-Path $p) { Remove-Item -Recurse -Force $p }
}

# Ensure Stock layout (in case TargetPath didn't apply for some items)
$stock = Join-Path $app "Stock"
$presets = Join-Path $stock "Presets"
$dyno = Join-Path $stock "DynoSaves"
$templates = Join-Path $stock "Templates"
$stats = Join-Path $stock "Stats"
$tools = Join-Path $app "Tools"
New-Item -ItemType Directory -Force -Path $presets, $dyno, $templates, $stats, $tools | Out-Null

# Move legacy root copies into Stock/Tools if publish still emitted them
function Move-IfPresent([string]$src, [string]$destDir) {
    if (-not (Test-Path $src)) { return }
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    if ((Get-Item $src).PSIsContainer) {
        Copy-Item -Recurse -Force (Join-Path $src "*") $destDir
        Remove-Item -Recurse -Force $src
    } else {
        Copy-Item -Force $src (Join-Path $destDir (Split-Path $src -Leaf))
        Remove-Item -Force $src
    }
}

Move-IfPresent (Join-Path $app "Presets") $presets
Move-IfPresent (Join-Path $app "DYNO SAVES") $dyno
Move-IfPresent (Join-Path $app "DynoSaves") $dyno
Move-IfPresent (Join-Path $app "Templates") $templates
Move-IfPresent (Join-Path $app "Stats") $stats
Move-IfPresent (Join-Path $app "car-donors.json") $stock
Move-IfPresent (Join-Path $app "Wipe-All.bat") $tools

# Prefer fresh stock libraries from repo
Copy-Item -Force (Join-Path $root "Presets\dyno_library.json") (Join-Path $presets "dyno_library.json")
Copy-Item -Force (Join-Path $root "Presets\livery_library.json") (Join-Path $presets "livery_library.json")
Copy-Item -Force (Join-Path $root "Presets\save_rack.json") (Join-Path $presets "save_rack.json") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "car-donors.json") (Join-Path $stock "car-donors.json")
Copy-Item -Force (Join-Path $root "Wipe-All.bat") (Join-Path $tools "Wipe-All.bat")
New-Item -ItemType Directory -Force -Path (Join-Path $app "Assets") | Out-Null
Copy-Item -Force (Join-Path $root "Assets\app.ico") (Join-Path $app "Assets\app.ico")
Copy-Item -Force (Join-Path $root "Assets\ccs-logo.png") (Join-Path $app "Assets\ccs-logo.png") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "Assets\ccs-text.png") (Join-Path $app "Assets\ccs-text.png") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "Assets\settings-icon.png") (Join-Path $app "Assets\settings-icon.png") -ErrorAction SilentlyContinue

# Pre-seed Data so first launch already has libraries
$data = Join-Path $app "Data"
New-Item -ItemType Directory -Force -Path $data | Out-Null
Copy-Item -Force (Join-Path $presets "dyno_library.json") (Join-Path $data "dyno_library.json")
Copy-Item -Force (Join-Path $presets "livery_library.json") (Join-Path $data "livery_library.json")
Copy-Item -Force (Join-Path $presets "save_rack.json") (Join-Path $data "save_rack.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $data "dyno_clipboard.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $data "roam_flag.txt") -Force -ErrorAction SilentlyContinue

$exe = Join-Path $app "NFSU2CustomCarSaves.exe"
if (-not (Test-Path $exe)) {
    throw "Missing NFSU2CustomCarSaves.exe in staged folder."
}

# Must-have stock checks
$must = @(
    (Join-Path $stock "car-donors.json"),
    (Join-Path $templates "JANE"),
    (Join-Path $presets "dyno_library.json"),
    (Join-Path $app "Assets\app.ico")
)
foreach ($m in $must) {
    if (-not (Test-Path $m)) { throw "Missing required file after staging: $m" }
}

$desk = [Environment]::GetFolderPath("Desktop")
$zip = Join-Path $desk ("CustomCarSaves_Beta_" + (Get-Date -Format "yyyyMMdd_HHmm") + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "Zipping -> $zip"
Compress-Archive -Path $app -DestinationPath $zip -Force
Remove-Item -Recurse -Force $stage

Write-Host ""
Write-Host "DONE: $zip"
Write-Host "Root: exe + Assets / Data / Stock / Tools (single-file, self-contained win-x64)."
