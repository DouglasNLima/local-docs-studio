using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Updates;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

/// <summary>Combines signed metadata and downloads with Windows trust using the disposable CI certificate.</summary>
internal static class NativeUpdatePipelineChecks
{
    public static void Run(string directory, PublisherIdentity identity) => RunAsync(directory, identity).GetAwaiter().GetResult();

    private static async Task RunAsync(string directory, PublisherIdentity identity)
    {
        var root = Path.Combine(directory, "update-pipeline"); Directory.CreateDirectory(root);
        var signedPath = Path.Combine(directory, "signed-test-fixture.exe");
        var signedBytes = await File.ReadAllBytesAsync(signedPath);
        var sourceHash = SHA256.HashData(signedBytes);
        var productionTrust = JsonSerializer.SerializeToUtf8Bytes(UpdateTrustConfiguration.Embedded(), UpdatePolicy.Json);
        using var key = RSA.Create(2048);
        var environment = new UpdateEnvironment("1.0.0", "Stable", "x64", Environment.OSVersion.Version);
        var trustedPolicy = Policy(identity);
        var checks = new List<string>();

        using (var transport = Transport(signedBytes, out var fixture))
        {
            var verifier = new CountingVerifier();
            var service = new UpdateService(transport, trustedPolicy, verifier);
            var candidate = await service.CheckAsync(environment) ?? throw new IOException("Signed fixture discovery produced no update.");
            if (!candidate.Trusted || fixture.InstallerRequests != 0 || verifier.Calls != 0)
                throw new IOException("Discovery downloaded executable bytes or bypassed manifest trust.");
            checks.Add("Valid signed release discovery offers a trusted candidate without downloading or executing it");
            var downloaded = await service.DownloadAsync(candidate, environment, Path.Combine(root, "accepted"));
            if (!(await File.ReadAllBytesAsync(downloaded.Path)).SequenceEqual(signedBytes) || verifier.Calls != 1 || fixture.InstallerRequests != 1)
                throw new IOException("The downloaded signed fixture changed or Windows trust was not checked.");
            checks.Add("Explicit download preserves exact bytes and passes actual Windows signature and publisher verification while locked");
            await service.VerifyAsync(downloaded, environment);
            if (verifier.Calls != 2) throw new IOException("Final verification did not repeat Windows trust.");
            checks.Add("Final validation repeats manifest, hash and actual Windows trust checks");
            var changed = signedBytes.ToArray(); changed[4096] ^= 1;
            await File.WriteAllBytesAsync(downloaded.Path, changed);
            await Reject(() => service.VerifyAsync(downloaded, environment), "checksum");
            if (verifier.Calls != 2) throw new IOException("Changed bytes reached Windows trust after the hash check failed.");
            checks.Add("Same-size cached-file tampering fails before the final signature check");
        }

        foreach (var unsigned in new[] { true, false })
        {
            var bytes = unsigned ? await File.ReadAllBytesAsync(Path.Combine(directory, "unsigned-test-fixture.exe")) : signedBytes;
            using var transport = Transport(bytes, out _);
            var service = new UpdateService(transport, unsigned ? trustedPolicy : Policy(identity with { CertificateSha256 = new string('0', 64) }), new AuthenticodeVerifier());
            var candidate = await service.CheckAsync(environment) ?? throw new IOException("The negative fixture did not reach the installer trust boundary.");
            var cache = Path.Combine(root, unsigned ? "unsigned" : "wrong-publisher");
            await Reject(() => service.DownloadAsync(candidate, environment, cache), unsigned ? "Windows rejected" : "publisher does not match");
            if (Directory.EnumerateFiles(cache, "*.exe", SearchOption.AllDirectories).Any())
                throw new IOException("A failed trust check retained downloaded executable bytes.");
            checks.Add(unsigned ? "A valid manifest cannot authorise an unsigned executable; failed download bytes are removed"
                : "A valid manifest cannot bypass the pinned Windows publisher; failed download bytes are removed");
        }

        using (var transport = Transport(signedBytes, out var fixture, tamperManifest: true))
        {
            var service = new UpdateService(transport, trustedPolicy, new AuthenticodeVerifier());
            if (await service.CheckAsync(environment) is not null || fixture.InstallerRequests != 0)
                throw new IOException("Altered signed metadata produced an offer or executable download.");
            checks.Add("Altered signed metadata produces no offer and no executable request");
        }
        if (!SHA256.HashData(await File.ReadAllBytesAsync(signedPath)).SequenceEqual(sourceHash))
            throw new IOException("The combined update check changed the original signed fixture.");
        if (!JsonSerializer.SerializeToUtf8Bytes(UpdateTrustConfiguration.Embedded(), UpdatePolicy.Json).SequenceEqual(productionTrust))
            throw new IOException("The combined update check changed embedded production trust.");
        File.WriteAllText(Path.Combine(root, "native-update-pipeline.json"), JsonSerializer.Serialize(new
        {
            passed = checks.Count, checks, sourceFixtureUnchanged = true, productionTrustChanged = false,
            privateManifestKeyExported = false, executableLaunched = false, productionCertification = false,
            limitation = "In-memory GitHub responses and a disposable Windows-trusted fixture; no live release, installer execution, production timestamp/revocation or signed-upgrade certification."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native signed update pipeline: {checks.Count} checks passed.");

        UpdatePolicy Policy(PublisherIdentity publisher) => new(new(1, new() { ["disposable-ci"] = key.ExportSubjectPublicKeyInfoPem() }, [publisher]));

        UpdateTransport Transport(byte[] bytes, out FixtureHandler handler, bool tamperManifest = false)
        {
            var manifest = new UpdateManifest(1, UpdatePolicy.ProductId, "1.1.0", "Stable", "x64", "10.0.17763", "native-v1.1.0",
                "LensDocsStudio.Native-1.1.0-win-x64-Setup.exe", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, UpdatePolicy.Json);
            var signature = key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            if (tamperManifest) payload = JsonSerializer.SerializeToUtf8Bytes(manifest with { Sha256 = new string('A', 64) }, UpdatePolicy.Json);
            var envelope = JsonSerializer.SerializeToUtf8Bytes(new SignedUpdateEnvelope(1, Convert.ToBase64String(payload), "disposable-ci", Convert.ToBase64String(signature)), UpdatePolicy.Json);
            var api = new Uri($"https://api.github.com/repos/{UpdatePolicy.Repository}/releases?per_page=100");
            var metadata = UpdatePolicy.AssetUri(manifest.ReleaseTag, "native-update.json");
            var installer = UpdatePolicy.AssetUri(manifest.ReleaseTag, manifest.AssetName);
            var releases = JsonSerializer.SerializeToUtf8Bytes(new[] { new
            {
                tag_name = manifest.ReleaseTag, html_url = UpdatePolicy.ReleasePage(manifest.ReleaseTag).AbsoluteUri,
                draft = false, prerelease = false, body = "Disposable local fixture; no release is published.",
                assets = new[] { new { name = "native-update.json", browser_download_url = metadata.AbsoluteUri, size = envelope.Length },
                    new { name = manifest.AssetName, browser_download_url = installer.AbsoluteUri, size = bytes.Length } }
            } });
            handler = new(new Dictionary<Uri, byte[]> { [api] = releases, [metadata] = envelope, [installer] = bytes }, installer);
            return new(handler);
        }
    }

    private static async Task Reject(Func<Task> action, string reason)
    {
        try { await action(); }
        catch (IOException ex) when (ex.Message.Contains(reason, StringComparison.Ordinal)) { return; }
        throw new IOException("The update pipeline did not reject " + reason + ".");
    }

    private sealed class CountingVerifier : IInstallerTrustVerifier
    {
        public int Calls { get; private set; }
        public Task VerifyAsync(string path, IReadOnlyList<PublisherIdentity> publishers, CancellationToken token)
        {
            Calls++; return new AuthenticodeVerifier().VerifyAsync(path, publishers, token);
        }
    }

    private sealed class FixtureHandler(IReadOnlyDictionary<Uri, byte[]> responses, Uri installer) : HttpMessageHandler
    {
        public int InstallerRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Get || request.Headers.Authorization is not null || request.RequestUri is null
                || !responses.TryGetValue(request.RequestUri, out var bytes)) throw new IOException("Unexpected fixture request.");
            if (request.RequestUri == installer) InstallerRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
