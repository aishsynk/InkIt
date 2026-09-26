<#
.SYNOPSIS
    Builds, packages and publishes the next InkIt release to GitHub.

.DESCRIPTION
    Version numbers work like a database auto-increment:
      * version.txt holds the last RELEASED version (0.0.0.0 = nothing released yet).
      * Each run releases exactly the next number: 0.0.0.1, 0.0.0.2, 0.0.0.3 ... (the last part keeps counting: 0.0.0.10, 0.0.0.11 ...).
      * The new number is saved to version.txt only after the build, installer, push and GitHub release all succeed,
        so a failed run never uses up a number and no number is ever skipped.

    Steps: checks -> self-contained publish stamped with the version -> installer -> commit version.txt + tag ->
    push -> GitHub release with the installer attached.

.PARAMETER NotesFile
    Markdown file with the release notes (default: docs/release-notes/v<version>.md if present).

.PARAMETER RetryUpload
    Re-uploads the release for the version already in version.txt (use if the final GitHub step failed).

.EXAMPLE
    pwsh tools/release/Release.ps1
#>
param(
    [string]$NotesFile,
    [switch]$RetryUpload
)

$ErrorActionPreference = 'Stop'
$Repo = 'aishsynk/InkIt'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $Root

function Fail([string]$message) { Write-Host "RELEASE STOPPED: $message" -ForegroundColor Red; exit 1 }
function Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }

$current = (Get-Content (Join-Path $Root 'version.txt') -Raw).Trim()
if ($current -notmatch '^0\.0\.0\.(\d+)$') { Fail "version.txt must look like 0.0.0.N (found '$current')." }
$currentNumber = [int]$Matches[1]

function Get-Installer([string]$version) { Join-Path $Root "artifacts\installer\InkIt_Setup_v$version.exe" }

function Publish-GitHubRelease([string]$version, [string]$notes)
{
    $tag = "v$version"
    $installer = Get-Installer $version
    if (-not (Test-Path $installer)) { Fail "Installer not found: $installer" }
    $notesPath = Join-Path ([IO.Path]::GetTempPath()) "inkit-notes-$version.md"
    Set-Content -Path $notesPath -Value $notes -Encoding utf8
    # A version-free copy keeps https://github.com/<repo>/releases/latest/download/InkIt_Setup.exe pointing at the newest release.
    $stable = Join-Path ([IO.Path]::GetTempPath()) 'InkIt_Setup.exe'
    Copy-Item $installer $stable -Force
    if (gh release view $tag --repo $Repo 2>$null) { gh release upload $tag $installer $stable --repo $Repo --clobber }
    else { gh release create $tag $installer $stable --repo $Repo --title "InkIt $version" --notes-file $notesPath --latest }
    if ($LASTEXITCODE -ne 0) { Fail "GitHub release upload failed. Fix the problem, then run: tools/release/Release.ps1 -RetryUpload" }
}

function Get-Notes([string]$version)
{
    $path = if ($NotesFile) { $NotesFile } else { Join-Path $Root "docs\release-notes\v$version.md" }
    $body = if (Test-Path $path) { Get-Content $path -Raw } else { "InkIt $version" }
    return $body + @"


---
**Install:** download ``InkIt_Setup_v$version.exe`` below and run it (no admin rights needed; Windows 10 2004 or later, 64-bit).
Windows may show *"Windows protected your PC"* because the installer is not code-signed yet: click **More info -> Run anyway**.

**Feedback:** tell us what worked and what to improve - see the README for the review link, or open an issue.
"@
}

if ($RetryUpload)
{
    if ($currentNumber -eq 0) { Fail 'Nothing has been released yet.' }
    Step "Re-uploading release v$current"
    Publish-GitHubRelease $current (Get-Notes $current)
    Write-Host "Done: https://github.com/$Repo/releases/tag/v$current" -ForegroundColor Green
    exit 0
}

$next = "0.0.0.$($currentNumber + 1)"
$tag = "v$next"
Step "Releasing InkIt $next (previous: $current)"

