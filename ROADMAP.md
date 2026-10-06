# Roadmap

Planned work for WT Mod Launcher. Items are roughly in priority order.

## Next

### Code signing
Sign the installer and the exe to remove the SmartScreen *Unknown publisher* warning (needs a code-signing certificate, e.g. Azure Trusted Signing or SignPath for open source), then sign in CI before the checksums step.

### Apply the controls preset for the player
Today the preset is dropped next to the game and imported by hand (*Controls → Import*). Investigate writing it into the profile's `machine.blk` directly (with a backup), and how it interacts with Gaijin's cloud sync of controls.

## Open questions

- Confirm which folder War Thunder's **Controls → Import** dialog opens by default, and drop the preset there (currently `<game>\ModLauncher\controls`).
- Get explicit redistribution permission from the RCSM author (FireXsoldier).

## Known limitations

- The self-updater compares `major.minor.patch` only: `v0.3.0-beta.1` → `v0.3.0` is not offered as an update. Bump the patch number for the final release, or extend the comparison to pre-release suffixes.
- An FCS copy is recognised by `FCS.exe` in the Desktop, Downloads, Documents, `%LOCALAPPDATA%\Programs`, `<drive>:\Games` or `<drive>:\Tools` folders (3 levels deep), and its version is read from the folder name (`WT-FCSGenerator-v2.0.2-win-x64`). Elsewhere, or renamed, it shows as an update.

## Done

### v0.2.0
- **Windows installer (Inno Setup)** — `WTModLauncher-Setup-x.y.z.exe`: per-user install in `%LOCALAPPDATA%\Programs\WTModLauncher` (no admin rights), Start menu + optional desktop shortcut, *Apps & features* entry, clean uninstaller. Built by CI and attached to every `v*` release next to the portable exe and `SHA256SUMS.txt`.
- **Detection of mods installed outside the launcher** — adopted into `state.json` so update and uninstall work as usual:
  - **RCSM**: banks in `sound\mod` matched by name and size against the `files` list of the manifest (exact match → installed, otherwise → update offered).
  - **FCS**: an existing `FCS.exe` found in common folders; uninstalling it from the launcher never deletes a folder the launcher did not create.
  - **Controls preset**: recognised when its key and axis bindings are the active controls of a War Thunder profile (`Saves\<id>\production\machine.blk`); the card then says the config is active.
- **Self-updater** — checks the `v*` releases at startup; one click downloads the installer (installed copy) or the exe (portable copy), verifies its SHA-256 (GitHub asset digest, or `SHA256SUMS.txt`), replaces the app and relaunches. Pre-releases are opt-in in Settings (on by default when running a pre-release build).
- **Maintenance** — GitHub Actions moved to Node 24 versions (`checkout@v7`, `setup-dotnet@v6`, `upload-artifact@v7`); the tag sets the app version.

### v0.1.0
- First pre-release: WPF launcher (FR/EN), RCSM, FCS and Robo's controls, repair, `content-*` catalog releases.
