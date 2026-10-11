using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Updates;

public sealed record UpdateEnvironment(string CurrentVersion, string Channel, string Architecture, Version WindowsVersion);
public sealed record DownloadedUpdate(string Path, UpdateCandidate Candidate);

public sealed class UpdateService(UpdateTransport transport, UpdatePolicy policy, IInstallerTrustVerifier installerTrust)
{
    public async Task<UpdateCandidate?> CheckAsync(UpdateEnvironment environment, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var data = await transport.ReadAsync(new Uri($"https://api.github.com/repos/{UpdatePolicy.Repository}/releases?per_page=100"), 4 * 1024 * 1024, deadline.Token);
        using var document = JsonDocument.Parse(data);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new IOException("GitHub did not return a release list.");
        var candidates = new List<UpdateCandidate>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var tag = String(item, "tag_name");
            if (tag is null || !tag.StartsWith("native-v", StringComparison.Ordinal) || !SemanticVersion.TryParse(tag[8..], out var version) ||
                !SemanticVersion.TryParse(environment.CurrentVersion, out var current) || version.CompareTo(current) <= 0 || Boolean(item, "draft")) continue;
            if (!Uri.TryCreate(String(item, "html_url"), UriKind.Absolute, out var page) || page != UpdatePolicy.ReleasePage(tag)) continue;
            if (!item.TryGetProperty("assets", out var list) || list.ValueKind != JsonValueKind.Array) continue;
            var assets = new List<ReleaseAsset>();
            foreach (var asset in list.EnumerateArray())
                if (String(asset, "name") is { } name && Uri.TryCreate(String(asset, "browser_download_url"), UriKind.Absolute, out var url) &&
                    asset.TryGetProperty("size", out var length) && length.TryGetInt64(out var size)) assets.Add(new(name, url, size));
            var metadataAssets = assets.Where(a => a.Name == "native-update.json").ToArray();
            if (metadataAssets.Length != 1) continue;
            var metadata = metadataAssets[0];
            if (metadata.Size is <= 0 or > 65536 || metadata.DownloadUri != UpdatePolicy.AssetUri(tag, metadata.Name)) continue;
            var release = new NativeRelease(tag, page, Boolean(item, "prerelease"), false, String(item, "body") ?? "", assets);
            var envelope = await transport.ReadAsync(metadata.DownloadUri, 65536, deadline.Token);
            var candidate = policy.Evaluate(envelope, release, environment.CurrentVersion, environment.Channel, environment.Architecture, environment.WindowsVersion);
            if (candidate is not null) candidates.Add(candidate);
        }
        return candidates.OrderByDescending(c => { SemanticVersion.TryParse(c.Manifest.Version, out var parsed); return parsed; }).FirstOrDefault();
    }

    public async Task<DownloadedUpdate> DownloadAsync(UpdateCandidate candidate, UpdateEnvironment environment, string cacheDirectory,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        candidate = Revalidate(candidate, environment);
        WorkspacePath.RejectReparsePoints(cacheDirectory);
        Directory.CreateDirectory(cacheDirectory);
        var folder = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, candidate.Manifest.AssetName);
        try
        {
            using var response = await transport.OpenAsync(candidate.Installer.DownloadUri, cancellationToken);
            if (response.Content.Headers.ContentLength is { } length && length != candidate.Manifest.Size) throw new IOException("The update download size does not match its signed manifest.");
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[65536]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += count;
                    if (total > candidate.Manifest.Size) throw new IOException("The update download exceeds its signed size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    progress?.Report((double)total / candidate.Manifest.Size);
                }
                if (total != candidate.Manifest.Size) throw new IOException("The update download is incomplete.");
                output.Flush(true);
            }
            var result = new DownloadedUpdate(path, candidate);
            await VerifyAsync(result, environment, cancellationToken);
            return result;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public async Task VerifyAsync(DownloadedUpdate update, UpdateEnvironment environment, CancellationToken cancellationToken = default)
    {
        var candidate = Revalidate(update.Candidate, environment);
        WorkspacePath.RejectReparsePoints(update.Path);
        // Keep the file locked against replacement while hash and Authenticode are checked.
        await using var file = new FileStream(update.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length != candidate.Manifest.Size) throw new IOException("The downloaded installer has changed.");
        var hash = await SHA256.HashDataAsync(file, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(candidate.Manifest.Sha256))) throw new IOException("The installer checksum does not match its signed manifest.");
        await installerTrust.VerifyAsync(update.Path, policy.Trust.Publishers, cancellationToken);
    }
    private UpdateCandidate Revalidate(UpdateCandidate candidate, UpdateEnvironment environment)
    {
        var validated = policy.Evaluate(candidate.EnvelopeBytes, candidate.Release, environment.CurrentVersion, environment.Channel, environment.Architecture, environment.WindowsVersion);
        return validated is { Trusted: true } ? validated : throw new IOException("This internal preview has no trusted update signing identity. Open the release page to review available downloads.");
    }
    private static string? String(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Boolean(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
}
