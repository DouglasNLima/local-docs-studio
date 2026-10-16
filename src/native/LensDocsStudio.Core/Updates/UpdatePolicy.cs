using System.Security.Cryptography;
using System.Text.Json;

namespace LensDocsStudio.Core.Updates;

public sealed record UpdateManifest(int SchemaVersion, string ProductId, string Version, string Channel, string Architecture,
    string MinimumWindowsVersion, string ReleaseTag, string AssetName, long Size, string Sha256);
public sealed record SignedUpdateEnvelope(int SchemaVersion, string Payload, string KeyId, string Signature);
public sealed record PublisherIdentity(string Subject, string CertificateSha256);
public sealed record UpdateTrustConfiguration(int SchemaVersion, Dictionary<string, string> ManifestKeys, PublisherIdentity[] Publishers)
{
    public static UpdateTrustConfiguration Embedded()
    {
        using var stream = typeof(UpdateTrustConfiguration).Assembly.GetManifestResourceStream("LensDocsStudio.Core.Updates.update-trust.json")
            ?? throw new InvalidOperationException("The update trust policy is missing.");
        return JsonSerializer.Deserialize<UpdateTrustConfiguration>(stream, UpdatePolicy.Json) ?? throw new InvalidDataException("The update trust policy is invalid.");
    }
}
public sealed record ReleaseAsset(string Name, Uri DownloadUri, long Size);
public sealed record NativeRelease(string Tag, Uri Page, bool Prerelease, bool Draft, string Notes, IReadOnlyList<ReleaseAsset> Assets);
public sealed record UpdateCandidate(UpdateManifest Manifest, NativeRelease Release, ReleaseAsset Installer, bool Trusted, byte[] EnvelopeBytes);
public interface IInstallerTrustVerifier
{
    Task VerifyAsync(string path, IReadOnlyList<PublisherIdentity> publishers, CancellationToken cancellationToken);
}

public sealed class UpdatePolicy(UpdateTrustConfiguration trust)
{
    public const string ProductId = "LensDocsStudio.Native";
    public const string Repository = "DouglasNLima/local-docs-studio";
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    public UpdateTrustConfiguration Trust => trust;
    public UpdateCandidate? Evaluate(byte[] envelopeBytes, NativeRelease release, string currentVersion, string channel, string architecture, Version windowsVersion)
    {
        if (envelopeBytes.Length > 65536 || release.Draft || channel is not ("Stable" or "Preview")) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<SignedUpdateEnvelope>(envelopeBytes, Json);
            if (envelope is not { SchemaVersion: 1, Payload: not null }) return null;
            var payload = Convert.FromBase64String(envelope.Payload);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(payload, Json);
            if (manifest is not { SchemaVersion: 1, ProductId: ProductId } || manifest.Architecture != architecture || manifest.Channel is not ("Stable" or "Preview")) return null;
            if (!SemanticVersion.TryParse(currentVersion, out var current) || !SemanticVersion.TryParse(manifest.Version, out var available) || available.CompareTo(current) <= 0) return null;
            if (channel == "Stable" && manifest.Channel != "Stable") return null;
            if ((manifest.Channel == "Preview") != (available.Prerelease.Length > 0) || release.Prerelease != (manifest.Channel == "Preview")) return null;
            if (!Version.TryParse(manifest.MinimumWindowsVersion, out var minimum) || minimum.Major < 10 || windowsVersion < minimum) return null;
            if (manifest.ReleaseTag != "native-v" + manifest.Version || release.Tag != manifest.ReleaseTag || release.Page != ReleasePage(release.Tag)) return null;
            if (manifest.AssetName != $"LensDocsStudio.Native-{manifest.Version}-win-{architecture}-Setup.exe" || manifest.Size is <= 0 or > 536870912) return null;
            if (manifest.Sha256 is null || manifest.Sha256.Length != 64 || !manifest.Sha256.All(char.IsAsciiHexDigit)) return null;
            var installer = release.Assets.SingleOrDefault(a => a.Name == manifest.AssetName);
            if (installer is null || installer.Size != manifest.Size || installer.DownloadUri != AssetUri(release.Tag, manifest.AssetName)) return null;
            var trusted = false;
            if (envelope.KeyId is not null && trust.ManifestKeys.TryGetValue(envelope.KeyId, out var key))
            {
                using var rsa = RSA.Create(); rsa.ImportFromPem(key);
                trusted = rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                if (!trusted) return null;
            }
            return new(manifest, release, installer, trusted && trust.Publishers.Length > 0, envelopeBytes);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or ArgumentException or InvalidOperationException) { return null; }
    }
    public static Uri ReleasePage(string tag) => new($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}");
    public static Uri AssetUri(string tag, string name) => new($"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}");
    public static bool IsCheckDue(DateTimeOffset now, DateTimeOffset? previous) => previous is null || now - previous >= TimeSpan.FromHours(24);
}
