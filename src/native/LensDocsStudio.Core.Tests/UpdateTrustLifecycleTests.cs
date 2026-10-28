using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Updates;

namespace LensDocsStudio.Core.Tests;

// Ephemeral manifest keys only. These checks do not sign a Windows executable or install trust.
public sealed class UpdateTrustLifecycleTests
{
    private static readonly Version Windows = new(10, 0, 22621);
    private static readonly PublisherIdentity[] Publishers = [new("CN=Disposable test publisher", new string('A', 64))];

    [Fact]
    public void Manifest_key_rollover_requires_a_previously_trusted_bridge()
    {
        using var oldKey = RSA.Create(2048);
        using var nextKey = RSA.Create(2048);
        var oldPolicy = new UpdatePolicy(new(1, new() { ["old"] = oldKey.ExportSubjectPublicKeyInfoPem() }, Publishers));
        var bridgePolicy = new UpdatePolicy(new(1, new()
        {
            ["old"] = oldKey.ExportSubjectPublicKeyInfoPem(), ["next"] = nextKey.ExportSubjectPublicKeyInfoPem()
        }, Publishers));
        var retiredPolicy = new UpdatePolicy(new(1, new() { ["next"] = nextKey.ExportSubjectPublicKeyInfoPem() }, Publishers));
        var manifest = Manifest("1.1.0", "Stable");
        Assert.True(Evaluate(oldPolicy, manifest, Sign(manifest, "old", oldKey))!.Trusted);
        Assert.False(Evaluate(oldPolicy, manifest, Sign(manifest, "next", nextKey))!.Trusted);
        Assert.True(Evaluate(bridgePolicy, manifest, Sign(manifest, "old", oldKey))!.Trusted);
        Assert.True(Evaluate(bridgePolicy, manifest, Sign(manifest, "next", nextKey))!.Trusted);
        Assert.False(Evaluate(retiredPolicy, manifest, Sign(manifest, "old", oldKey))!.Trusted);
        Assert.True(Evaluate(retiredPolicy, manifest, Sign(manifest, "next", nextKey))!.Trusted);
        Assert.Null(Evaluate(bridgePolicy, manifest, Sign(manifest, "old", nextKey)));
    }

    [Fact]
    public void Trusted_manifest_without_publisher_pins_cannot_bootstrap_installation()
    {
        using var key = RSA.Create(2048);
        var policy = new UpdatePolicy(new(1, new() { ["key"] = key.ExportSubjectPublicKeyInfoPem() }, []));
        var manifest = Manifest("1.1.0", "Stable");
        Assert.False(Evaluate(policy, manifest, Sign(manifest, "key", key))!.Trusted);
        var embedded = UpdateTrustConfiguration.Embedded();
        Assert.Empty(embedded.ManifestKeys);
        Assert.Empty(embedded.Publishers);
    }

    [Theory]
    [InlineData("1.0.0-preview.999", "1.0.0-rc.1", "Preview", "Preview", true)]
    [InlineData("1.0.0-rc.1", "1.0.0", "Preview", "Stable", true)]
    [InlineData("1.0.0", "1.0.1-rc.1", "Stable", "Preview", false)]
    [InlineData("1.0.0", "1.0.0-rc.2", "Preview", "Preview", false)]
    [InlineData("1.0.0", "1.0.0", "Stable", "Stable", false)]
    public void Release_candidate_selection_preserves_channels_and_prevents_replay(
        string installed, string offered, string selectedChannel, string offeredChannel, bool expected)
    {
        using var key = RSA.Create(2048);
        var policy = new UpdatePolicy(new(1, new() { ["key"] = key.ExportSubjectPublicKeyInfoPem() }, Publishers));
        var manifest = Manifest(offered, offeredChannel);
        var result = policy.Evaluate(Sign(manifest, "key", key), Release(manifest), installed, selectedChannel, "x64", Windows);
        Assert.Equal(expected, result?.Trusted == true);
        Assert.Null(policy.Evaluate(Sign(manifest, "key", key), Release(manifest) with { Draft = true }, installed, selectedChannel, "x64", Windows));
        Assert.Null(policy.Evaluate(Sign(manifest, "key", key), Release(manifest) with { Prerelease = offeredChannel != "Preview" }, installed, selectedChannel, "x64", Windows));
    }

    private static UpdateManifest Manifest(string version, string channel) => new(1, UpdatePolicy.ProductId,
        version, channel, "x64", "10.0.17763", "native-v" + version,
        $"LensDocsStudio.Native-{version}-win-x64-Setup.exe", 5, new string('B', 64));

    private static NativeRelease Release(UpdateManifest manifest) => new(manifest.ReleaseTag,
        UpdatePolicy.ReleasePage(manifest.ReleaseTag), manifest.Channel == "Preview", false, "Synthetic release",
        [new(manifest.AssetName, UpdatePolicy.AssetUri(manifest.ReleaseTag, manifest.AssetName), manifest.Size)]);

    private static byte[] Sign(UpdateManifest manifest, string id, RSA key)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, UpdatePolicy.Json);
        return JsonSerializer.SerializeToUtf8Bytes(new SignedUpdateEnvelope(1, Convert.ToBase64String(payload), id,
            Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), UpdatePolicy.Json);
    }

    private static UpdateCandidate? Evaluate(UpdatePolicy policy, UpdateManifest manifest, byte[] envelope) =>
        policy.Evaluate(envelope, Release(manifest), "1.0.0", "Stable", "x64", Windows);
}
