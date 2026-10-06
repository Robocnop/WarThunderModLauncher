using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using WTModLauncher.Core;

namespace WTModLauncher.Tests;

public class BlkEditorTests
{
    private const string Config =
        "graphics{\r\n  ssaa:b=no\r\n}\r\nsound{\r\n  speakerMode:t=\"auto\"\r\n  fmod_sound_enable:b=yes\r\n}\r\nvideo{\r\n  vsync:b=no\r\n}\r\n";

    [Fact]
    public void Adds_enable_mod_inside_existing_sound_block_only()
    {
        var updated = BlkEditor.SetBool(Config, "sound", "enable_mod", true);

        Assert.Equal(true, BlkEditor.GetBool(updated, "sound", "enable_mod"));
        Assert.Contains("sound{\r\n  enable_mod:b=yes\r\n  speakerMode", updated);
        Assert.Equal(Config.Length + "  enable_mod:b=yes\r\n".Length, updated.Length);
        Assert.Null(BlkEditor.GetBool(updated, "video", "enable_mod"));
    }

    [Fact]
    public void Toggles_existing_value_without_touching_the_rest()
    {
        var on = BlkEditor.SetBool(Config, "sound", "enable_mod", true);
        var off = BlkEditor.SetBool(on, "sound", "enable_mod", false);

        Assert.Equal(false, BlkEditor.GetBool(off, "sound", "enable_mod"));
        Assert.Equal(on.Replace("enable_mod:b=yes", "enable_mod:b=no"), off);
    }

    [Fact]
    public void Ignores_nested_blocks_and_braces_in_strings()
    {
        const string text = "controls{\n  sound{\n    enable_mod:b=yes\n  }\n  name:t=\"sound{ }\"\n}\n";
        Assert.Null(BlkEditor.GetBool(text, "sound", "enable_mod"));

        var updated = BlkEditor.SetBool(text, "sound", "enable_mod", true);
        Assert.StartsWith(text, updated); // appended as a new top-level block
        Assert.Equal(true, BlkEditor.GetBool(updated, "sound", "enable_mod"));
    }

    [Fact]
    public void Does_not_match_block_with_longer_name()
    {
        const string text = "soundExtra{\n  enable_mod:b=yes\n}\n";
        Assert.Null(BlkEditor.GetBool(text, "sound", "enable_mod"));
    }
}

public class GameLocatorTests
{
    [Fact]
    public void Parses_steam_library_folders_and_installdir()
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"236390\"\t\t\"1\"\n\t\t}\n\t}\n}";
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], GameLocator.ParseLibraryFolders(vdf));
        Assert.Equal("War Thunder", GameLocator.ParseAcfInstallDir("\"AppState\"\n{\n\t\"installdir\"\t\t\"War Thunder\"\n}"));
    }

    [Fact]
    public void Validates_game_dir_by_config_and_exe()
    {
        using var tmp = new TempDir();
        Assert.False(GameLocator.IsValidGameDir(tmp.Path));
        File.WriteAllText(Path.Combine(tmp.Path, "config.blk"), "");
        Directory.CreateDirectory(Path.Combine(tmp.Path, "win64"));
        File.WriteAllText(Path.Combine(tmp.Path, "win64", "aces.exe"), "");
        Assert.True(GameLocator.IsValidGameDir(tmp.Path));
    }
}

/// <summary>Tests that point the static AppPaths.Root at a temp dir must not run in parallel.</summary>
[CollectionDefinition("AppPaths", DisableParallelization = true)]
public class AppPathsCollection;

