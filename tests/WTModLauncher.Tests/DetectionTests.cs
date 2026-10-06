using System.IO;
using System.Net.Http;
using WTModLauncher.Core;

namespace WTModLauncher.Tests;

public class ControlsDetectorTests
{
    private const string Preset =
        "controls{\r\n  version:i=5\r\n  hotkeys{\r\n    ID_FIRE{\r\n      mouseButton:i=0\r\n    }\r\n  }\r\n" +
        "  axes{\r\n    zoom{\r\n      axisId:i=-1\r\n    }\r\n  }\r\n  params{\r\n    USEROPT_MOUSE_SMOOTH:b=yes\r\n  }\r\n}\r\n";

    // The game nests controls inside content{} and indents one level deeper; params differ.
    private const string Machine =
        "version:i=322\n\ncontent{\n  machineId:t=\"x\"\n\n  controls{\n    version:i=5\n    hotkeys{\n      ID_FIRE{\n        mouseButton:i=0\n      }\n    }\n\n" +
        "    axes{\n      zoom{\n        axisId:i=-1\n      }\n    }\n    params{}\n  }\n  settings{}\n}\n";

    [Fact]
    public void Reads_nested_block_bodies()
    {
        Assert.NotNull(BlkEditor.GetBlockBody(Machine, "content", "controls", "hotkeys"));
        Assert.Null(BlkEditor.GetBlockBody(Machine, "controls")); // not top level in machine.blk
        Assert.Null(BlkEditor.GetBlockBody(Machine, "content", "hotkeys"));
    }

    [Fact]
    public void Same_bindings_ignore_indentation_line_endings_and_params()
    {
        Assert.True(ControlsDetector.SameBindings(Preset, Machine));
    }

    [Fact]
    public void Different_binding_is_not_a_match()
    {
        Assert.False(ControlsDetector.SameBindings(Preset, Machine.Replace("mouseButton:i=0", "mouseButton:i=1")));
        Assert.False(ControlsDetector.SameBindings(Preset, "content{\n}\n"));
        Assert.False(ControlsDetector.SameBindings("not a preset", Machine));
    }
}

public class VersionTests
{
    [Theory]
    [InlineData(@"C:\Users\me\Desktop\WT-FCSGenerator-v2.0.2-win-x64", "v2.0.2")]
    [InlineData(@"D:\Tools\FCS 2.1", "v2.1")]
    [InlineData(@"D:\Tools\FCS", null)]
    [InlineData(@"D:\Tools\win-x64", null)]
    public void Reads_version_from_folder_name(string dir, string? expected) =>
        Assert.Equal(expected, ExternalToolLocator.VersionFromFolderName(dir));

    [Fact]
    public void Compares_versions_with_or_without_v()
    {
        Assert.True(Versions.Same("v2.2.1", "2.2.1"));
        Assert.False(Versions.Same("v2.2.1", "v2.2.0"));
        Assert.Equal(new Version(1, 2, 3), Versions.Parse("v1.2.3-beta"));
        Assert.Null(Versions.Parse("content-2026.10"));
    }

    [Fact]
    public void Finds_tool_dirs_under_roots()
    {
        using var tmp = new TempDir();
        var dir = Path.Combine(tmp.Path, "a", "WT-FCSGenerator-v2.0.2-win-x64");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "FCS.exe"), "");
        Assert.Equal([dir], ExternalToolLocator.FindToolDirs("fcs.exe", [tmp.Path, Path.Combine(tmp.Path, "missing")]));
    }
}

public class UpdaterTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("v1.0", "1.0.0")]
    [InlineData("v1.2.3-rc.1", "1.2.3")]
    [InlineData("content-2026.10", null)]
    [InlineData("0.2.0", null)]
    public void Parses_release_tags(string tag, string? expected) =>
        Assert.Equal(expected is null ? null : Version.Parse(expected), AppUpdater.ParseTag(tag));

    [Fact]
    public void Reads_sha256sums_lines()
    {
        var a = new string('a', 64);
        var b = new string('B', 64);
        var sums = $"{a}  WTModLauncher.exe\r\n{b} *WTModLauncher-Setup-0.2.0.exe\r\n";
        Assert.Equal(a, AppUpdater.ParseChecksums(sums, "WTModLauncher.exe"));
        Assert.Equal(b.ToLowerInvariant(), AppUpdater.ParseChecksums(sums, "WTModLauncher-Setup-0.2.0.exe"));
        Assert.Null(AppUpdater.ParseChecksums(sums, "other.exe"));
    }

    [Fact]
    public void Follows_the_channel_the_running_build_was_published_on()
    {
        var current = AppUpdater.Current;
        Assert.True(AppUpdater.IsPrereleaseChannel([(current, true)]));
        Assert.False(AppUpdater.IsPrereleaseChannel([(current, false), (new Version(99, 0, 0), true)]));
        Assert.True(AppUpdater.IsPrereleaseChannel([])); // unpublished dev build
    }
}

