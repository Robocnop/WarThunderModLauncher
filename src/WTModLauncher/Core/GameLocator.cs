using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WTModLauncher.Core;

public sealed record SightsProfile(string UserId, string UserSightsPath);

/// <summary>Finds the War Thunder install (Steam first, then Gaijin launcher, then common paths).</summary>
public static partial class GameLocator
{
    public const string SteamAppId = "236390";

    public static bool IsValidGameDir(string? dir) =>
        !string.IsNullOrWhiteSpace(dir)
        && File.Exists(Path.Combine(dir, "config.blk"))
        && (File.Exists(Path.Combine(dir, "win64", "aces.exe")) || File.Exists(Path.Combine(dir, "launcher.exe")));

    public static string? Detect()
    {
        foreach (var candidate in Candidates())
        {
            try
            {
                if (IsValidGameDir(candidate)) return Path.GetFullPath(candidate);
            }
            catch (Exception) { /* unreadable candidate, keep looking */ }
        }
        return null;
    }

    private static IEnumerable<string> Candidates()
    {
        foreach (var p in SteamCandidates()) yield return p;
        foreach (var p in UninstallCandidates()) yield return p;

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var root = drive.RootDirectory.FullName;
            yield return Path.Combine(root, "SteamLibrary", "steamapps", "common", "War Thunder");
            yield return Path.Combine(root, "Program Files (x86)", "Steam", "steamapps", "common", "War Thunder");
            yield return Path.Combine(root, "Games", "War Thunder");
        }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "WarThunder");
    }

    private static IEnumerable<string> SteamCandidates()
    {
        string? steam = null;
        try
        {
            steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        }
        catch (Exception) { }
        if (steam is null) yield break;

        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        var libraries = File.Exists(vdf) ? ParseLibraryFolders(File.ReadAllText(vdf)) : [];
        if (!libraries.Contains(steam, StringComparer.OrdinalIgnoreCase)) libraries.Insert(0, steam);

        foreach (var lib in libraries)
        {
            var acf = Path.Combine(lib, "steamapps", $"appmanifest_{SteamAppId}.acf");
            if (!File.Exists(acf)) continue;
            var installDir = ParseAcfInstallDir(File.ReadAllText(acf)) ?? "War Thunder";
            yield return Path.Combine(lib, "steamapps", "common", installDir);
        }
    }

    private static IEnumerable<string> UninstallCandidates()
    {
        var results = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var keyPath in new[]
                 {
                     @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                     @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                 })
        {
            try
            {
                using var root = hive.OpenSubKey(keyPath);
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(name);
                    if (k?.GetValue("DisplayName") is string dn && dn.Contains("War Thunder", StringComparison.OrdinalIgnoreCase)
                        && k.GetValue("InstallLocation") is string loc && loc.Length > 0)
                        results.Add(loc);
                }
            }
            catch (Exception) { }
        }
        return results;
    }

    internal static List<string> ParseLibraryFolders(string vdf) =>
        LibraryPathRegex().Matches(vdf).Select(m => m.Groups[1].Value.Replace(@"\\", @"\")).ToList();

    internal static string? ParseAcfInstallDir(string acf)
    {
        var m = InstallDirRegex().Match(acf);
        return m.Success ? m.Groups[1].Value.Replace(@"\\", @"\") : null;
    }

    /// <summary>One entry per Gaijin profile found under Documents\My Games\WarThunder\Saves.</summary>
    public static List<SightsProfile> FindSightsProfiles()
    {
        var saves = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "WarThunder", "Saves");
        if (!Directory.Exists(saves)) return [];
        return Directory.GetDirectories(saves)
            .Select(Path.GetFileName)
            .Where(n => n is not null && n.All(char.IsDigit))
            .Select(n => new SightsProfile(n!, Path.Combine(saves, n!, "production", "UserSights")))
            .ToList();
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();
}
