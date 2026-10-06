using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace WTModLauncher.Core;

public enum ModStatus { Unknown, NotInstalled, Installed, UpdateAvailable, NeedsRepair }

public readonly record struct InstallProgress(string Message, double? Fraction, string? Detail = null);

public sealed record InstallContext(
    string GamePath,
    LauncherState State,
    HttpClient Http,
    IProgress<InstallProgress> Progress,
    Action<string> Log)
{
    public void Save() => StateStore.Save(State);
}

public static class GameProcess
{
    public static bool IsRunning() =>
        Process.GetProcessesByName("aces").Length > 0 || Process.GetProcessesByName("aces_BE").Length > 0;
}

public abstract class ModInstaller(ModItem item)
{
    public ModItem Item { get; } = item;
    public string DisplayName => Loc.Instance.Pick(Item.Name);

    /// <summary>True when the installer writes into the game folder (game must be closed).</summary>
    public virtual bool TouchesGameFiles => false;

    public virtual Task<string> ResolveAvailableVersionAsync(InstallContext ctx, CancellationToken ct) =>
        Task.FromResult(Item.Version);

    public abstract ModStatus GetStatus(InstallContext ctx, string availableVersion);
    public abstract Task InstallAsync(InstallContext ctx, string availableVersion, CancellationToken ct);
    public abstract Task UninstallAsync(InstallContext ctx, CancellationToken ct);

    /// <summary>Fixes a NeedsRepair state. Default: reinstall.</summary>
    public virtual Task RepairAsync(InstallContext ctx, string availableVersion, CancellationToken ct) =>
        InstallAsync(ctx, availableVersion, ct);

    /// <summary>
    /// Recognises a copy installed without the launcher and records it in the state, so status, update and
    /// uninstall then work as usual. Only called while the state has no entry for the item.
    /// </summary>
    public virtual Task<bool> TryAdoptAsync(InstallContext ctx, string availableVersion, CancellationToken ct) =>
        Task.FromResult(false);

    public InstalledItem? Installed(InstallContext ctx) => ctx.State.Items.GetValueOrDefault(Item.Id);

    protected void Adopt(InstallContext ctx, string version, List<string> files, string? location = null)
    {
        ctx.State.Items[Item.Id] = new InstalledItem { Version = version, Files = files, Adopted = true, Location = location };
        ctx.Save();
    }

    public static ModInstaller Create(ModItem item) => item.Type switch
    {
        ModTypes.SoundMod => new SoundModInstaller(item),
        ModTypes.Controls => new ControlsPresetInstaller(item),
        ModTypes.GitHubTool => new GitHubToolInstaller(item),
        _ => throw new NotSupportedException($"Unknown mod type '{item.Type}'"),
    };

    protected async Task<string> DownloadToCacheAsync(InstallContext ctx, string url, string fileName, string? sha256, CancellationToken ct)
    {
        var dest = Path.Combine(AppPaths.Cache, fileName);
        var msg = Loc.Format("Msg.Downloading", DisplayName);
        ctx.Log(msg);
        await Downloader.DownloadAsync(ctx.Http, url, dest, sha256,
            new Progress<TransferProgress>(p => ctx.Progress.Report(new InstallProgress(msg,
                p.Total is > 0 ? (double)p.Done / p.Total.Value : null,
                FormatTransfer(p)))), ct);
        return dest;
    }

    private static string FormatTransfer(TransferProgress p)
    {
        static string Mb(long b) => $"{b / 1048576.0:0.0} MB";
        var size = p.Total is { } t ? $"{Mb(p.Done)} / {Mb(t)}" : Mb(p.Done);
        return p.BytesPerSecond > 0 ? $"{size} — {p.BytesPerSecond / 1048576.0:0.0} MB/s" : size;
    }

    /// <summary>
    /// Extracts entries accepted by <paramref name="filter"/> into <paramref name="destDir"/>.
    /// Returns the written paths relative to <paramref name="relativeTo"/>, with forward slashes.
    /// Entries escaping destDir (zip-slip) are rejected.
    /// </summary>
    internal static List<string> Extract(string zipPath, string destDir, string relativeTo, Func<ZipArchiveEntry, bool> filter,
        bool flatten, Action<double>? onProgress, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);
        var root = Path.GetFullPath(destDir);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;

        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && filter(e)).ToList();
        var totalBytes = Math.Max(1, entries.Sum(e => e.Length));
        long doneBytes = 0;
        var written = new List<string>();

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var rel = flatten ? entry.Name : entry.FullName.Replace('\\', '/');
            var target = Path.GetFullPath(Path.Combine(root, rel));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe path in archive: {entry.FullName}");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            written.Add(Path.GetRelativePath(relativeTo, target).Replace('\\', '/'));
            doneBytes += entry.Length;
            onProgress?.Invoke((double)doneBytes / totalBytes);
        }
        return written;
    }

    internal static void DeleteFiles(string root, IEnumerable<string> relativeFiles)
    {
        foreach (var rel in relativeFiles)
        {
            var path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) File.Delete(path);
        }
    }

    internal static void DeleteEmptyDirs(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var sub in Directory.GetDirectories(dir)) DeleteEmptyDirs(sub);
        if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
    }
}

