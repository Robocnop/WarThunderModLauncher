using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace WTModLauncher.Core;

/// <summary>FR/EN string table. Bind with {core:T Key}; switching language refreshes every binding.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public static readonly string[] Supported = ["fr", "en"];

    private string _lang = DetectNative();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language
    {
        get => _lang;
        set
        {
            var v = Supported.Contains(value) ? value : "en";
            if (v == _lang) return;
            _lang = v;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    public string this[string key] =>
        (_lang == "fr" ? Fr : En).TryGetValue(key, out var s) ? s
        : En.TryGetValue(key, out var e) ? e : key;

    public static string Get(string key) => Instance[key];
    public static string Format(string key, params object[] args) => string.Format(Instance[key], args);

    /// <summary>Picks a localized value ("fr"/"en") out of a manifest dictionary.</summary>
    public string Pick(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0) return "";
        if (values.TryGetValue(_lang, out var v)) return v;
        if (values.TryGetValue("en", out var en)) return en;
        return values.Values.First();
    }

    public static string DetectNative() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";

    private static readonly Dictionary<string, string> Fr = new()
    {
        ["Nav.Mods"] = "MODS",
        ["Nav.Settings"] = "PARAMÈTRES",
        ["Nav.Log"] = "JOURNAL",
        ["Nav.About"] = "À PROPOS",
        ["Hero.Title"] = "PRÉPARE TON WAR THUNDER",
        ["Hero.Subtitle"] = "Coche ce que tu veux installer : sons réalistes, viseurs balistiques et la config de Robo. Tout est réversible.",
        ["Card.Install"] = "Installer",
        ["Card.By"] = "par {0}",
        ["Card.Version"] = "Version {0}",
        ["Card.Uninstall"] = "Désinstaller",
        ["Card.Details"] = "Page du mod",
        ["Status.NotInstalled"] = "NON INSTALLÉ",
        ["Status.Installed"] = "INSTALLÉ",
        ["Status.Update"] = "MISE À JOUR",
        ["Status.Repair"] = "À RÉPARER",
        ["Status.Unknown"] = "…",
        ["Action.Install"] = "INSTALLER",
        ["Action.UpToDate"] = "À JOUR",
        ["Action.Working"] = "EN COURS…",
        ["Action.Cancel"] = "Annuler",
        ["Action.LaunchFcs"] = "Lancer FCS",
        ["Action.OpenSights"] = "Dossier UserSights",
        ["Action.OpenPreset"] = "Ouvrir le dossier",
        ["Action.CopyPath"] = "Copier le chemin",
        ["Action.Play"] = "JOUER",
        ["Game.NotFound"] = "War Thunder introuvable — choisis le dossier dans Paramètres",
        ["Game.Found"] = "War Thunder : {0}",
        ["Game.Running"] = "Ferme War Thunder avant d'installer ou désinstaller un mod.",
        ["Settings.GamePath"] = "DOSSIER DU JEU",
        ["Settings.Browse"] = "Parcourir…",
        ["Settings.Detect"] = "Détecter",
        ["Settings.InvalidGame"] = "Ce dossier ne contient pas War Thunder (config.blk + win64\\aces.exe attendus).",
        ["Settings.UserSights"] = "DOSSIER USERSIGHTS (VISEURS)",
        ["Settings.Profile"] = "Profil",
        ["Settings.Language"] = "LANGUE",
        ["Settings.Repair"] = "VÉRIFIER / RÉPARER",
        ["Settings.RepairHint"] = "Une mise à jour du jeu peut désactiver le mod de sons. Ce bouton revérifie tout et répare ce qui doit l'être.",
        ["Settings.RepairButton"] = "Vérifier et réparer",
        ["Settings.Data"] = "DONNÉES DU LAUNCHER",
        ["Settings.OpenData"] = "Ouvrir le dossier",
        ["Log.Title"] = "JOURNAL",
        ["Log.Copy"] = "Copier",
        ["About.Title"] = "À PROPOS",
        ["About.Text"] = "War Thunder Mod Launcher installe et configure des mods communautaires pour une meilleure expérience de jeu. Il ne modifie jamais les fichiers protégés du jeu : seulement le dossier sound\\mod, l'option enable_mod de config.blk et tes dossiers personnels.",
        ["About.Credits"] = "CRÉDITS",
        ["About.Disclaimer"] = "Projet de fan, non affilié à Gaijin Entertainment. War Thunder est une marque de Gaijin Entertainment.",
        ["Msg.ManifestOffline"] = "Catalogue en ligne indisponible — catalogue intégré utilisé.",
        ["Msg.ManifestLoaded"] = "Catalogue chargé ({0}).",
        ["Msg.Downloading"] = "Téléchargement de {0}…",
        ["Msg.Verifying"] = "Vérification de {0}…",
        ["Msg.Extracting"] = "Extraction de {0}…",
        ["Msg.Installed"] = "{0} installé.",
        ["Msg.Uninstalled"] = "{0} désinstallé.",
        ["Msg.Failed"] = "Échec de {0} : {1}",
        ["Msg.Done"] = "Terminé.",
        ["Msg.Cancelled"] = "Annulé.",
        ["Msg.NeedAdmin"] = "Le dossier du jeu est protégé en écriture. Relancer le launcher en administrateur ?",
        ["Msg.ConfirmUninstall"] = "Désinstaller {0} ?",
        ["Msg.ChecksumMismatch"] = "Le fichier téléchargé est corrompu (SHA-256 différent).",
        ["Msg.SoundModEnabled"] = "enable_mod activé dans config.blk.",
        ["Msg.SoundModDisabled"] = "enable_mod désactivé dans config.blk.",
        ["Msg.Repaired"] = "{0} réparé.",
        ["Msg.AllGood"] = "Tout est en ordre.",
        ["Msg.Detected"] = "{0} était déjà installé : repris par le launcher.",
        ["Msg.DetectedAt"] = "{0} trouvé dans {1} : repris par le launcher.",
        ["Msg.ExternalLeft"] = "{0} : la copie de {1} n'a pas été installée par le launcher, elle est laissée en place.",
        ["Preset.Applied"] = "Cette config est active dans ton profil War Thunder. Pour la réappliquer : Contrôles → Importer, avec le fichier ci-dessous.",
        ["Update.Available"] = "Nouvelle version du launcher disponible : {0}",
        ["Update.Install"] = "METTRE À JOUR",
        ["Update.Notes"] = "Nouveautés",
        ["Update.Downloading"] = "Téléchargement de la mise à jour {0}…",
        ["Update.Restarting"] = "Redémarrage sur la nouvelle version…",
        ["Update.UpToDate"] = "Le launcher est à jour ({0}).",
        ["Update.CheckFailed"] = "Impossible de vérifier les mises à jour : {0}",
        ["Update.Failed"] = "Échec de la mise à jour : {0}. La page de téléchargement s'ouvre.",
        ["Settings.Updates"] = "MISES À JOUR DU LAUNCHER",
        ["Settings.VersionInstalled"] = "Version {0} — installée",
        ["Settings.VersionPortable"] = "Version {0} — portable",
        ["Settings.Prereleases"] = "Recevoir les pré-versions (bêta)",
        ["Settings.CheckUpdates"] = "Vérifier maintenant",
        ["Preset.HowTo"] = "Pour appliquer la config : en jeu, Contrôles → Importer, puis choisis le fichier ci-dessous. Tu peux d'abord exporter tes contrôles actuels en sauvegarde.",
        ["Fcs.HowTo"] = "Lance FCS, choisis ton dossier War Thunder, génère les viseurs, puis copie-les dans UserSights. Pour les viseurs à télémètre box (Tochka, Luch, Sector), la config Robo règle déjà la molette à 50 % sur la distance de visée.",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["Nav.Mods"] = "MODS",
        ["Nav.Settings"] = "SETTINGS",
        ["Nav.Log"] = "LOG",
        ["Nav.About"] = "ABOUT",
        ["Hero.Title"] = "GEAR UP YOUR WAR THUNDER",
        ["Hero.Subtitle"] = "Tick what you want: realistic sounds, ballistic sights and Robo's controls. Everything can be undone.",
        ["Card.Install"] = "Install",
        ["Card.By"] = "by {0}",
        ["Card.Version"] = "Version {0}",
        ["Card.Uninstall"] = "Uninstall",
        ["Card.Details"] = "Mod page",
        ["Status.NotInstalled"] = "NOT INSTALLED",
        ["Status.Installed"] = "INSTALLED",
        ["Status.Update"] = "UPDATE",
        ["Status.Repair"] = "NEEDS REPAIR",
        ["Status.Unknown"] = "…",
        ["Action.Install"] = "INSTALL",
        ["Action.UpToDate"] = "UP TO DATE",
        ["Action.Working"] = "WORKING…",
        ["Action.Cancel"] = "Cancel",
        ["Action.LaunchFcs"] = "Launch FCS",
        ["Action.OpenSights"] = "UserSights folder",
        ["Action.OpenPreset"] = "Open folder",
        ["Action.CopyPath"] = "Copy path",
        ["Action.Play"] = "PLAY",
        ["Game.NotFound"] = "War Thunder not found — pick the folder in Settings",
        ["Game.Found"] = "War Thunder: {0}",
        ["Game.Running"] = "Close War Thunder before installing or removing a mod.",
        ["Settings.GamePath"] = "GAME FOLDER",
        ["Settings.Browse"] = "Browse…",
        ["Settings.Detect"] = "Detect",
        ["Settings.InvalidGame"] = "This folder does not contain War Thunder (expected config.blk + win64\\aces.exe).",
        ["Settings.UserSights"] = "USERSIGHTS FOLDER (SIGHTS)",
        ["Settings.Profile"] = "Profile",
        ["Settings.Language"] = "LANGUAGE",
        ["Settings.Repair"] = "CHECK / REPAIR",
        ["Settings.RepairHint"] = "A game update can turn the sound mod off. This re-checks everything and fixes what needs fixing.",
        ["Settings.RepairButton"] = "Check and repair",
        ["Settings.Data"] = "LAUNCHER DATA",
        ["Settings.OpenData"] = "Open folder",
        ["Log.Title"] = "LOG",
        ["Log.Copy"] = "Copy",
        ["About.Title"] = "ABOUT",
        ["About.Text"] = "War Thunder Mod Launcher installs and configures community mods for a better game experience. It never touches protected game files: only the sound\\mod folder, the enable_mod option in config.blk and your personal folders.",
        ["About.Credits"] = "CREDITS",
        ["About.Disclaimer"] = "Fan project, not affiliated with Gaijin Entertainment. War Thunder is a trademark of Gaijin Entertainment.",
        ["Msg.ManifestOffline"] = "Online catalog unavailable — using the built-in catalog.",
        ["Msg.ManifestLoaded"] = "Catalog loaded ({0}).",
        ["Msg.Downloading"] = "Downloading {0}…",
        ["Msg.Verifying"] = "Verifying {0}…",
        ["Msg.Extracting"] = "Extracting {0}…",
        ["Msg.Installed"] = "{0} installed.",
        ["Msg.Uninstalled"] = "{0} uninstalled.",
        ["Msg.Failed"] = "{0} failed: {1}",
        ["Msg.Done"] = "Done.",
        ["Msg.Cancelled"] = "Cancelled.",
        ["Msg.NeedAdmin"] = "The game folder is write-protected. Restart the launcher as administrator?",
        ["Msg.ConfirmUninstall"] = "Uninstall {0}?",
        ["Msg.ChecksumMismatch"] = "The downloaded file is corrupted (SHA-256 mismatch).",
        ["Msg.SoundModEnabled"] = "enable_mod turned on in config.blk.",
        ["Msg.SoundModDisabled"] = "enable_mod turned off in config.blk.",
        ["Msg.Repaired"] = "{0} repaired.",
        ["Msg.AllGood"] = "Everything is fine.",
        ["Msg.Detected"] = "{0} was already installed: now managed by the launcher.",
        ["Msg.DetectedAt"] = "{0} found in {1}: now managed by the launcher.",
        ["Msg.ExternalLeft"] = "{0}: the copy in {1} was not installed by the launcher and was left in place.",
        ["Preset.Applied"] = "This config is active in your War Thunder profile. To apply it again: Controls → Import, with the file below.",
        ["Update.Available"] = "A new launcher version is available: {0}",
        ["Update.Install"] = "UPDATE NOW",
        ["Update.Notes"] = "What's new",
        ["Update.Downloading"] = "Downloading update {0}…",
        ["Update.Restarting"] = "Restarting on the new version…",
        ["Update.UpToDate"] = "The launcher is up to date ({0}).",
        ["Update.CheckFailed"] = "Could not check for updates: {0}",
        ["Update.Failed"] = "Update failed: {0}. Opening the download page.",
        ["Settings.Updates"] = "LAUNCHER UPDATES",
        ["Settings.VersionInstalled"] = "Version {0} — installed",
        ["Settings.VersionPortable"] = "Version {0} — portable",
        ["Settings.Prereleases"] = "Get pre-releases (beta)",
        ["Settings.CheckUpdates"] = "Check now",
        ["Preset.HowTo"] = "To apply the controls: in game, Controls → Import, then pick the file below. You can export your current controls first as a backup.",
        ["Fcs.HowTo"] = "Launch FCS, pick your War Thunder folder, generate sights, then copy them into UserSights. For box-rangefinder sights (Tochka, Luch, Sector), Robo's config already sets the mouse wheel to sight distance at 50%.",
    };
}

/// <summary>XAML: Text="{core:T Nav.Mods}"</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
