# WT Mod Launcher

Launcher Windows au look War Thunder pour installer et configurer en quelques clics :

| Mod | Ce que fait le launcher |
|---|---|
| **RCSM — Sons réalistes** (FireXsoldier) | Copie les `.bank` dans `<jeu>\sound\mod` et active `sound{ enable_mod:b=yes }` dans `config.blk` (sauvegarde `config.blk.modlauncher.bak`). |
| **FCS — Viseurs balistiques** ([tsvl/WT-FCSGenerator](https://github.com/tsvl/WT-FCSGenerator)) | Télécharge la dernière release officielle, la lance, ouvre ton dossier `UserSights`. |
| **Config Robo — Contrôles** | Dépose le preset dans `<jeu>\ModLauncher\controls` ; tu l'importes en jeu (Contrôles → Importer). |

Tout est réversible (bouton Désinstaller), le launcher détecte les installations Steam / Gaijin, vérifie les SHA-256, reprend les téléchargements interrompus et répare le mod de sons si une mise à jour du jeu l'a désactivé. Interface FR / EN (langue du système par défaut).

## Utilisation

1. Télécharge `WTModLauncher.exe` depuis les [releases](https://github.com/Robocnop/WarThunderModLauncher/releases) (version `v*` la plus récente).
2. Lance-le (Windows SmartScreen peut afficher « Éditeur inconnu » → *Informations complémentaires* → *Exécuter quand même* : l'exe n'est pas signé).
3. Coche ce que tu veux, clique **INSTALLER**.

## Développement

```powershell
dotnet test WTModLauncher.slnx
dotnet run --project src/WTModLauncher
dotnet publish src/WTModLauncher -c Release -o publish   # exe autonome unique
```

- **Appli** : pousser un tag `vX.Y.Z` → la CI publie `WTModLauncher.exe` sur la release.
- **Contenu** (mods / config) : mettre à jour `src/WTModLauncher/default-manifest.json` (version, url, sha256, size) puis
  `./tools/publish-content.ps1 -Tag content-AAAA.MM -Files <fichiers>`. Le launcher lit le `manifest.json` de la release `content-*` la plus récente, sans recompiler.

## Crédits

- RCSM — FireXsoldier ([War Thunder Live](https://live.warthunder.com/post/1089792/en/))
- WT-FCSGenerator — tsvl & Assin127 (téléchargé depuis leurs releases, jamais ré-hébergé)
- Police Rajdhani — Indian Type Foundry, SIL OFL 1.1

Projet de fan, non affilié à Gaijin Entertainment. War Thunder est une marque de Gaijin Entertainment.