[Collection("AppPaths")]
public class SoundModInstallerTests
{
    [Fact]
    public async Task Install_then_uninstall_round_trips_game_folder()
    {
        using var root = new TempDir();
        AppPaths.Root = Path.Combine(root.Path, "launcher");
        var game = Path.Combine(root.Path, "game");
        Directory.CreateDirectory(Path.Combine(game, "win64"));
        File.WriteAllText(Path.Combine(game, "win64", "aces.exe"), "");
        const string original = "sound{\n  fmod_sound_enable:b=yes\n}\n";
        File.WriteAllText(Path.Combine(game, "config.blk"), original);

        // A pre-verified file in the cache means the downloader never touches the network.
        Directory.CreateDirectory(AppPaths.Cache);
        var zipPath = Path.Combine(AppPaths.Cache, "mod.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddEntry(zip, "tanks_engines.bank", "a");
            AddEntry(zip, "sub/aircraft_guns.assets.bank", "b");
            AddEntry(zip, "readme.txt", "ignored");
        }
        var item = new ModItem
        {
            Id = "rcsm", Type = ModTypes.SoundMod, Version = "1", Url = "https://invalid.example/mod.zip",
            FileName = "mod.zip", Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zipPath))),
            Name = new() { ["en"] = "RCSM" },
        };

        var installer = new SoundModInstaller(item);
        var ctx = new InstallContext(game, new LauncherState(), new HttpClient(), new Progress<InstallProgress>(), _ => { });

        Assert.Equal(ModStatus.NotInstalled, installer.GetStatus(ctx, "1"));
        await installer.InstallAsync(ctx, "1", CancellationToken.None);

        var modDir = Path.Combine(game, "sound", "mod");
        Assert.True(File.Exists(Path.Combine(modDir, "tanks_engines.bank")));
        Assert.True(File.Exists(Path.Combine(modDir, "aircraft_guns.assets.bank")));
        Assert.False(File.Exists(Path.Combine(modDir, "readme.txt")));
        Assert.True(ConfigBlk.IsSoundModEnabled(game));
        Assert.True(File.Exists(Path.Combine(game, "config.blk.modlauncher.bak")));
        Assert.Equal(ModStatus.Installed, installer.GetStatus(ctx, "1"));
        Assert.Equal(ModStatus.UpdateAvailable, installer.GetStatus(ctx, "2"));

        // A game update resetting config.blk must be detected and repaired without re-downloading.
        File.WriteAllText(Path.Combine(game, "config.blk"), original);
        Assert.Equal(ModStatus.NeedsRepair, installer.GetStatus(ctx, "1"));
        await installer.RepairAsync(ctx, "1", CancellationToken.None);
        Assert.Equal(ModStatus.Installed, installer.GetStatus(ctx, "1"));

        await installer.UninstallAsync(ctx, CancellationToken.None);
        Assert.False(Directory.Exists(modDir));
        Assert.False(ConfigBlk.IsSoundModEnabled(game));
        Assert.Equal(ModStatus.NotInstalled, installer.GetStatus(ctx, "1"));
    }

    [Fact]
    public void Extract_rejects_zip_slip_entries()
    {
        using var root = new TempDir();
        var zipPath = Path.Combine(root.Path, "evil.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) AddEntry(zip, "../../evil.exe", "x");

        var dest = Path.Combine(root.Path, "out");
        Assert.Throws<InvalidDataException>(() =>
            ModInstaller.Extract(zipPath, dest, dest, _ => true, flatten: false, null, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(root.Path, "evil.exe")));
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open());
        w.Write(content);
    }
}

public class ManifestTests
{
    [Fact]
    public void Embedded_manifest_parses_and_every_type_has_an_installer()
    {
        var m = ManifestService.LoadEmbedded();
        Assert.Equal(["rcsm", "fcs", "robo-config"], m.Items.Select(i => i.Id));
        foreach (var item in m.Items) Assert.NotNull(ModInstaller.Create(item));
        Assert.All(m.Items.Where(i => i.Type != ModTypes.GitHubTool), i =>
        {
            Assert.StartsWith("https://github.com/Robocnop/WarThunderModLauncher/releases/download/", i.Url);
            Assert.Equal(64, i.Sha256!.Length);
        });
        // The sound mod carries the bank fingerprint used to recognise a copy installed by hand.
        Assert.Equal(27, m.Items.Single(i => i.Id == "rcsm").Files!.Count);
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("wtml-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