# ---------------------------------------------------------------- Checks (nothing is changed before these pass)
if (git status --porcelain) { Fail 'There are uncommitted changes. Commit them first so the release matches the code.' }
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'main') { Fail "Releases are made from 'main' (current branch: $branch)." }
if (git tag -l $tag) { Fail "Tag $tag already exists locally." }
gh auth status *> $null
if ($LASTEXITCODE -ne 0) { Fail 'GitHub CLI is not signed in (run: gh auth login).' }
$latest = (gh release list --repo $Repo --limit 1 --json tagName --jq '.[0].tagName' 2>$null)
if ($currentNumber -eq 0) { if ($latest) { Fail "version.txt says nothing is released, but GitHub already has $latest." } }
elseif ($latest -ne "v$current") { Fail "GitHub's latest release is '$latest' but version.txt says $current. Fix this first so no number is skipped (see -RetryUpload)." }

# ---------------------------------------------------------------- Test
Step 'Running InkIt checks'
dotnet build ScreenCanvas.slnx -c Release -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Fail 'Build failed.' }
dotnet run --project tests/InkIt.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Fail 'Some checks failed; nothing was released.' }

# Optional code signing: set INKIT_SIGN_THUMBPRINT to a code-signing certificate in your Windows certificate store.
function Sign-File([string]$file)
{
    if (-not $env:INKIT_SIGN_THUMBPRINT) { return }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { Fail 'INKIT_SIGN_THUMBPRINT is set but signtool.exe (Windows SDK) was not found.' }
    & $signtool.FullName sign /sha1 $env:INKIT_SIGN_THUMBPRINT /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $file
    if ($LASTEXITCODE -ne 0) { Fail "Signing failed: $file" }
}

# ---------------------------------------------------------------- Build
Step 'Publishing self-contained win-x64 build'
$publish = Join-Path $Root 'artifacts\publish\win-x64'
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
dotnet publish src/ScreenCanvas/ScreenCanvas.csproj -c Release -r win-x64 --self-contained true -o $publish "-p:Version=$next" -nologo
if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish failed.' }
$stamped = (Get-Item (Join-Path $publish 'InkIt.exe')).VersionInfo.FileVersion
if ($stamped -ne $next) { Fail "InkIt.exe carries version '$stamped', expected $next." }
Sign-File (Join-Path $publish 'InkIt.exe')

Step 'Smoke test (renders the UI off-screen, then exits)'
$smoke = Start-Process (Join-Path $publish 'InkIt.exe') -ArgumentList '--qa-capture' -PassThru
if (-not $smoke.WaitForExit(90000) -or $smoke.ExitCode -ne 0) { Fail 'Published InkIt.exe did not start and exit cleanly.' }

Step 'Building installer'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { Fail "Inno Setup 6 not found at $iscc" }
# Compile outside the repo (antivirus can lock files in artifacts\installer mid-compile), then copy in.
$stage = Join-Path ([IO.Path]::GetTempPath()) "inkit-installer-$next"
New-Item -ItemType Directory -Force $stage | Out-Null
& $iscc "/DMyAppVersion=$next" "/O$stage" (Join-Path $Root 'packaging\installer.iss') | Out-Null
if ($LASTEXITCODE -ne 0) { Fail 'Installer compile failed.' }
New-Item -ItemType Directory -Force (Join-Path $Root 'artifacts\installer') | Out-Null
Copy-Item (Join-Path $stage "InkIt_Setup_v$next.exe") (Get-Installer $next) -Force
Sign-File (Get-Installer $next)

# ---------------------------------------------------------------- Record, push, publish
Step "Recording $next"
Set-Content -Path (Join-Path $Root 'version.txt') -Value $next -NoNewline
git add version.txt
git commit -q -m "Release $tag"
git tag -a $tag -m "InkIt $next"
git push -q origin main
if ($LASTEXITCODE -ne 0) { git tag -d $tag | Out-Null; git reset -q --soft HEAD~1; git restore --staged version.txt; git checkout -- version.txt; Fail 'Push failed; version change undone.' }
git push -q origin $tag
if ($LASTEXITCODE -ne 0) { Fail "Tag push failed. Run: git push origin $tag, then tools/release/Release.ps1 -RetryUpload" }

Step 'Creating GitHub release'
Publish-GitHubRelease $next (Get-Notes $next)
Write-Host "Released InkIt ${next}: https://github.com/$Repo/releases/tag/$tag" -ForegroundColor Green