/// <summary>FMOD .bank sound mod: banks go to &lt;game&gt;\sound\mod and config.blk gets sound{ enable_mod:b=yes }.</summary>
public sealed class SoundModInstaller(ModItem item) : ModInstaller(item)
{
    public override bool TouchesGameFiles => true;

    public static string ModDir(string game) => Path.Combine(game, "sound", "mod");

    private static bool FilesPresent(InstallContext ctx, InstalledItem? inst) =>
        inst is not null && inst.Files.Count > 0 && inst.Files.All(f => File.Exists(Path.Combine(ctx.GamePath, f)));

    public override ModStatus GetStatus(InstallContext ctx, string availableVersion)
    {
        var inst = Installed(ctx);
        if (inst is null) return ModStatus.NotInstalled;
        if (!FilesPresent(ctx, inst) || ConfigBlk.IsSoundModEnabled(ctx.GamePath) != true) return ModStatus.NeedsRepair;
        return inst.Version == availableVersion ? ModStatus.Installed : ModStatus.UpdateAvailable;
    }

    public override Task<bool> TryAdoptAsync(InstallContext ctx, string availableVersion, CancellationToken ct)
    {
        var dir = ModDir(ctx.GamePath);
        if (Item.Files is not { Count: > 0 } expected || !Directory.Exists(dir)) return Task.FromResult(false);

        var present = expected.Where(f => File.Exists(Path.Combine(dir, f.Key))).ToList();
        if (present.Count == 0) return Task.FromResult(false);

        // Every bank at the expected size: this exact build. Anything else is another build (or another sound mod
        // reusing the same FMOD bank names): adopted with an unknown version so the card offers the update.
        var exact = present.Count == expected.Count
                    && present.All(f => new FileInfo(Path.Combine(dir, f.Key)).Length == f.Value);
        Adopt(ctx, exact ? availableVersion : "", present.Select(f => $"sound/mod/{f.Key}").ToList());
        return Task.FromResult(true);
    }

    public override async Task InstallAsync(InstallContext ctx, string availableVersion, CancellationToken ct)
    {
        var zip = await DownloadToCacheAsync(ctx, Item.Url!, Item.FileName ?? $"{Item.Id}.zip", Item.Sha256, ct);

        // Remove banks from a previous version that the new one might not ship anymore.
        if (Installed(ctx) is { } previous) DeleteFiles(ctx.GamePath, previous.Files);

        var msg = Loc.Format("Msg.Extracting", DisplayName);
        ctx.Log(msg);
        var files = await Task.Run(() => Extract(zip, ModDir(ctx.GamePath), ctx.GamePath,
            e => e.Name.EndsWith(".bank", StringComparison.OrdinalIgnoreCase), flatten: true,
            f => ctx.Progress.Report(new InstallProgress(msg, f)), ct), ct);

        ConfigBlk.SetSoundModEnabled(ctx.GamePath, true);
        ctx.Log(Loc.Get("Msg.SoundModEnabled"));

        ctx.State.Items[Item.Id] = new InstalledItem { Version = availableVersion, Files = files };
        ctx.Save();
        TryDelete(zip); // 500+ MB, not worth keeping once installed
    }

    public override Task UninstallAsync(InstallContext ctx, CancellationToken ct)
    {
        if (Installed(ctx) is { } inst) DeleteFiles(ctx.GamePath, inst.Files);
        var dir = ModDir(ctx.GamePath);
        DeleteEmptyDirs(dir);
        // Another sound mod may still live in sound\mod: only turn the switch off when it is empty.
        if (!Directory.Exists(dir))
        {
            ConfigBlk.SetSoundModEnabled(ctx.GamePath, false);
            ctx.Log(Loc.Get("Msg.SoundModDisabled"));
        }
        ctx.State.Items.Remove(Item.Id);
        ctx.Save();
        return Task.CompletedTask;
    }

    public override async Task RepairAsync(InstallContext ctx, string availableVersion, CancellationToken ct)
    {
        if (FilesPresent(ctx, Installed(ctx)))
        {
            ConfigBlk.SetSoundModEnabled(ctx.GamePath, true);
            ctx.Log(Loc.Get("Msg.SoundModEnabled"));
        }
        else
        {
            await InstallAsync(ctx, availableVersion, ct);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception) { }
    }
}

