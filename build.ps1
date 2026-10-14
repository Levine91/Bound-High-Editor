param([string]$OutputName = 'BH-Editor.exe')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Path dist -Force | Out-Null
& ./tools/build-audio.ps1
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /win32icon:src\BoundHighIcon.ico "/out:dist\$OutputName" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Core.dll /resource:src\stages.json,stages.json /resource:src\enemies.json,enemies.json /resource:src\score.json,score.json /resource:src\pocket.json,pocket.json /resource:src\pocket-autotiles.json,pocket-autotiles.json /resource:Bound-High_VB_Debug-Restoration_v1.0.ips,bound-debug.ips /resource:Bound-High_VB_Save-Support_v1.0.ips,bound-save.ips src\AssemblyInfo.cs src\Editor.cs src\SramPatch.cs src\BundledPatch.cs src\EnemyPatch.cs src\BossRushPatch.cs src\StageHudPatch.cs src\Proto2.cs src\Proto2Patch.cs src\Proto2BossRush.cs src\SelfTest.cs src\Audio.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
New-Item -ItemType Directory -Force dist/licenses | Out-Null
Copy-Item third-party/shrooms-vb-core/NOTICE.txt dist/licenses/Shrooms-NOTICE.txt -Force
Copy-Item third-party/shrooms-vb-core/ZLIB.txt dist/licenses/Shrooms-ZLIB.txt -Force
Write-Output "Built dist/$OutputName (Windows x64, .NET Framework 4.x)."
