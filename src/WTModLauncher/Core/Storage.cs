using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WTModLauncher.Core;

public static class AppPaths
{
    public static string Root { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WTModLauncher");

    public static string Cache => Path.Combine(Root, "cache");
    public static string Tools => Path.Combine(Root, "tools");
    public static string Logs => Path.Combine(Root, "logs");
    public static string StateFile => Path.Combine(Root, "state.json");
}

public sealed class InstalledItem
{
    public string Version { get; set; } = "";
    /// <summary>Paths relative to the install root (game dir, tool dir...), forward slashes.</summary>
    public List<string> Files { get; set; } = [];
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    /// <summary>Found on disk rather than installed by the launcher.</summary>
    public bool Adopted { get; set; }
    /// <summary>Install root when it is not the default one (tool adopted from another folder).</summary>
    public string? Location { get; set; }
}

public sealed class LauncherState
{
    public string? GamePath { get; set; }
    public string? Language { get; set; }
    public string? SightsProfileId { get; set; }
    /// <summary>Null = follow the channel of the running build (pre-release builds get pre-release updates).</summary>
    public bool? IncludePrereleases { get; set; }
    public Dictionary<string, InstalledItem> Items { get; set; } = [];
    /// <summary>Copies the player chose to forget: never adopted again.</summary>
    public List<string> IgnoredPaths { get; set; } = [];
}

public static class StateStore
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly object Gate = new();

    public static LauncherState Load()
    {
        try
        {
            if (File.Exists(AppPaths.StateFile))
                return JsonSerializer.Deserialize<LauncherState>(File.ReadAllText(AppPaths.StateFile), Json) ?? new();
        }
        catch (Exception)
        {
            // Corrupted state: keep a copy for diagnosis and start fresh rather than crash.
            TryCopy(AppPaths.StateFile, AppPaths.StateFile + ".corrupt");
        }
        return new LauncherState();
    }

    public static void Save(LauncherState state)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(AppPaths.Root);
            var tmp = AppPaths.StateFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json));
            File.Move(tmp, AppPaths.StateFile, overwrite: true);
        }
    }

    private static void TryCopy(string from, string to)
    {
        try { File.Copy(from, to, overwrite: true); } catch (Exception) { }
    }
}
