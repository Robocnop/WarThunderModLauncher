using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace WTModLauncher.Core;

public sealed record UpdateCheck(AppRelease? Release, bool Prereleases);

public sealed record AppRelease(Version Version, string Tag, bool Prerelease, string HtmlUrl, string AssetName, string AssetUrl, string Sha256);

/// <summary>Self-update from the "v*" GitHub releases: the installer for installed copies, the exe for portable ones.</summary>
public static class AppUpdater
{
    public const string SetupPrefix = "WTModLauncher-Setup-";
    public const string PortableName = "WTModLauncher.exe";
    public const string ChecksumsName = "SHA256SUMS.txt";

    public static Version Current { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Installed by the Inno Setup installer (its uninstaller sits next to the exe) rather than run portable.</summary>
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>
    /// Newest "v*" release above <see cref="Current"/> (Release is null when up to date), and whether pre-releases
    /// were considered. <paramref name="includePrereleases"/> null means "same channel as the running build":
    /// a build published as a pre-release keeps receiving pre-releases.
    /// </summary>
    public static async Task<UpdateCheck> CheckAsync(HttpClient http, bool? includePrereleases, CancellationToken ct)
    {
        using var doc = await Http.GetGitHubJsonAsync(http, $"https://api.github.com/repos/{Http.Repo}/releases?per_page=30", ct);
        var releases = doc.RootElement.EnumerateArray()
            .Where(r => !r.GetProperty("draft").GetBoolean())
            .Select(r => (Json: r, Version: ParseTag(r.GetProperty("tag_name").GetString())))
            .Where(r => r.Version is not null)
            .ToList();

        var prerelease = includePrereleases ?? IsPrereleaseChannel(releases.Select(r => (r.Version!, r.Json.GetProperty("prerelease").GetBoolean())));

        foreach (var (json, version) in releases.Where(r => r.Version > Current).OrderByDescending(r => r.Version))
        {
            if (json.GetProperty("prerelease").GetBoolean() && !prerelease) continue;
            if (await PickAssetAsync(http, json, IsInstalled, ct) is not { } asset) continue;
            return new UpdateCheck(new AppRelease(version!, json.GetProperty("tag_name").GetString()!,
                json.GetProperty("prerelease").GetBoolean(), json.GetProperty("html_url").GetString() ?? "",
                asset.Name, asset.Url, asset.Sha256), prerelease);
        }
        return new UpdateCheck(null, prerelease);
    }

    /// <summary>The running build was published as a pre-release (or not published at all: a dev build).</summary>
    internal static bool IsPrereleaseChannel(IEnumerable<(Version Version, bool Prerelease)> releases) =>
        releases.FirstOrDefault(r => r.Version == Current) is not { Version: not null } own || own.Prerelease;

    /// <summary>Asset to download for this kind of copy, with its SHA-256. Releases without a checksum are skipped.</summary>
    private static async Task<(string Name, string Url, string Sha256)?> PickAssetAsync(HttpClient http, JsonElement release,
        bool installed, CancellationToken ct)
    {
        var assets = release.GetProperty("assets").EnumerateArray().ToList();
        static string Name(JsonElement a) => a.GetProperty("name").GetString() ?? "";

        var wanted = assets.FirstOrDefault(a => installed
            ? Name(a).StartsWith(SetupPrefix, StringComparison.OrdinalIgnoreCase) && Name(a).EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            : Name(a).Equals(PortableName, StringComparison.OrdinalIgnoreCase));
        if (wanted.ValueKind == JsonValueKind.Undefined) return null;

        var url = wanted.GetProperty("browser_download_url").GetString()!;

        // GitHub computes "digest": "sha256:..." for uploaded assets; SHA256SUMS.txt is the fallback.
        if (wanted.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
            && digest.GetString() is { } d && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            return (Name(wanted), url, d["sha256:".Length..].ToLowerInvariant());

        var sums = assets.FirstOrDefault(a => Name(a).Equals(ChecksumsName, StringComparison.OrdinalIgnoreCase));
        if (sums.ValueKind == JsonValueKind.Undefined) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        var text = await http.GetStringAsync(sums.GetProperty("browser_download_url").GetString(), cts.Token);
        return ParseChecksums(text, Name(wanted)) is { } sha ? (Name(wanted), url, sha) : null;
    }

    /// <summary>Downloads the update into the cache, verifying its SHA-256.</summary>
    public static async Task<string> DownloadAsync(HttpClient http, AppRelease release, IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        var dest = Path.Combine(AppPaths.Cache, "update", release.AssetName);
        await Downloader.DownloadAsync(http, release.AssetUrl, dest, release.Sha256, progress, ct);
        return dest;
    }

    /// <summary>
    /// Starts the new version and returns; the caller must then shut the app down.
    /// Installer: runs silently over the current install and relaunches the app.
    /// Portable: the running exe is renamed (Windows allows it) and the new one takes its place.
    /// </summary>
    public static void Apply(string downloaded)
    {
        if (IsInstalled)
        {
            Process.Start(new ProcessStartInfo(downloaded, "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1") { UseShellExecute = true });
            return;
        }

        var current = Environment.ProcessPath!;
        var old = current + ".old";
        File.Move(current, old, overwrite: true);
        try
        {
            File.Move(downloaded, current);
        }
        catch
        {
            File.Move(old, current); // put the running version back
            throw;
        }
        Process.Start(new ProcessStartInfo(current) { UseShellExecute = true });
    }

    /// <summary>Removes what a previous update left behind.</summary>
    public static void CleanupAfterUpdate()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && File.Exists(exe + ".old")) File.Delete(exe + ".old");
            var dir = Path.Combine(AppPaths.Cache, "update");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception) { /* the old process may still be exiting: next launch */ }
    }

    internal static Version? ParseTag(string? tag) =>
        tag is not null && tag.StartsWith('v') && Versions.Parse(tag) is { } v ? Normalize(v) : null;

    /// <summary>"sha256  name" lines (sha256sum format, optional '*' before binary names).</summary>
    internal static string? ParseChecksums(string text, string fileName)
    {
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Trim().Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0].Length == 64
                && parts[1].Trim().TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0].ToLowerInvariant();
        }
        return null;
    }

    /// <summary>Compares on major.minor.build only (assembly versions carry a 4th "revision" part).</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
