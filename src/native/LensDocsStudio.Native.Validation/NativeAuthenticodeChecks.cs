using System.Text.Json;
using LensDocsStudio.Core.Updates;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

/// <summary>Exercises Windows trust against disposable signed fixtures supplied by the isolated CI script.</summary>
internal static class NativeAuthenticodeChecks
{
    public static void Run(string directory)
    {
        var root = Path.GetFullPath(directory);
        var identityPath = Path.Combine(root, "publisher.json");
        if (new FileInfo(identityPath).Length > 65536) throw new IOException("The test publisher identity is too large.");
        var identity = JsonSerializer.Deserialize<PublisherIdentity>(File.ReadAllText(identityPath), UpdatePolicy.Json)
            ?? throw new IOException("The test publisher identity is missing.");
        var signed = Path.Combine(root, "signed-test-fixture.exe");
        var verifier = new AuthenticodeVerifier();
        var checks = new List<string>();
        verifier.VerifyAsync(signed, [identity], CancellationToken.None).GetAwaiter().GetResult();
        checks.Add("Windows-verified signature and exact publisher accepted");
        Reject("Unsigned file rejected", "unsigned-test-fixture.exe", [identity], "Windows rejected");
        Reject("Tampered signed bytes rejected", "tampered-test-fixture.exe", [identity], "Windows rejected");
        Reject("Unpinned certificate rejected", "signed-test-fixture.exe", [identity with { CertificateSha256 = new string('0', 64) }], "publisher does not match");
        Reject("Incorrect publisher subject rejected", "signed-test-fixture.exe", [identity with { Subject = "CN=Unpinned test publisher" }], "publisher does not match");
        Reject("Empty publisher policy rejected", "signed-test-fixture.exe", [], "No trusted update publisher");
        try
        {
            verifier.VerifyAsync(signed, [identity], new CancellationToken(true)).GetAwaiter().GetResult();
            throw new IOException("Cancelled signature verification completed.");
        }
        catch (OperationCanceledException) { checks.Add("Cancelled verification rejected before Windows trust call"); }
        File.WriteAllText(Path.Combine(root, "native-authenticode-results.json"), JsonSerializer.Serialize(new
        {
            passed = checks.Count,
            checks,
            productionCertification = false,
            limitation = "Disposable CI certificate trust only; production issuer, revocation, timestamp and signed-upgrade certification remain separate."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native Authenticode: {checks.Count} checks passed.");

        void Reject(string label, string name, IReadOnlyList<PublisherIdentity> publishers, string expectedReason)
        {
            try { verifier.VerifyAsync(Path.Combine(root, name), publishers, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (IOException ex) when (ex.Message.Contains(expectedReason, StringComparison.Ordinal)) { checks.Add(label); return; }
            throw new IOException(label + " unexpectedly succeeded.");
        }
    }
}
