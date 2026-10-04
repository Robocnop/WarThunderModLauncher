using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace WTModLauncher.Core;

public readonly record struct TransferProgress(long Done, long? Total, double BytesPerSecond);

public static class Downloader
{
    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="destination"/> through a resumable ".part" file,
    /// then checks the SHA-256 when one is given. Returns immediately if a verified copy already exists.
    /// </summary>
    public static async Task DownloadAsync(HttpClient http, string url, string destination, string? sha256,
        IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination) && sha256 is not null && await HashMatchesAsync(destination, sha256, ct))
            return;

        var part = destination + ".part";
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0) req.Headers.Range = new RangeHeaderValue(existing, null);

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // The .part is already complete (or bogus): start over.
            File.Delete(part);
            await DownloadAsync(http, url, destination, sha256, progress, ct);
            return;
        }
        res.EnsureSuccessStatusCode();

        var resumed = res.StatusCode == HttpStatusCode.PartialContent;
        if (!resumed) existing = 0;
        long? total = res.Content.Headers.ContentLength is { } len ? len + existing : null;

        await using (var src = await res.Content.ReadAsStreamAsync(ct))
        await using (var dst = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
        {
            var buffer = new byte[1 << 16];
            var done = existing;
            var started = DateTime.UtcNow;
            var lastReport = DateTime.MinValue;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 100)
                {
                    lastReport = now;
                    var secs = Math.Max((now - started).TotalSeconds, 0.001);
                    progress?.Report(new TransferProgress(done, total, (done - existing) / secs));
                }
            }
            progress?.Report(new TransferProgress(done, total, 0));
        }

        if (sha256 is not null && !await HashMatchesAsync(part, sha256, ct))
        {
            File.Delete(part);
            throw new InvalidDataException(Loc.Get("Msg.ChecksumMismatch"));
        }
        File.Move(part, destination, overwrite: true);
    }

    public static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
    }

    private static async Task<bool> HashMatchesAsync(string path, string expected, CancellationToken ct) =>
        string.Equals(await Sha256Async(path, ct), expected, StringComparison.OrdinalIgnoreCase);
}
