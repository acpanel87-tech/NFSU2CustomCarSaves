# Build self-contained single-file portable zips (win-x64 + win-x86, .NET runtime included).
# Layout inside each zip:
#   NFSU2CustomCarSaves/
#     NFSU2CustomCarSaves.exe
#     Assets/
#     Data/
#     Stock/   (Presets, DynoSaves, Templates, Stats, car-donors.json)
#     Tools/   (Wipe-All.bat)
param(
    [string[]]$Rids = @("win-x64", "win-x86")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

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

function Pack-Rid([string]$rid) {
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
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }

    $stage = Join-Path $env:TEMP ("CCS_beta_" + $rid + "_" + [guid]::NewGuid().ToString("N"))
    $app = Join-Path $stage "NFSU2CustomCarSaves"
    New-Item -ItemType Directory -Force -Path $app | Out-Null

    Write-Host "Staging portable folder from $publishDir"
    Copy-Item -Recurse -Force (Join-Path $publishDir "*") $app\

    Remove-Item (Join-Path $app "createdump.exe") -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $app -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
    $cultureDirs = @("cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant")
    foreach ($c in $cultureDirs) {
        $p = Join-Path $app $c
        if (Test-Path $p) { Remove-Item -Recurse -Force $p }
    }

    $stock = Join-Path $app "Stock"
    $presets = Join-Path $stock "Presets"
    $dyno = Join-Path $stock "DynoSaves"
    $templates = Join-Path $stock "Templates"
    $stats = Join-Path $stock "Stats"
    $tools = Join-Path $app "Tools"
    New-Item -ItemType Directory -Force -Path $presets, $dyno, $templates, $stats, $tools | Out-Null

    Move-IfPresent (Join-Path $app "Presets") $presets
    Move-IfPresent (Join-Path $app "DYNO SAVES") $dyno
    Move-IfPresent (Join-Path $app "DynoSaves") $dyno
    Move-IfPresent (Join-Path $app "Templates") $templates
    Move-IfPresent (Join-Path $app "Stats") $stats
    Move-IfPresent (Join-Path $app "car-donors.json") $stock
    Move-IfPresent (Join-Path $app "Wipe-All.bat") $tools

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

    $data = Join-Path $app "Data"
    New-Item -ItemType Directory -Force -Path $data | Out-Null
    Copy-Item -Force (Join-Path $presets "dyno_library.json") (Join-Path $data "dyno_library.json")
    Copy-Item -Force (Join-Path $presets "livery_library.json") (Join-Path $data "livery_library.json")
    Copy-Item -Force (Join-Path $presets "save_rack.json") (Join-Path $data "save_rack.json") -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $data "dyno_clipboard.json") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $data "roam_flag.txt") -Force -ErrorAction SilentlyContinue

    $exe = Join-Path $app "NFSU2CustomCarSaves.exe"
    if (-not (Test-Path $exe)) {
        throw "Missing NFSU2CustomCarSaves.exe in staged folder ($rid)."
    }

    $must = @(
        (Join-Path $stock "car-donors.json"),
        (Join-Path $templates "JANE"),
        (Join-Path $presets "dyno_library.json"),
        (Join-Path $app "Assets\app.ico")
    )
    foreach ($m in $must) {
        if (-not (Test-Path $m)) { throw "Missing required file after staging ($rid): $m" }
    }

    $desk = [Environment]::GetFolderPath("Desktop")
    $stamp = Get-Date -Format "yyyyMMdd_HHmm"
    $zip = Join-Path $desk ("CustomCarSaves_Beta_{0}_{1}.zip" -f $rid, $stamp)
    if (Test-Path $zip) { Remove-Item $zip -Force }

    Write-Host "Zipping -> $zip"
    Compress-Archive -Path $app -DestinationPath $zip -Force
    Remove-Item -Recurse -Force $stage
    return $zip
}

$built = @()
foreach ($rid in $Rids) {
    $built += Pack-Rid $rid
}

Write-Host ""
Write-Host "DONE:"
foreach ($z in $built) { Write-Host "  $z" }
Write-Host "Root: exe + Assets / Data / Stock / Tools (single-file, self-contained)."
