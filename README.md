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
- 🔒 SHA-256 verification and resumable downloads
- 🛠️ **Check & repair** — re-enables the sound mod after a game update resets `config.blk`
- ♻️ Every change is reversible from the launcher
- 📦 Content catalog served from GitHub releases: mods can be updated without shipping a new app
- 🛡️ Never touches protected game files (`.vromfs`, executables) — only `sound\mod`, the `enable_mod` switch and your personal folders

## Download & install

1. Go to the [**Releases**](https://github.com/Robocnop/WarThunderModLauncher/releases) page and download **`WTModLauncher.exe`** from the latest `v*` release.
2. Run it — no installer or runtime required (self-contained, single file).
   > Windows SmartScreen may show *Unknown publisher* because the exe is not code-signed yet: click **More info → Run anyway**.
3. Tick the mods you want and press **INSTALL**. Close War Thunder before installing the sound mod.

> **Note:** current builds are **pre-releases**. A Windows installer and a self-updater are planned — see the [roadmap](ROADMAP.md).

### Releases

| Release | Contents |
|---|---|
| [`v*`](https://github.com/Robocnop/WarThunderModLauncher/releases) | The launcher (`WTModLauncher.exe`), built and published by CI. |
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
```

### Project layout

```
src/WTModLauncher/
├─ Core/                  game detection, BLK editor, downloader, installers, catalog, localization
├─ ViewModels/            MVVM (CommunityToolkit.Mvvm)
├─ Themes/WarThunder.xaml colors, fonts and control styles
├─ default-manifest.json  built-in catalog (fallback when offline)
tests/WTModLauncher.Tests/ xUnit tests
tools/publish-content.ps1  publishes a content-* release
```

## Publishing

**App release** — push a tag; CI tests, builds and attaches the exe:

```powershell
git tag v0.2.0
git push origin v0.2.0
```

**Content release** — update `src/WTModLauncher/default-manifest.json` (version, url, `sha256`, `size`), then:

```powershell
./tools/publish-content.ps1 -Tag content-2026.11 -Prerelease -Files <mod.zip>, <preset.blk>
```

The script refuses to publish if a file does not match the manifest. Launchers pick up the newest `content-*` release automatically.

## Roadmap

See [ROADMAP.md](ROADMAP.md) — next up: Inno Setup installer, detection of mods installed outside the launcher, and a GitHub-based self-updater.

## Credits

- **RCSM** — FireXsoldier ([War Thunder Live](https://live.warthunder.com/post/1089792/en/))
- **WT-FCSGenerator** — [tsvl](https://github.com/tsvl) & Assin127 — always downloaded from the authors' releases, never re-hosted
- **Rajdhani** font — Indian Type Foundry, SIL Open Font License 1.1

## Disclaimer

This is a fan project and is not affiliated with or endorsed by Gaijin Entertainment. War Thunder is a trademark of Gaijin Entertainment. Mods are the property of their respective authors.