/// <summary>Controls preset (.blk): dropped in &lt;game&gt;\ModLauncher\controls, then imported in-game by the player.</summary>
public sealed class ControlsPresetInstaller(ModItem item) : ModInstaller(item)
{
    public static string PresetDir(string game) => Path.Combine(game, "ModLauncher", "controls");
    public string PresetPath(string game) => Path.Combine(PresetDir(game), Item.FileName ?? $"{Item.Id}.blk");
    private string CachedPath => Path.Combine(AppPaths.Cache, Item.FileName ?? $"{Item.Id}.blk");

    public override ModStatus GetStatus(InstallContext ctx, string availableVersion)
    {
        var inst = Installed(ctx);
        if (inst is null) return ModStatus.NotInstalled;
        // Files is empty when adopted from the game profile but the game folder was not writable.
        if (inst.Files.Count > 0 && !File.Exists(PresetPath(ctx.GamePath))) return ModStatus.NeedsRepair;
        return inst.Version == availableVersion ? ModStatus.Installed : ModStatus.UpdateAvailable;
    }

    /// <summary>True when the preset's bindings are the active controls of a War Thunder profile (imported in game).</summary>
    public bool IsAppliedInGame(string game)
    {
        var source = File.Exists(PresetPath(game)) ? PresetPath(game) : CachedPath;
        try { return File.Exists(source) && ControlsDetector.IsAppliedInAnyProfile(File.ReadAllText(source)); }
        catch (IOException) { return false; }
    }

    public override async Task<bool> TryAdoptAsync(InstallContext ctx, string availableVersion, CancellationToken ct)
    {
        // The preset is tiny: fetch it (verified) to compare with what the player has.
        try { await Downloader.DownloadAsync(ctx.Http, Item.Url!, CachedPath, Item.Sha256, null, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return false; } // offline: try again next launch

        var target = PresetPath(ctx.GamePath);
        var dropped = File.Exists(target)
                      && (await File.ReadAllBytesAsync(target, ct)).SequenceEqual(await File.ReadAllBytesAsync(CachedPath, ct));
        if (!dropped && !ControlsDetector.IsAppliedInAnyProfile(await File.ReadAllTextAsync(CachedPath, ct))) return false;

        var files = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!dropped) File.Copy(CachedPath, target, overwrite: true);
            files.Add(Path.GetRelativePath(ctx.GamePath, target).Replace('\\', '/'));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* applied in game is what matters */ }

        Adopt(ctx, availableVersion, files);
        return true;
    }

    public override async Task InstallAsync(InstallContext ctx, string availableVersion, CancellationToken ct)
    {
        var cached = await DownloadToCacheAsync(ctx, Item.Url!, Item.FileName ?? $"{Item.Id}.blk", Item.Sha256, ct);
        var target = PresetPath(ctx.GamePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(cached, target, overwrite: true);
        ctx.State.Items[Item.Id] = new InstalledItem
        {
            Version = availableVersion,
            Files = [Path.GetRelativePath(ctx.GamePath, target).Replace('\\', '/')],
        };
        ctx.Save();
    }

    public override Task UninstallAsync(InstallContext ctx, CancellationToken ct)
    {
        if (Installed(ctx) is { } inst) DeleteFiles(ctx.GamePath, inst.Files);
        DeleteEmptyDirs(Path.Combine(ctx.GamePath, "ModLauncher"));
        ctx.State.Items.Remove(Item.Id);
        ctx.Save();
        return Task.CompletedTask;
    }
}

/// <summary>Standalone tool shipped as a zip on its author's GitHub releases (e.g. WT-FCSGenerator). Never re-hosted.</summary>
public sealed class GitHubToolInstaller(ModItem item) : ModInstaller(item)
{
    private string? _assetUrl;
    private string? _assetName;

    public string ToolDir => Path.Combine(AppPaths.Tools, Item.Id);

    /// <summary>The launcher's own copy, or the folder of a copy adopted from elsewhere.</summary>
    public string ExePath(InstallContext ctx) => Path.Combine(Installed(ctx)?.Location ?? ToolDir, Item.Exe ?? "");

