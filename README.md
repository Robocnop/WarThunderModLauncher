<div align="center">

# WT Mod Launcher

**A War Thunder–styled Windows launcher that installs and configures community mods in one click.**

[![Latest release](https://img.shields.io/github/v/release/Robocnop/WarThunderModLauncher?include_prereleases&label=release&color=f2a531)](https://github.com/Robocnop/WarThunderModLauncher/releases)
[![Build](https://img.shields.io/github/actions/workflow/status/Robocnop/WarThunderModLauncher/release.yml?label=build)](https://github.com/Robocnop/WarThunderModLauncher/actions/workflows/release.yml)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0d1013)
![.NET](https://img.shields.io/badge/.NET-8-512bd4)

[**Download**](https://github.com/Robocnop/WarThunderModLauncher/releases) · [Roadmap](ROADMAP.md) · [Report a bug](https://github.com/Robocnop/WarThunderModLauncher/issues)

</div>

---

## Overview

WT Mod Launcher lets players pick the mods they want with checkboxes and takes care of the rest: download, integrity check, installation, game configuration, updates and uninstallation. The interface is available in **English and French** (follows the Windows language by default).

| Mod | Author | What the launcher does |
|---|---|---|
| **RCSM — Realistic Sounds** | FireXsoldier | Installs the FMOD `.bank` files into `<game>\sound\mod` and turns on `sound{ enable_mod:b=yes }` in `config.blk` (a backup is kept). |
| **FCS — Ballistic Sights** | [tsvl & Assin127](https://github.com/tsvl/WT-FCSGenerator) | Downloads the latest official WT-FCSGenerator release, launches it and opens your `UserSights` folder. |
| **Robo's Config — Controls** | Robocnop | Drops the controls preset into `<game>\ModLauncher\controls`, ready to import in game (*Controls → Import*). Tuned for FCS box-rangefinder sights. |

### Features

- 🎯 Automatic War Thunder detection (Steam libraries, Gaijin launcher, common paths)
- 🔎 **Detects mods you already have**: RCSM banks in `sound\mod`, an FCS folder you unzipped yourself, or Robo's controls already imported in your profile — no need to reinstall
- ⬆️ **Self-updating**: a banner offers each new launcher version; one click downloads it, checks its SHA-256 and restarts
- 🔒 SHA-256 verification and resumable downloads
- 🛠️ **Check & repair** — re-enables the sound mod after a game update resets `config.blk`
- ♻️ Every change is reversible from the launcher
- 📦 Content catalog served from GitHub releases: mods can be updated without shipping a new app
- 🛡️ Never touches protected game files (`.vromfs`, executables) — only `sound\mod`, the `enable_mod` switch and your personal folders

## Download & install

1. Go to the [**Releases**](https://github.com/Robocnop/WarThunderModLauncher/releases) page and download from the latest `v*` release:
   - **`WTModLauncher-Setup-x.y.z.exe`** (recommended) — installs for your user only, no admin rights: Start menu entry, optional desktop shortcut, uninstall from *Settings → Apps*.
   - or **`WTModLauncher.exe`** — portable single file, runs from anywhere.
   > Windows SmartScreen may show *Unknown publisher* because the exe is not code-signed yet: click **More info → Run anyway**.
2. Tick the mods you want and press **INSTALL**. Close War Thunder before installing the sound mod.
3. Later versions install themselves: click **UPDATE NOW** on the banner when one is out (*Settings → Launcher updates* to check manually or opt in to pre-releases).

Uninstalling the launcher leaves your mods in place and keeps its data in `%LOCALAPPDATA%\WTModLauncher`; remove mods from the launcher first if you want them gone.

> **Note:** current builds are **pre-releases** (`v0.x`).

### Releases

| Release | Contents |
|---|---|
| [`v*`](https://github.com/Robocnop/WarThunderModLauncher/releases) | The launcher — installer (`WTModLauncher-Setup-x.y.z.exe`), portable exe (`WTModLauncher.exe`) and `SHA256SUMS.txt` — built and published by CI. |
| [`content-*`](https://github.com/Robocnop/WarThunderModLauncher/releases/tag/content-2026.10) | Mod files + `manifest.json` downloaded by the launcher. You don't need to download these manually. |

## Requirements

- Windows 10 or 11 (x64)
- War Thunder (Steam or Gaijin launcher)
- Internet connection for downloads

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/Robocnop/WarThunderModLauncher.git
cd WarThunderModLauncher

dotnet test WTModLauncher.slnx                              # unit tests
dotnet run --project src/WTModLauncher                      # run in debug
dotnet publish src/WTModLauncher -c Release -o publish      # single-file exe in ./publish
iscc installer\WTModLauncher.iss /DAppVersion=0.2.0          # installer in ./publish (Inno Setup 6)
```

### Project layout

```
src/WTModLauncher/
├─ Core/                  game detection, BLK editor, downloader, installers, detection of existing mods,
│                         self-updater, catalog, localization
├─ ViewModels/            MVVM (CommunityToolkit.Mvvm)
├─ Themes/WarThunder.xaml colors, fonts and control styles
├─ default-manifest.json  built-in catalog (fallback when offline)
installer/WTModLauncher.iss Inno Setup script (per-user install)
tests/WTModLauncher.Tests/ xUnit tests
tools/publish-content.ps1  publishes a content-* release
```

## Publishing

**App release** — push a tag; CI tests, builds and attaches the installer, the portable exe and `SHA256SUMS.txt`. The tag sets the version (`v0.3.0-beta.1` works too); `v0.x` and suffixed tags are published as pre-releases:

```powershell
git tag v0.2.0
git push origin v0.2.0
```

Running launchers see the new version at their next start.

**Content release** — update `src/WTModLauncher/default-manifest.json` (version, url, `sha256`, `size`, and for a sound mod the `files` bank list the script prints if it is out of date), then:

```powershell
./tools/publish-content.ps1 -Tag content-2026.11 -Prerelease -Files <mod.zip>, <preset.blk>
```

The script refuses to publish if a file does not match the manifest. Add `-ManifestOnly` to re-upload only `manifest.json`. Launchers pick up the newest `content-*` release automatically.

## Roadmap

See [ROADMAP.md](ROADMAP.md).

## Credits

- **RCSM** — FireXsoldier ([War Thunder Live](https://live.warthunder.com/post/1089792/en/))
- **WT-FCSGenerator** — [tsvl](https://github.com/tsvl) & Assin127 — always downloaded from the authors' releases, never re-hosted
- **Rajdhani** font — Indian Type Foundry, SIL Open Font License 1.1

## Disclaimer

This is a fan project and is not affiliated with or endorsed by Gaijin Entertainment. War Thunder is a trademark of Gaijin Entertainment. Mods are the property of their respective authors.
