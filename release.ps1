# Pack Custom Car Saves and publish a GitHub Release (win-x64 + win-x86).
# Beta (default):  .\release.ps1 -Version 0.1.0-beta.1
# Stable:          .\release.ps1 -Version 1.0.0 -Stable
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Stable,

    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [System.Environment]::GetEnvironmentVariable("Path", "User")

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) not found. Install with: winget install GitHub.cli"
}

gh auth status 2>$null
if ($LASTEXITCODE -ne 0) { throw "Not logged in. Run: gh auth login" }
gh auth setup-git 2>$null | Out-Null

$ver = $Version.Trim()
if ($ver.StartsWith("v")) { $ver = $ver.Substring(1) }
$tag = "v$ver"

$isPrerelease = -not $Stable.IsPresent
if ($isPrerelease -and ($ver -notmatch "beta|rc|preview|alpha")) {
    Write-Warning "Version '$ver' has no beta/rc marker; publishing as pre-release anyway (omit -Stable)."
}

Write-Host "Building + packing win-x64 + win-x86 via pack-beta.ps1 ..."
& (Join-Path $root "pack-beta.ps1") -Rids @("win-x64", "win-x86")
if ($LASTEXITCODE -ne 0) { throw "pack-beta.ps1 failed" }

$desk = [Environment]::GetFolderPath("Desktop")
function Latest-PackZip([string]$rid) {
    $hit = Get-ChildItem -Path $desk -Filter ("CustomCarSaves_Beta_{0}_*.zip" -f $rid) |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $hit) { throw "No CustomCarSaves_Beta_${rid}_*.zip found on Desktop after pack." }
    return $hit
}

$zip64 = Latest-PackZip "win-x64"
$zip86 = Latest-PackZip "win-x86"

$asset64 = "NFSU2CustomCarSaves-$ver-win-x64.zip"
$asset86 = "NFSU2CustomCarSaves-$ver-win-x86.zip"
$staged64 = Join-Path $env:TEMP $asset64
$staged86 = Join-Path $env:TEMP $asset86
Copy-Item -Force $zip64.FullName $staged64
Copy-Item -Force $zip86.FullName $staged86

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $Notes = @"
## NFSU2 CCS $ver

- Feature updates for this build (edit notes before publish if needed)

### Install
1. Download the zip for your PC:
   - ``$asset64`` — 64-bit Windows (most PCs)
   - ``$asset86`` — 32-bit Windows
2. Extract the ``NFSU2CustomCarSaves`` folder
3. Run ``NFSU2CustomCarSaves.exe``

Self-contained (single-file) — no separate .NET install.
Layout: ``exe`` + ``Assets/`` ``Data/`` ``Stock/`` ``Tools/``. Keep the whole folder together.
"@
}

$title = if ($isPrerelease) { "NFSU2 CCS Beta $ver" } else { "NFSU2 CCS $ver" }

git rev-parse --is-inside-work-tree 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Not a git repository." }

$branch = (git rev-parse --abbrev-ref HEAD).Trim()

cmd /c "git push -u origin $branch >nul 2>&1"
if ($LASTEXITCODE -ne 0) {
    Write-Warning "git push returned $LASTEXITCODE - continuing if remote already has the commit."
}

cmd /c "git rev-parse $tag >nul 2>&1"
if ($LASTEXITCODE -ne 0) {
    git tag $tag
    cmd /c "git push origin $tag >nul 2>&1"
}

Write-Host "Creating GitHub release $tag ..."
$notesFile = Join-Path $env:TEMP ("ccs_release_notes_" + [guid]::NewGuid().ToString("N") + ".md")
Set-Content -Path $notesFile -Value $Notes -Encoding utf8

$ghArgs = @(
    "release", "create", $tag,
    $staged64,
    $staged86,
    "--title", $title,
    "--notes-file", $notesFile,
    "--target", $branch
)
if ($isPrerelease) { $ghArgs += "--prerelease" }

& gh @ghArgs
$relCode = $LASTEXITCODE
Remove-Item $notesFile -Force -ErrorAction SilentlyContinue
if ($relCode -ne 0) { throw "gh release create failed" }

$repo = (gh repo view --json nameWithOwner -q .nameWithOwner).Trim()
$url = "https://github.com/$repo/releases/tag/$tag"

Write-Host ""
Write-Host "DONE: $url"
Write-Host "Discord: share $url or https://github.com/$repo/releases"
Write-Host ("Assets:  {0} + {1}" -f $asset64, $asset86)

Remove-Item $staged64, $staged86 -Force -ErrorAction SilentlyContinue