[Collection("AppPaths")]
public class AdoptionTests
{
    [Fact]
    public async Task Sound_mod_installed_by_hand_is_adopted_with_version_when_banks_match()
    {
        using var root = new TempDir();
        AppPaths.Root = Path.Combine(root.Path, "launcher");
        var game = Path.Combine(root.Path, "game");
        var mod = Path.Combine(game, "sound", "mod");
        Directory.CreateDirectory(mod);
        File.WriteAllText(Path.Combine(game, "config.blk"), "sound{\n  enable_mod:b=yes\n}\n");
        File.WriteAllText(Path.Combine(mod, "a.bank"), "12345");
        File.WriteAllText(Path.Combine(mod, "b.bank"), "12");

        var item = new ModItem
        {
            Id = "rcsm", Type = ModTypes.SoundMod, Version = "2", Name = new() { ["en"] = "RCSM" },
            Files = new() { ["a.bank"] = 5, ["b.bank"] = 2 },
        };
        var installer = new SoundModInstaller(item);
        var ctx = Context(game);

        Assert.Equal(ModStatus.NotInstalled, installer.GetStatus(ctx, "2"));
        Assert.True(await installer.TryAdoptAsync(ctx, "2", CancellationToken.None));
        Assert.Equal(ModStatus.Installed, installer.GetStatus(ctx, "2"));
        Assert.Equal(["sound/mod/a.bank", "sound/mod/b.bank"], ctx.State.Items["rcsm"].Files);
        Assert.True(ctx.State.Items["rcsm"].Adopted);

        // Uninstall then removes the adopted banks like launcher-installed ones.
        await installer.UninstallAsync(ctx, CancellationToken.None);
        Assert.False(Directory.Exists(mod));
    }

    [Fact]
    public async Task Sound_mod_with_other_bank_sizes_is_adopted_as_outdated()
    {
        using var root = new TempDir();
        AppPaths.Root = Path.Combine(root.Path, "launcher");
        var game = Path.Combine(root.Path, "game");
        var mod = Path.Combine(game, "sound", "mod");
        Directory.CreateDirectory(mod);
        File.WriteAllText(Path.Combine(game, "config.blk"), "sound{\n  enable_mod:b=yes\n}\n");
        File.WriteAllText(Path.Combine(mod, "a.bank"), "older build");

        var installer = new SoundModInstaller(new ModItem
        {
            Id = "rcsm", Type = ModTypes.SoundMod, Version = "2", Files = new() { ["a.bank"] = 5, ["b.bank"] = 2 },
        });
        var ctx = Context(game);

        Assert.True(await installer.TryAdoptAsync(ctx, "2", CancellationToken.None));
        Assert.Equal(ModStatus.UpdateAvailable, installer.GetStatus(ctx, "2"));
    }

    [Fact]
    public async Task Nothing_to_adopt_without_fingerprint_or_files()
    {
        using var root = new TempDir();
        AppPaths.Root = Path.Combine(root.Path, "launcher");
        var game = Path.Combine(root.Path, "game");
        Directory.CreateDirectory(Path.Combine(game, "sound", "mod"));

        var withoutFingerprint = new SoundModInstaller(new ModItem { Id = "x", Type = ModTypes.SoundMod });
        var withFingerprint = new SoundModInstaller(new ModItem { Id = "y", Type = ModTypes.SoundMod, Files = new() { ["a.bank"] = 1 } });
        var ctx = Context(game);

        Assert.False(await withoutFingerprint.TryAdoptAsync(ctx, "1", CancellationToken.None));
        Assert.False(await withFingerprint.TryAdoptAsync(ctx, "1", CancellationToken.None));
        Assert.Empty(ctx.State.Items);
    }

    [Fact]
    public async Task Uninstalling_an_external_tool_keeps_its_folder_and_never_readopts_it()
    {
        using var root = new TempDir();
        AppPaths.Root = Path.Combine(root.Path, "launcher");
        var external = Path.Combine(root.Path, "Desktop", "WT-FCSGenerator-v2.0.2-win-x64");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "FCS.exe"), "");

        var tool = new GitHubToolInstaller(new ModItem { Id = "fcs", Type = ModTypes.GitHubTool, Exe = "FCS.exe" });
        var ctx = Context("");
        ctx.State.Items["fcs"] = new InstalledItem { Version = "v2.0.2", Adopted = true, Location = external };

        Assert.Equal(ModStatus.Installed, tool.GetStatus(ctx, "2.0.2"));
        Assert.Equal(ModStatus.UpdateAvailable, tool.GetStatus(ctx, "v2.2.1"));
        Assert.Equal(Path.Combine(external, "FCS.exe"), tool.ExePath(ctx));

        await tool.UninstallAsync(ctx, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(external, "FCS.exe")));
        Assert.Contains(external, ctx.State.IgnoredPaths);
        Assert.Equal(ModStatus.NotInstalled, tool.GetStatus(ctx, "v2.2.1"));
    }

    private static InstallContext Context(string game) =>
        new(game, new LauncherState(), new HttpClient(), new Progress<InstallProgress>(), _ => { });
}
