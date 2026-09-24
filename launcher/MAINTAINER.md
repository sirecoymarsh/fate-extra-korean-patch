# Launcher maintenance

Build the Windows x64 .NET Framework launcher with `powershell -File launcher/build.ps1`. Package the EXE with the `tools` directory and licenses from the published launcher ZIP. No disc image or emulator is bundled.

For each new patch release, publish all base/HD archives and a `launcher-update.json` matching this example. Use each uploaded GitHub asset's exact name, byte count and SHA-256 digest; bind the original ISO and output ISO and the installed HD `textures.ini` digest. Keep original-root names and the manifest structure used by the installer. Create/upload as a draft and publish only after all files and metadata are complete. The launcher reads the latest non-prerelease release.

The original ISO remains immutable. Updating the patch regenerates a versioned ISO. HD, cheats, saves and PPSSPP config changes use staged replacements, per-item backups and an interrupted-install journal. GitHub startup checks do not download patch archives or apply changes. A new minimum launcher version asks the user to update the launcher ZIP manually.

Verified for 1.0.0: real base+HD install from release archives; separate extras installation; unchecked save/cheat preservation; interrupted swap recovery; existing-HD compatibility; game-specific HD settings; direct PSP-folder routing; HTTPS resume, corruption and cancellation; invalid archive paths; real GUI startup notification without installation. PPSSPP boot/game identification was observed. This session could not obtain a usable new GPU/window game capture; the installed ISO matches the separately visual-tested v8e image. Hardware PSP is untested.

Runtime test harnesses use private user-owned ISO/emulator paths and are kept outside this public source tree. `launcher-update.example.json` describes a real public release and contains no local input paths.

## 1.1.0 path discovery

Read-only bounded discovery probes ISO9660 PARAM.SFO and source size, PPSSPP PE header/assets, installed.txt, and Recent paths. It preserves explicit paths and requires choice for ambiguity. Fixed/removable drives only; UNC/mapped network drives and parent reparse points are excluded. Test `DiscoveryTests.cs` with arguments: writable test folder, original ISO path, patched ISO path, optional junction fixture. References: compiled launcher EXE, System.Web.Extensions, System.Net.Http, System.Windows.Forms, System.Drawing. Startup discovery and latest-release checks never install components.

The installer Core.cs only changes its version constant relative to 1.0.0, retaining the prior integration proof. 1.1.0 has 19 discovery assertions and current unit/network/GUI checks. The release is Authenticode-unsigned. SmartScreen trust requires a trusted signing identity and may still require reputation; no certificate enrollment or Windows security configuration changes are automated.

## 1.2.0 deep PPSSPP and memory-stick discovery

The 1.1.0 common-location scan missed the actual user's deeply nested portable installation. If common scanning finds no executable or no memory-stick candidate, 1.2.0 performs bounded breadth-first traversal across local drives (45 seconds, 50,000 directories, depth 32), probing executable/config names. Standalone PSP/SYSTEM/ppsspp.ini profiles are recognized. Heavy asset/save/cache directories, network paths and reparse points are skipped. Folder-specific scanning supports depth 16 and 20,000 directories.

Editable dropdowns expose all candidates. Selecting an executable pairs its memory stick until the user explicitly selects/types a profile. Existing installation paths are retained. Same-directory 32/64-bit duplicates display only 64-bit while preserving the mapping for an explicitly configured 32-bit executable. Candidate validation now requires PE signature as well as MZ and assets.

Validation: 23 discovery assertions, 26 unit assertions, 8 public-feed/download assertions, 9 GUI assertions. A generic drive scan and the actual startup UI both found the user's real portable PPSSPP and paired memory stick; discovery made no install/download and left the player's config untouched. Core.cs only changes Version, retaining the full 1.0.0 installer integration proof. DeviceGuiTests.cs takes work folder, expected executable and expected memory-stick paths; LiveDiscoveryTests.cs takes work folder and expected executable. Expected paths are assertions, never search roots. The GUI test requires a fresh test work folder/settings and multiple candidates to test explicit selection preservation.

## 1.2.1 different-drive reproduction and Windows execution hints

