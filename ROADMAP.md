# Roadmap

Planned work for WT Mod Launcher. Items are roughly in priority order.

## Next

### 1. Windows installer (Inno Setup)
Replace the portable single-file exe with a proper installer.

- `WTModLauncher-Setup-x.y.z.exe` built with Inno Setup: install to `%LOCALAPPDATA%\Programs\WTModLauncher` (no admin rights needed), Start menu + optional desktop shortcut, entry in *Apps & features*, clean uninstaller.
- Build the installer in CI (`.github/workflows/release.yml`) and attach it to every `v*` release, next to the portable exe.

### 2. Detect mods installed outside the launcher
Today an item's status only comes from the launcher's own `state.json`, so FCS and the controls preset show **Not installed** even when the player already has them.

- **FCS**: look for an existing `FCS.exe` (launcher tools folder, common download locations) and read its version.
- **Controls preset**: detect that Robo's preset is already present/imported.
- **RCSM**: if `sound\mod` already contains the mod's `.bank` files (hash-match against the release zip) and `enable_mod` is on, adopt it as installed.
- Adopted items are written to `state.json` so update/uninstall work as for launcher-installed items.

### 3. Self-updater
- On startup, check the latest `v*` release on GitHub; if newer than the running version, show an update banner.
- One click: download the new installer/exe, verify its SHA-256, replace the app and relaunch.
- Respect pre-releases (opt-in channel in Settings).

## Open questions

- Confirm which folder War Thunder's **Controls → Import** dialog opens by default, and drop the preset there (currently `<game>\ModLauncher\controls`).
- Get explicit redistribution permission from the RCSM author (FireXsoldier).

## Maintenance

- GitHub Actions: move `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact` to versions running on Node 24.
- Code-sign the executable to remove the SmartScreen "Unknown publisher" warning.
