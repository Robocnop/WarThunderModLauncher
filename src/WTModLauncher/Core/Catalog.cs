using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace WTModLauncher.Core;

public static class ModTypes
{
    public const string SoundMod = "soundmod";
    public const string Controls = "controls";
    public const string GitHubTool = "github-tool";
}

public sealed class ModItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Version { get; set; } = "";
    public Dictionary<string, string> Name { get; set; } = [];
    public Dictionary<string, string> Description { get; set; } = [];
    public string? Author { get; set; }
    public string? Homepage { get; set; }
    /// <summary>Segoe MDL2 Assets glyph shown on the card.</summary>
    public string? Icon { get; set; }

    // Direct-file items (soundmod, controls)
    public string? Url { get; set; }
    public string? FileName { get; set; }
    public string? Sha256 { get; set; }
    public long? Size { get; set; }
    /// <summary>File name → size of what the item installs (soundmod banks): recognises a copy installed without the launcher.</summary>
    public Dictionary<string, long>? Files { get; set; }

    // github-tool items: resolved at runtime from the repo's latest release
    public string? Repo { get; set; }
    public string? AssetPattern { get; set; }
    public string? Exe { get; set; }
}

public sealed class Manifest
{
    public int Schema { get; set; } = 1;
    public string ContentVersion { get; set; } = "";
    public List<ModItem> Items { get; set; } = [];
}

public static class Http
{
    public const string Repo = "Robocnop/WarThunderModLauncher";

    public static HttpClient Create()
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(15),
        })
        { Timeout = Timeout.InfiniteTimeSpan };
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0";
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WTModLauncher", version));
        return http;
    }

    /// <summary>GET a GitHub API url as JSON, with a per-call timeout (the shared client has none for big downloads).</summary>
    public static async Task<JsonDocument> GetGitHubJsonAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var res = await http.SendAsync(req, cts.Token);
        res.EnsureSuccessStatusCode();
        await using var s = await res.Content.ReadAsStreamAsync(cts.Token);
        return await JsonDocument.ParseAsync(s, cancellationToken: cts.Token);
    }
}

public static class ManifestService
{
    public const string ContentTagPrefix = "content-";

    /// <summary>Latest manifest from the newest "content-*" GitHub release, or the embedded copy when offline.</summary>
    public static async Task<(Manifest Manifest, string Source)> LoadAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            using var releases = await Http.GetGitHubJsonAsync(http, $"https://api.github.com/repos/{Http.Repo}/releases?per_page=30", ct);
            foreach (var rel in releases.RootElement.EnumerateArray())
            {
                var tag = rel.GetProperty("tag_name").GetString() ?? "";
                if (!tag.StartsWith(ContentTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.GetProperty("draft").GetBoolean()) continue;
                foreach (var asset in rel.GetProperty("assets").EnumerateArray())
                {
                    if (asset.GetProperty("name").GetString() != "manifest.json") continue;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(20));
                    var json = await http.GetStringAsync(asset.GetProperty("browser_download_url").GetString(), cts.Token);
                    return (Parse(json), tag);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { /* offline, rate-limited or no content release yet */ }

        return (LoadEmbedded(), "embedded");
    }

    public static Manifest Parse(string json) =>
        JsonSerializer.Deserialize<Manifest>(json, StateStore.Json) ?? throw new InvalidDataException("Empty manifest");

    public static Manifest LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith("default-manifest.json", StringComparison.Ordinal));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }
}