The 1.2.0 live test was too close to the emulator tree. Reproduction using the user's Downloads launcher location on a different drive returned zero candidates after hitting the 50,000-directory budget, in about 13 seconds. This supersedes the earlier broad claim of actual-use discovery coverage.

Before traversing folders, 1.2.1 reads only path names for PPSSPP from current-user Compatibility Assistant Store and MuiCache. Known metadata suffixes are stripped; candidates must still pass the existing local path, reparse, PE and assets checks. Stale records are ignored. Registry values are neither read nor changed, and paths are not sent to a service. No user-specific directory is built into the product.

The same unrelated Downloads root now found the actual emulator and memory stick in about 3.5 seconds without hitting scan limits. A GUI test in a separate Downloads sibling folder verified visible candidates, profile pairing, explicit selection preservation and no install/download. 27 discovery, 9 GUI, 26 unit and 8 network assertions passed. Core.cs remains unchanged except Version. RootDiscoveryProbe.cs arguments are launcher root (read-only), expected executable (assertion only), output JSON. DeviceGuiTests.cs accepts an optional fourth argument for the test launcher root, so this regression need not run next to the emulator tree.

## 1.3.0 discovery follows component selection

Base-only installation searches only for an original ISO and disables emulator/profile fields. Device/history/profile probing is excluded at the Discovery layer, including explicit hints and fallback drive scans. Selecting cheats or clear save enables device fields and triggers discovery when the profile is missing; extras-only discovery skips ISO and Recent game probing. Deselecting extras disables irrelevant fields without erasing paths. HD still needs an explicit memory-stick destination for textures, but no PPSSPP executable or automatic device scan. A supplied profile suffices for extras installation without an emulator executable.

The separate Game Run action can request emulator/profile paths when needed. This does not add prerequisites to patch generation. Disabled fields retain existing settings, and the installation core is unchanged except Version.

Validation: 30 discovery assertions, 17 selection GUI assertions, 9 existing device GUI assertions, 26 unit and 8 network assertions. Eight new installation assertions generated the real patched ISO with empty emulator/memory-stick settings, then installed 19 disabled cheats and the clear save into a separate profile with empty source/emulator settings. Missing profile targets fail for HD/cheats/save. Existing user profiles were untouched. SelectionGuiTests uses a Downloads test root, and restores the WinForms synchronization context after pumping events to match real checkbox callbacks. SelectionInstallTests arguments: work folder with build/tools and launcher-update.json, original ISO, cached base ZIP. Use fresh test destinations.

## 1.4.0 optional UI and per-game defaults

UI-v1 contains both reviewed maps and all 651 changed-map PNGs; 750 mappings / 138 assets. HD-v39 remains the original-English baseline. UI installation requires HD; unknown custom maps fail before live changes. UI removal restores the exact v39 map and required original PNGs, including migration from the integrated v40 package. Config.UiVersion records explicit opt-in. HD updates preserve it; unchecked install components do not uninstall. All texture/config writes use the existing journal and backups.

Per-game NPJH50247_ppsspp.ini uses the official PPSSPP 1.20.4 keys InternalResolution=8, MultiSampleLevel=2 (1<<2 = 4 samples), VerticalSync=True, ReplaceTextures=True, General.EnableCheats=True only when installing cheats or configuring a profile with a cheat file. Global settings and control bindings seed a newly created per-game INI; existing per-game values outside the requested keys are preserved. Global files, audio and individual code toggles are unchanged. Graphics defaults are set at HD/UI installation, once on first launch, or via explicit button. Later launches respect manual adjustment. Engine.RunningCheck is an internal process guard override used only in isolated file integration tests, avoiding disturbance of a live user game; production defaults to real PPSSPP process detection.

Build includes UiPack.cs. OptionalUiTests uses actual release archives and tests both UI states, legacy migration, standalone changes, combined HD/UI, opted-in update retention, rollback after a partial swap, wrong-profile/unknown-map rejection, cancellation, cheat defaults and per-game settings. No Computer Use or player profile mutation is used. PPSSPP official sources: https://github.com/hrydgard/ppsspp/blob/v1.20.4/Core/Config.cpp and https://github.com/hrydgard/ppsspp/blob/v1.20.4/UI/GameSettingsScreen.cpp .
