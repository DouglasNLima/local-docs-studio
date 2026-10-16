using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Updates;

namespace LensDocsStudio.Core.Tests;

public sealed class UpdateTests : IDisposable
{
    private readonly RSA key = RSA.Create(2048);
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-update-test-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] installer = [1, 2, 3, 4, 5];
    private readonly UpdateEnvironment environment = new("1.0.0", "Stable", "x64", new Version(10, 0, 22621));
    private UpdatePolicy Policy => new(new(1, new() { ["test"] = key.ExportSubjectPublicKeyInfoPem() }, [new("CN=Test", new string('A', 64))]));
    private UpdateManifest Manifest => new(1, UpdatePolicy.ProductId, "1.1.0", "Stable", "x64", "10.0.17763", "native-v1.1.0", "LensDocsStudio.Native-1.1.0-win-x64-Setup.exe", installer.Length, Convert.ToHexString(SHA256.HashData(installer)));
    private NativeRelease Release(UpdateManifest manifest) => new(manifest.ReleaseTag, UpdatePolicy.ReleasePage(manifest.ReleaseTag), manifest.Channel == "Preview", false, "Release notes", [new(manifest.AssetName, UpdatePolicy.AssetUri(manifest.ReleaseTag, manifest.AssetName), manifest.Size)]);
    private byte[] Envelope(UpdateManifest manifest)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, UpdatePolicy.Json);
        return JsonSerializer.SerializeToUtf8Bytes(new SignedUpdateEnvelope(1, Convert.ToBase64String(payload), "test", Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), UpdatePolicy.Json);
    }
    private UpdateCandidate? Evaluate(UpdateManifest manifest) => Policy.Evaluate(Envelope(manifest), Release(manifest), environment.CurrentVersion, environment.Channel, environment.Architecture, environment.WindowsVersion);

    [Theory]
    [InlineData("1.0.0-preview.2", "1.0.0-preview.10")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.0.0-1", "1.0.0-alpha")]
    public void Semantic_versions_sort_by_precedence(string lower, string higher)
    {
        Assert.True(SemanticVersion.TryParse(lower, out var first)); Assert.True(SemanticVersion.TryParse(higher, out var second));
        Assert.True(first.CompareTo(second) < 0);
    }
    [Theory]
    [InlineData("v1.0.0")][InlineData("1.0")][InlineData("01.0.0")][InlineData("1.0.0-01")][InlineData("1.0.0-preview..1")]
    public void Invalid_versions_are_rejected(string value) => Assert.False(SemanticVersion.TryParse(value, out _));
    [Fact]
    public void Signed_compatible_native_release_is_eligible() => Assert.True(Evaluate(Manifest)!.Trusted);
    [Fact]
    public void Legacy_product_downgrade_wrong_architecture_and_newer_windows_are_rejected()
    {
        Assert.Null(Evaluate(Manifest with { ProductId = "LensDocsStudio.Windows" }));
        Assert.Null(Evaluate(Manifest with { Version = "0.9.0" }));
        Assert.Null(Evaluate(Manifest with { Architecture = "arm64" }));
        Assert.Null(Evaluate(Manifest with { MinimumWindowsVersion = "10.0.99999" }));
        Assert.Null(Evaluate(Manifest with { Channel = "Preview", Version = "1.1.0-preview.1" }));
        Assert.Null(Evaluate(Manifest with { Sha256 = "invalid" }));
    }
    [Fact]
    public void Tampering_with_signed_metadata_prevents_an_offer()
    {
        var envelope = JsonSerializer.Deserialize<SignedUpdateEnvelope>(Envelope(Manifest), UpdatePolicy.Json)!;
        var tampered = envelope with { Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Manifest with { Sha256 = new string('A', 64) }, UpdatePolicy.Json)) };
        Assert.Null(Policy.Evaluate(JsonSerializer.SerializeToUtf8Bytes(tampered, UpdatePolicy.Json), Release(Manifest), "1.0.0", "Stable", "x64", environment.WindowsVersion));
    }
    [Fact]
    public async Task Unsigned_preview_can_only_offer_a_release_page()
    {
        var policy = new UpdatePolicy(new(1, [], []));
        var candidate = policy.Evaluate(Envelope(Manifest), Release(Manifest), "1.0.0", "Stable", "x64", environment.WindowsVersion)!;
        Assert.False(candidate.Trusted);
        using var transport = new UpdateTransport(new FakeHandler(installer));
        await Assert.ThrowsAsync<IOException>(() => new UpdateService(transport, policy, new FakeTrust()).DownloadAsync(candidate, environment, root));
        Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task Download_requires_hash_and_publisher_validation_and_rechecks_changed_bytes()
    {
        using var transport = new UpdateTransport(new FakeHandler(installer)); var trust = new FakeTrust();
        var service = new UpdateService(transport, Policy, trust);
        var result = await service.DownloadAsync(Evaluate(Manifest)!, environment, root);
        Assert.Equal(installer, await File.ReadAllBytesAsync(result.Path)); Assert.Equal(1, trust.Checks);
        await File.WriteAllBytesAsync(result.Path, [5, 4, 3, 2, 1]);
        await Assert.ThrowsAsync<IOException>(() => service.VerifyAsync(result, environment));
        Assert.Equal(1, trust.Checks);
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Corrupt_download_or_untrusted_publisher_never_leaves_an_executable(bool corrupt)
    {
        using var transport = new UpdateTransport(new FakeHandler(corrupt ? [5, 4, 3, 2, 1] : installer));
        var service = new UpdateService(transport, Policy, new FakeTrust { Reject = !corrupt });
        await Assert.ThrowsAsync<IOException>(() => service.DownloadAsync(Evaluate(Manifest)!, environment, root));
        Assert.Empty(Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task Cancellation_during_signature_verification_discards_the_download()
    {
        using var cancellation = new CancellationTokenSource();
        using var transport = new UpdateTransport(new FakeHandler(installer));
        var trust = new FakeTrust { AfterVerify = cancellation.Cancel };
        var service = new UpdateService(transport, Policy, trust);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(Evaluate(Manifest)!, environment, root, cancellationToken: cancellation.Token));
        Assert.Equal(1, trust.Checks);
        Assert.Empty(Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task Cancellation_at_final_verification_does_not_accept_the_cached_installer()
    {
        using var cancellation = new CancellationTokenSource();
        using var transport = new UpdateTransport(new FakeHandler(installer));
        var trust = new FakeTrust(); var service = new UpdateService(transport, Policy, trust);
        var downloaded = await service.DownloadAsync(Evaluate(Manifest)!, environment, root);
        trust.AfterVerify = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.VerifyAsync(downloaded, environment, cancellation.Token));
        Assert.Equal(2, trust.Checks);
        Assert.Equal(installer, await File.ReadAllBytesAsync(downloaded.Path));
    }
    [Fact]
    public async Task Redirects_to_unrelated_hosts_are_rejected()
    {
        using var transport = new UpdateTransport(new FakeHandler(installer) { Redirect = new Uri("https://example.com/installer.exe") });
        await Assert.ThrowsAsync<IOException>(() => transport.ReadAsync(UpdatePolicy.AssetUri("native-v1.1.0", "native-update.json"), 65536, default));
    }
    [Fact]
    public void Automatic_checks_wait_at_least_twenty_four_hours()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(UpdatePolicy.IsCheckDue(now, null)); Assert.False(UpdatePolicy.IsCheckDue(now, now.AddHours(-23)));
        Assert.False(UpdatePolicy.IsCheckDue(now, now.AddDays(1))); Assert.True(UpdatePolicy.IsCheckDue(now, now.AddHours(-24)));
    }
    private sealed class FakeHandler(byte[] bytes) : HttpMessageHandler
    {
        public Uri? Redirect { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(Redirect is null ? HttpStatusCode.OK : HttpStatusCode.Redirect) { Content = new ByteArrayContent(bytes) };
            response.Headers.Location = Redirect; return Task.FromResult(response);
        }
    }
    private sealed class FakeTrust : IInstallerTrustVerifier
    {
        public bool Reject { get; init; } public int Checks { get; private set; }
        public Action? AfterVerify { get; set; }
        public Task VerifyAsync(string path, IReadOnlyList<PublisherIdentity> publishers, CancellationToken cancellationToken)
        { Checks++; if (Reject) throw new IOException("Untrusted test publisher"); AfterVerify?.Invoke(); return Task.CompletedTask; }
    }
    public void Dispose() { key.Dispose(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
