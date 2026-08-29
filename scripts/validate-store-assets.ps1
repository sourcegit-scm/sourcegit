$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$assets = @(
  'store\assets\Square44x44Logo.png',
  'store\assets\Square150x150Logo.png',
  'store\assets\StoreLogo.png'
)
foreach ($relativePath in $assets) {
  $path = Join-Path $repoRoot $relativePath
  if (-not (Test-Path $path -PathType Leaf)) { throw "Store asset missing: $path" }
  if ((Get-Item $path).Length -le 0) { throw "Store asset is empty: $path" }
}
Write-Host 'Dev Board Store assets are present and non-empty.'
