# BH! Editor v1.0

A Windows x64 level editor for Bound High! on Nintendo Virtual Boy by Levine91

Edit Adventure of Chalvo, Score Attack!!, and Pocket and Cushion stages,
save your work as a project, and export a separate patched ROM.

## Features / What Can Be Modified

- All 4 Adventure Worlds: 45 editable non-boss Stages.
- 20 Score Attack!! Stages
- 20 Pocket and Cushion Stages.
- Terrain, enemies, electric variants, visible powerups, hidden powerups, ball placement, all can be edited.
- Change stage's background layers and/or BGM.
- Stage rules, score/time targets, and supported Wind/Gobbler settings.
- Built-in audio player for BGM & Sound Effect playback
- Ability to locally dump sprites and/or audio files from the ROM.
- Optional Enhancements: 
--- Add Boss Rush Mode
--- Stage Number in HUD
--- SRAM Support
--- Debug Restoration

Proto 2 keeps its own engine. Exports stay **2,048 KB**, with capacity checks
for ROM space, graphics and enemy slots. Display colors use black plus
`#690000`, `#A20000`, and `#FA0000`, matching the verified Lemur RGB mapping.

<img width="1110" height="761" alt="pic02" src="https://github.com/user-attachments/assets/175c60c1-f20f-45dd-b3ac-644dbc5ce191" />


## Getting started

1. Download BH-Editor-v1.0-win64.zip from this repository's Releases page.
2. Extract the entire ZIP and run BH-Editor.exe (Keep BoundHighAudio.dll beside it.)
3. Choose ROM > Load ROM and select your supported original ROM.
4. Make your edits, then File > Save Project and ROM > Export ROM.

Requires Windows x64 with .NET Framework 4.x. No compiler, Python, Ghidra,
or external emulator is required to run the release. You must provide your
own ROM; game ROMs, save files, music and graphics dumps are not included.

Both the Prototype 2 ROM and Thunderstruck's ROM with Activated Debug Features are both supported.

At startup, the editor looks beside the executable and in its parent folder
for `Bound High (World) (Proto 2).vb`, then `BH.Debug.vb`.
The file names and SHA-1 MUST match if you wish for the Editor to load the ROM automatically.
Alternately, you cab manually load the ROM as well if you wish to keep it in another location.
File name does not matter going this route, only the matching SHA-1 (see below)

## Supported ROMs

| Original ROM | SHA-1 |
| --- | --- |
| BH.Debug.vb | `05548676fd332e4898f8779b952b1244ff2103c2` |
| Bound High (World) (Proto 2).vb | `c9d4181ed56e2520ae42a74afaeaff9902180f86` |

Renaming a ROM does not change its identity. Modified/exported ROMs are not
accepted as source ROMs. Keep your original ROM and project file.
Projects use schema 18 and store the source SHA-1.
Schema numbers are internal, not release versions.

SRAM stores Adventure password progress and Score Attack/Pocket progress,
high scores and unlocks; it does not save Boss Rush progress or arbitrary
in-stage states. Your cartridge/emulator must support persistent SRAM.
To erase SRAM, hold L + R + Left D-pad Down + Right D-pad Down on the title screen.

Editor and emulator checks have been performed. **Physical Virtual Boy testing
remains pending**; emulator results do not establish real-hardware compatibility.

Please submit any issues, suggestions, etc. to the official GitHub

## Building

Install Visual Studio 2022 Build Tools with the C++ x64 tools and Windows SDK.
Use Windows PowerShell from the repository root:

```powershell
.\build.ps1
.\tools\package.ps1
```

The build uses the Windows .NET Framework compiler and the vendored emulator
core. No ROM is needed to build. Outputs are `dist/BH-Editor.exe`,
`dist/BoundHighAudio.dll`, and `dist/BH-Editor-v1.0-win64.zip`.
GitHub Actions runs the same build and uploads the ZIP as a build artifact;
it does not automatically publish a release.

Optional ROM-dependent checks (provide your own supported original):

```powershell
.\dist\BH-Editor.exe --self-test "path-to-original.vb" "work\checks"
.\dist\BH-Editor.exe --audio-test "path-to-original.vb" "work\audio-checks"
```

See [release notes](CHANGELOG.md), [third-party notices](THIRD-PARTY-NOTICES.md),
and [release preparation](docs/RELEASING.md). No project-wide open-source
license has been selected yet; third-party components retain their own terms.

## Credits

- **[vaguerant / vb-patches](https://github.com/vaguerant/vb-patches)**:
  Bound High debug-restoration and save-support patches, both ASM and IPS.
- **Guy Perfect and contributors**: Shrooms Virtual Boy core used for audio.

BH! Editor is a custom C# Windows Forms application. Tiled was downloaded
and evaluated during initial research, but no Tiled code, libraries or runtime
components are used or distributed with this editor.
