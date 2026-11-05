# Pack Custom Car Saves and publish a GitHub Release.
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

# Normalize version / tag
$ver = $Version.Trim()
if ($ver.StartsWith("v")) { $ver = $ver.Substring(1) }
$tag = "v$ver"

$isPrerelease = -not $Stable.IsPresent
if ($isPrerelease -and ($ver -notmatch "beta|rc|preview|alpha")) {
    Write-Warning "Version '$ver' has no beta/rc marker; publishing as pre-release anyway (omit -Stable)."
}

Write-Host "Building + packing via pack-beta.ps1 ..."
& (Join-Path $root "pack-beta.ps1")
if ($LASTEXITCODE -ne 0) { throw "pack-beta.ps1 failed" }

$desk = [Environment]::GetFolderPath("Desktop")
$zip = Get-ChildItem -Path $desk -Filter "CustomCarSaves_Beta_*.zip" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $zip) { throw "No CustomCarSaves_Beta_*.zip found on Desktop after pack." }

$assetName = "NFSU2CustomCarSaves-$ver.zip"
$stagedZip = Join-Path $env:TEMP $assetName
Copy-Item -Force $zip.FullName $stagedZip

if ([string]::IsNullOrWhiteSpace($Notes)) {
    if ($isPrerelease) {
        $Notes = @"
## NFSU2 CCS Beta $ver

Public Discord beta of **Custom Car Saves**.

1. Download the zip below
2. Extract
3. Run ``NFSU2CustomCarSaves.exe``

Includes stock dyno library, livery library, save rack seed, DYNO SAVES, Presets, and flags.
"@
    } else {
        $Notes = @"
## NFSU2 CCS $ver

1. Download the zip below
2. Extract
3. Run ``NFSU2CustomCarSaves.exe``
"@
    }
}

$title = if ($isPrerelease) { "NFSU2 CCS Beta $ver" } else { "NFSU2 CCS $ver" }

git rev-parse --is-inside-work-tree 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Not a git repository." }

$branch = (git rev-parse --abbrev-ref HEAD).Trim()

# Push branch (ignore stderr noise from credential helpers)
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
    $stagedZip,
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
Write-Host ("Asset:   {0} (from {1})" -f $assetName, $zip.Name)

Remove-Item $stagedZip -Force -ErrorAction SilentlyContinue
