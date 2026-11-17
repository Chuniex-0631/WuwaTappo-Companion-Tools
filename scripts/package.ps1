param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$NoBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Test }
$version = 'v1.2.1'
$releaseOutput = Join-Path $repoRoot "artifacts\$version"
$stage = Join-Path $releaseOutput ('stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
$checksums = @()
foreach ($appName in @('WuwaTappoToolkit', 'WuwaTappoAutoPilot')) {
    $binary = Join-Path $repoRoot "dist\$appName.exe"
    Copy-Item -LiteralPath $binary -Destination $stage
    Copy-Item -LiteralPath $binary -Destination $releaseOutput -Force
    $checksums += (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash + "  $appName.exe"
}
foreach ($name in @('README.md', 'LICENSE', 'CHANGELOG.md', 'docs', 'assets')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $stage -Recurse
}
$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines((Join-Path $stage 'CHECKSUMS.txt'), $checksums, $encoding)
$archiveName = "WuwaTappo-Companion-Tools-$version.zip"
$archive = Join-Path $releaseOutput $archiveName
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
$checksums += (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash + "  $archiveName"
[System.IO.File]::WriteAllLines((Join-Path $releaseOutput 'CHECKSUMS.txt'), $checksums, $encoding)
[System.IO.File]::WriteAllLines((Join-Path $repoRoot 'CHECKSUMS.txt'), $checksums, $encoding)
Write-Output "Release files: $releaseOutput"
Write-Output $checksums
