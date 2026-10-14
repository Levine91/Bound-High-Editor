$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root 'dist'
$stage = Join-Path $root 'work/release-v1.0'
if (Test-Path -LiteralPath $stage) {
    $stage = Join-Path $root ('work/release-v1.0-' + [Guid]::NewGuid().ToString('N'))
}
New-Item -ItemType Directory -Path $stage | Out-Null
foreach ($name in @('BH-Editor.exe','BoundHighAudio.dll')) {
    $source = Join-Path $dist $name
    if (!(Test-Path -LiteralPath $source)) { throw "Missing build output: $name. Run build.ps1 first." }
    Copy-Item -LiteralPath $source -Destination $stage
}
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'BH-Editor.exe'))
if ($version.FileVersion -ne '1.0.0.0') { throw 'Release executable must be v1.0.' }
foreach ($name in @('README.md','CHANGELOG.md','THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage
}
New-Item -ItemType Directory -Path (Join-Path $stage 'licenses') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'third-party/shrooms-vb-core/NOTICE.txt') -Destination (Join-Path $stage 'licenses/Shrooms-NOTICE.txt')
Copy-Item -LiteralPath (Join-Path $root 'third-party/shrooms-vb-core/ZLIB.txt') -Destination (Join-Path $stage 'licenses/Shrooms-ZLIB.txt')
Copy-Item -LiteralPath (Join-Path $root 'third-party/vb-patches/LICENSE') -Destination (Join-Path $stage 'licenses/vb-patches-MIT.txt')
$zip = Join-Path $dist 'BH-Editor-v1.0-win64.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($zip + '.sha256', "$hash  BH-Editor-v1.0-win64.zip`r`n")
Write-Output "Packaged $zip"
