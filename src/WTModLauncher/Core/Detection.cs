using System.IO;
using System.Text.RegularExpressions;

namespace WTModLauncher.Core;

/// <summary>Tells whether a controls preset is the one currently applied in a War Thunder profile.</summary>
public static class ControlsDetector
{
    /// <summary>The blocks an in-game import replaces; params/deviceMapping vary with the player's settings and devices.</summary>
    private static readonly string[] BindingBlocks = ["hotkeys", "axes"];

    /// <summary>Documents\My Games\WarThunder\Saves\&lt;id&gt;\production\machine.blk of every profile (holds the active controls).</summary>
    public static IEnumerable<string> ProfileMachineBlks() =>
        GameLocator.FindSightsProfiles()
            .Select(p => Path.Combine(Path.GetDirectoryName(p.UserSightsPath)!, "machine.blk"))
            .Where(File.Exists);

    /// <summary>True when <paramref name="preset"/> (an exported controls .blk) is applied in any profile.</summary>
    public static bool IsAppliedInAnyProfile(string preset)
    {
        foreach (var machine in ProfileMachineBlks())
        {
            try
            {
                if (SameBindings(preset, File.ReadAllText(machine))) return true;
            }
            catch (IOException) { /* file locked by the game: try the other profiles */ }
        }
        return false;
    }

    /// <summary>Compares the key/axis bindings of an exported preset with the controls{} of a machine.blk.</summary>
    internal static bool SameBindings(string preset, string machineBlk)
    {
        var presetControls = BlkEditor.GetBlockBody(preset, "controls");
        var activeControls = BlkEditor.GetBlockBody(machineBlk, "content", "controls");
        if (presetControls is null || activeControls is null) return false;

        foreach (var block in BindingBlocks)
        {
            var expected = BlkEditor.GetBlockBody(presetControls, block);
            var actual = BlkEditor.GetBlockBody(activeControls, block);
            if (expected is null || actual is null) return false;
            if (!BlkEditor.NormalizedLines(expected).SequenceEqual(BlkEditor.NormalizedLines(actual))) return false;
        }
        return true;
    }
}

/// <summary>Finds a copy of a standalone tool (e.g. FCS.exe) the player unzipped somewhere themselves.</summary>
public static partial class ExternalToolLocator
{
    public static IEnumerable<string> SearchRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Path.Combine(home, "Downloads");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            yield return Path.Combine(drive.RootDirectory.FullName, "Games");
            yield return Path.Combine(drive.RootDirectory.FullName, "Tools");
        }
    }

    /// <summary>Directories (up to 3 levels deep under <paramref name="roots"/>) holding <paramref name="exeName"/>.</summary>
    public static IEnumerable<string> FindToolDirs(string exeName, IEnumerable<string> roots)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 3,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };
        foreach (var root in roots.Where(Directory.Exists))
        {
            List<string> hits;
            try { hits = Directory.EnumerateFiles(root, exeName, options).ToList(); }
            catch (Exception) { continue; }
            foreach (var hit in hits) yield return Path.GetDirectoryName(hit)!;
        }
    }

    /// <summary>"WT-FCSGenerator-v2.0.2-win-x64" → "v2.0.2". Null when the folder name carries no version.</summary>
    internal static string? VersionFromFolderName(string dir)
    {
        var m = VersionRegex().Match(Path.GetFileName(dir.TrimEnd('\\', '/')));
        return m.Success ? "v" + m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(?:^|[-_ ])v?(\d+\.\d+(?:\.\d+)?)(?=$|[-_ ])", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();
}

public static class Versions
{
    /// <summary>"v2.1.0" and "2.1.0" are the same version.</summary>
    public static bool Same(string? a, string? b) =>
        string.Equals(a?.TrimStart('v', 'V'), b?.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase);

    /// <summary>"v1.2.3", "1.2.3-beta" → 1.2.3. Null when not a version.</summary>
    public static Version? Parse(string? v) =>
        Version.TryParse(v?.TrimStart('v', 'V').Split('-', '+')[0], out var parsed) ? parsed : null;
}