    public override async Task<string> ResolveAvailableVersionAsync(InstallContext ctx, CancellationToken ct)
    {
        try
        {
            using var doc = await Http.GetGitHubJsonAsync(ctx.Http, $"https://api.github.com/repos/{Item.Repo}/releases/latest", ct);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (Item.AssetPattern is null || name.Contains(Item.AssetPattern, StringComparison.OrdinalIgnoreCase))
                {
                    _assetName = name;
                    _assetUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
            return tag;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Offline / rate-limited: report the installed version so the card doesn't claim an update.
            return Installed(ctx)?.Version ?? "";
        }
    }

    public override ModStatus GetStatus(InstallContext ctx, string availableVersion)
    {
        var inst = Installed(ctx);
        if (inst is null) return ModStatus.NotInstalled;
        if (!File.Exists(ExePath(ctx))) return ModStatus.NeedsRepair;
        return availableVersion.Length == 0 || Versions.Same(inst.Version, availableVersion) ? ModStatus.Installed : ModStatus.UpdateAvailable;
    }

    public override Task<bool> TryAdoptAsync(InstallContext ctx, string availableVersion, CancellationToken ct) => Task.Run(() =>
    {
        if (string.IsNullOrEmpty(Item.Exe)) return false;

        // The launcher's own folder survived but its state did not (state.json deleted...).
        if (File.Exists(Path.Combine(ToolDir, Item.Exe)))
        {
            var files = Directory.EnumerateFiles(ToolDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(ToolDir, f).Replace('\\', '/')).ToList();
            Adopt(ctx, "", files);
            return true;
        }

        // A copy the player unzipped themselves: prefer the newest one, judged by its folder name.
        var found = ExternalToolLocator.FindToolDirs(Item.Exe, ExternalToolLocator.SearchRoots())
            .Where(d => !ctx.State.IgnoredPaths.Contains(d, StringComparer.OrdinalIgnoreCase))
            .Select(d => (Dir: d, Version: ExternalToolLocator.VersionFromFolderName(d)))
            .OrderByDescending(x => Versions.Parse(x.Version))
            .FirstOrDefault();
        if (found.Dir is null) return false;

        // Files stays empty: the launcher never deletes a folder it did not create.
        Adopt(ctx, found.Version ?? "", [], found.Dir);
        return true;
    }, ct);

    public override async Task InstallAsync(InstallContext ctx, string availableVersion, CancellationToken ct)
    {
        if (_assetUrl is null)
        {
            availableVersion = await ResolveAvailableVersionAsync(ctx, ct);
            if (_assetUrl is null) throw new InvalidOperationException($"No release asset found on github.com/{Item.Repo}");
        }

        var zip = await DownloadToCacheAsync(ctx, _assetUrl, _assetName!, sha256: null, ct);

        // An adopted copy elsewhere is left alone: the launcher now manages its own copy.
        if (Installed(ctx)?.Location is { } external) ctx.Log(Loc.Format("Msg.ExternalLeft", DisplayName, external));

        // Extract over the existing folder: the tool keeps user output (e.g. generated sights) next to its exe.
        var previous = Installed(ctx) is { Location: null } own ? own.Files : [];
        var msg = Loc.Format("Msg.Extracting", DisplayName);
        ctx.Log(msg);
        var files = await Task.Run(() => Extract(zip, ToolDir, ToolDir, _ => true, flatten: false,
            f => ctx.Progress.Report(new InstallProgress(msg, f)), ct), ct);
        DeleteFiles(ToolDir, previous.Except(files, StringComparer.OrdinalIgnoreCase));

        ctx.State.Items[Item.Id] = new InstalledItem { Version = availableVersion, Files = files };
        ctx.Save();
        try { File.Delete(zip); } catch (Exception) { }
    }

    public override Task UninstallAsync(InstallContext ctx, CancellationToken ct)
    {
        if (Installed(ctx)?.Location is { } external)
        {
            // Not ours to delete: forget it, and don't adopt it again on next launch.
            ctx.State.IgnoredPaths.Add(external);
            ctx.Log(Loc.Format("Msg.ExternalLeft", DisplayName, external));
        }
        else
        {
            if (Installed(ctx) is { } inst) DeleteFiles(ToolDir, inst.Files);
            DeleteEmptyDirs(ToolDir);
        }
        ctx.State.Items.Remove(Item.Id);
        ctx.Save();
        return Task.CompletedTask;
    }

    public void Launch(InstallContext ctx)
    {
        var exe = ExePath(ctx);
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = true });
    }
}

/// <summary>Reads/writes the sound{ enable_mod } switch of &lt;game&gt;\config.blk, keeping a one-time backup.</summary>
public static class ConfigBlk
{
    public static string PathOf(string game) => Path.Combine(game, "config.blk");

    public static bool? IsSoundModEnabled(string game)
    {
        var p = PathOf(game);
        return File.Exists(p) ? BlkEditor.GetBool(File.ReadAllText(p), "sound", "enable_mod") : null;
    }

    public static void SetSoundModEnabled(string game, bool enabled)
    {
        var p = PathOf(game);
        var bytes = File.ReadAllBytes(p);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));

        var updated = BlkEditor.SetBool(text, "sound", "enable_mod", enabled);
        if (updated == text) return;

        var backup = p + ".modlauncher.bak";
        if (!File.Exists(backup)) File.Copy(p, backup);

        var tmp = p + ".tmp";
        File.WriteAllText(tmp, updated, new UTF8Encoding(hasBom));
        File.Move(tmp, p, overwrite: true);
    }
}
