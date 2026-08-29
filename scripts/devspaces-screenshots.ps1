param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts/devspaces-screenshots",
    [string]$Upstream = "https://github.com/sourcegit-scm/sourcegit.git"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (Test-Path $Output) {
    Remove-Item $Output -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $Output | Out-Null

if (-not (git remote get-url upstream 2>$null)) {
    git remote add upstream $Upstream
}

git fetch upstream master --depth=1
$diff = git diff --name-only upstream/master...HEAD -- src/Views src/ViewModels src/Models
$diff | Set-Content -Encoding utf8 (Join-Path $Output "fork-devspaces-diff.txt")

$appDir = Join-Path $root "artifacts/devboard-screenshot-app"
if (Test-Path $appDir) {
    Remove-Item $appDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $appDir | Out-Null

dotnet publish src/DevBoard.csproj -c $Configuration -r win-x64 --self-contained false -p:DisableAOT=true -o $appDir
if ($LASTEXITCODE -ne 0) {
    throw "DevBoard publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $appDir "DevBoard.exe"
if (-not (Test-Path $exe -PathType Leaf)) {
    throw "Published DevBoard.exe was not found at $exe"
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class DevBoardScreenshotNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint flags);
}
"@

$process = Start-Process -FilePath $exe -ArgumentList @($root) -WorkingDirectory $appDir -PassThru
$windowProcess = $null
$deadline = [DateTime]::UtcNow.AddSeconds(60)

try {
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500

        if (-not $process.HasExited) {
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                $windowProcess = $process
                break
            }
        }

        $candidate = Get-Process -Name DevBoard -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
            Select-Object -First 1
        if ($candidate) {
            $windowProcess = $candidate
            break
        }
    }

    if (-not $windowProcess) {
        throw "DevBoard did not expose a visible main window within 60 seconds."
    }

    $handle = $windowProcess.MainWindowHandle
    [DevBoardScreenshotNative]::ShowWindow($handle, 9) | Out-Null
    [DevBoardScreenshotNative]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Seconds 3

    $rect = New-Object DevBoardScreenshotNative+RECT
    if (-not [DevBoardScreenshotNative]::GetWindowRect($handle, [ref]$rect)) {
        throw "Unable to read the DevBoard window bounds."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -lt 400 -or $height -lt 300) {
        throw "DevBoard window is unexpectedly small: ${width}x${height}."
    }

    $screenshot = Join-Path $Output "devboard-main.png"
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $hdc = $graphics.GetHdc()
        try {
            $printed = [DevBoardScreenshotNative]::PrintWindow($handle, $hdc, 2)
        }
        finally {
            $graphics.ReleaseHdc($hdc)
        }

        if (-not $printed) {
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        }

        $bitmap.Save($screenshot, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    $file = Get-Item $screenshot
    if ($file.Length -lt 10000) {
        throw "Captured DevBoard screenshot is suspiciously small: $($file.Length) bytes."
    }

    [pscustomobject]@{
        processId = $windowProcess.Id
        windowTitle = $windowProcess.MainWindowTitle
        width = $width
        height = $height
        executable = $exe
    } | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $Output "capture.json")
}
finally {
    Get-Process -Name DevBoard -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

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
<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>DevBoard real-app screenshots</title>
<style>body{font-family:system-ui;margin:24px;background:#111;color:#eee}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:18px}article{background:#1b1b1b;padding:14px;border-radius:12px}img{width:100%;height:auto;border-radius:8px;border:1px solid #333}h1,h2{margin-top:0}</style></head><body><h1>DevBoard real-app screenshots</h1><main>$cards</main></body></html>
"@
$html | Set-Content -Encoding utf8 (Join-Path $Output "index.html")

if ($pngs.Count -eq 0) { throw "No real DevBoard screenshots were generated." }
Write-Host "Captured $($pngs.Count) real DevBoard screenshot(s) in $Output"
