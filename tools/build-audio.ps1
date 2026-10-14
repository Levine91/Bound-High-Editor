$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vsRoot = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vsRoot) { throw 'Visual Studio C++ x64 build tools are required to rebuild the audio component.' }
$setup = Join-Path $vsRoot 'VC/Auxiliary/Build/vcvars64.bat'
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Force work/audio-build,dist | Out-Null
    @('@echo off', ('call "' + $setup + '" >nul'), 'if errorlevel 1 exit /b 1', 'cl /nologo /O2 /MT /LD /DVB_LITTLE_ENDIAN /DVB_SIGNED_PROPAGATE /DVB_DIV_GENERIC /Ithird-party\shrooms-vb-core\core src\AudioNative.c third-party\shrooms-vb-core\core\vb.c /Fo:work\audio-build\ /Fe:dist\BoundHighAudio.dll /link /IMPLIB:work\audio-build\BoundHighAudio.lib') | Set-Content work/audio-build/build.cmd -Encoding ascii
    & "$env:WINDIR/System32/cmd.exe" /d /c work\audio-build\build.cmd
    if ($LASTEXITCODE -ne 0) { throw 'Audio component build failed.' }
} finally { Pop-Location }
