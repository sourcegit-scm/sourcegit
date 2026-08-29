param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts/devspaces-screenshots",
    [string]$Upstream = "https://github.com/sourcegit-scm/sourcegit.git"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force -Path $Output | Out-Null

if (-not (git remote get-url upstream 2>$null)) {
    git remote add upstream $Upstream
}

git fetch upstream master --depth=1
$diff = git diff --name-only upstream/master...HEAD -- src/Views src/ViewModels src/Models
$diff | Set-Content -Encoding utf8 (Join-Path $Output "fork-devspaces-diff.txt")

$env:SOURCEGIT_SCREENSHOT_OUTPUT = (Resolve-Path $Output).Path
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj -c $Configuration -p:DisableAOT=true --filter "FullyQualifiedName~DevSpacesScreenshot"

$pngs = Get-ChildItem -Path $Output -Recurse -Filter *.png | Sort-Object FullName
$manifest = $pngs | ForEach-Object {
    [pscustomobject]@{
        id = $_.BaseName
        path = $_.FullName.Substring((Resolve-Path $Output).Path.Length + 1).Replace('\\','/')
    }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $Output "manifest.json")

$cards = ($manifest | ForEach-Object {
    $safeId = [System.Net.WebUtility]::HtmlEncode($_.id)
    $safePath = [System.Net.WebUtility]::HtmlEncode($_.path)
    "<article><h2>$safeId</h2><a href='$safePath'><img loading='lazy' src='$safePath' alt='$safeId'></a></article>"
}) -join "`n"

$html = @"
<!doctype html>
<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>DevSpaces screenshots</title>
<style>body{font-family:system-ui;margin:24px;background:#111;color:#eee}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:18px}article{background:#1b1b1b;padding:14px;border-radius:12px}img{width:100%;height:auto;border-radius:8px;border:1px solid #333}h1,h2{margin-top:0}</style></head><body><h1>DevSpaces fork screenshots</h1><main>$cards</main></body></html>
"@
$html | Set-Content -Encoding utf8 (Join-Path $Output "index.html")

if ($pngs.Count -eq 0) { throw "No DevSpaces screenshots were generated." }
Write-Host "Generated $($pngs.Count) screenshots in $Output"
