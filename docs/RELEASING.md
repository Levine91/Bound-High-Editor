# Preparing the first GitHub release

Public version: **v1.0**. Tag: **v1.0**. Release title: **BH! Editor v1.0**.
Executable: `BH-Editor.exe`. Asset: `BH-Editor-v1.0-win64.zip`.
Assembly/file version: `1.0.0.0`; window title: `BH! Editor v1.0`.

1. Confirm the desired GitHub owner/repository and visibility.
2. Select the editor's license. Preserve the third-party credits and bundled
   license notices, including vaguerant's MIT notice.
3. Build and package with `build.ps1` and `tools/package.ps1`.
4. Review the source file list. Do not force-add ignored local files.
5. Push the prepared repository, create tag `v1.0`, and publish a release with
   the ZIP and its SHA-256 checksum from `dist/`. Use CHANGELOG.md for the notes.

No remote repository or public release is created by the build. GitHub Actions
provides a ROM-free build artifact. Runtime checks require your private ROM.

The release source allowlist excludes local ROMs, saved projects, research,
asset dumps, archived development builds, emulator/Ghidra/Tiled installations,
agent instructions and local configuration. The workspace's original inputs
remain local. Never publish `work/`, `archive/`, `Graphics/` or `Audio/`.
