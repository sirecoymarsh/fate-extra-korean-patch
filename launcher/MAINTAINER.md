# Launcher maintenance

Build the Windows x64 .NET Framework launcher with `powershell -File launcher/build.ps1`. Package the EXE with the `tools` directory and licenses from the published launcher ZIP. No disc image or emulator is bundled.

For each new patch release, publish all base/HD archives and a `launcher-update.json` matching this example. Use each uploaded GitHub asset's exact name, byte count and SHA-256 digest; bind the original ISO and output ISO and the installed HD `textures.ini` digest. Keep original-root names and the manifest structure used by the installer. Create/upload as a draft and publish only after all files and metadata are complete. The launcher reads the latest non-prerelease release.

The original ISO remains immutable. Updating the patch regenerates a versioned ISO. HD, cheats, saves and PPSSPP config changes use staged replacements, per-item backups and an interrupted-install journal. GitHub startup checks do not download patch archives or apply changes. A new minimum launcher version asks the user to update the launcher ZIP manually.

Verified for 1.0.0: real base+HD install from release archives; separate extras installation; unchecked save/cheat preservation; interrupted swap recovery; existing-HD compatibility; game-specific HD settings; direct PSP-folder routing; HTTPS resume, corruption and cancellation; invalid archive paths; real GUI startup notification without installation. PPSSPP boot/game identification was observed. This session could not obtain a usable new GPU/window game capture; the installed ISO matches the separately visual-tested v8e image. Hardware PSP is untested.

Runtime test harnesses use private user-owned ISO/emulator paths and are kept outside this public source tree. `launcher-update.example.json` describes a real public release and contains no local input paths.
