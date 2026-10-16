using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Updates;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

// Uses the actual view model, signed in-memory metadata and inert bytes. No Windows trust or process launch.
internal static class NativeUpdateInteractionChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var task = CheckAsync(output);
            var frame = new DispatcherFrame(); var expired = false;
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(25) };
            timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
            timer.Start(); if (!task.IsCompleted) Dispatcher.PushFrame(frame); timer.Stop();
            if (expired) throw new TimeoutException("Update interaction validation exceeded its deadline.");
            task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task CheckAsync(string output)
    {
        var originalTrust = JsonSerializer.Serialize(UpdateTrustConfiguration.Embedded(), UpdatePolicy.Json);
        var outcomes = new List<object>(); var failures = new List<string>();
        using var key = RSA.Create(2048);
        var root = Path.Combine(output, "update-interactions-" + Guid.NewGuid().ToString("N"));
        foreach (var scenario in new[] { "channel-during-download", "channel-round-trip", "cancel-verification", "channel-during-install", "accepted-selection", "unsigned-selection" })
        {
            using var fixture = await Fixture.CreateAsync(Path.Combine(root, scenario), key, scenario == "unsigned-selection");
            var model = fixture.Model; var passed = false;
            if (scenario == "unsigned-selection")
            {
                var commandsDisabled = !model.DownloadCommand.CanExecute(null) && !model.InstallCommand.CanExecute(null);
                // Even a direct command invocation cannot bypass the service's trust boundary.
                await model.DownloadCommand.ExecuteAsync(null);
                passed = commandsDisabled && model.Downloaded is null && fixture.Handler.Requests == 0 && fixture.InstallCalls == 0;
            }
            else if (scenario == "accepted-selection")
            {
                await model.DownloadCommand.ExecuteAsync(null);
                var downloaded = model.Downloaded;
                await model.InstallCommand.ExecuteAsync(null);
                passed = downloaded is not null && fixture.InstallCalls == 1 && fixture.Installed == downloaded
                    && fixture.InstalledEnvironment == model.Environment && fixture.Trust.Checks == 2
                    && (await File.ReadAllBytesAsync(downloaded.Path)).SequenceEqual(Fixture.Bytes);
            }
            else
            {
                if (scenario == "channel-during-install") await model.DownloadCommand.ExecuteAsync(null);
                var held = fixture.Trust.HoldNext();
                var pending = scenario == "channel-during-install" ? model.InstallCommand.ExecuteAsync(null) : model.DownloadCommand.ExecuteAsync(null);
                try
                {
                    await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    if (scenario == "cancel-verification") model.CancelCommand.Execute(null);
                    else
                    {
                        model.Channel = "Preview";
                        if (scenario == "channel-round-trip") model.Channel = "Stable";
                    }
                }
                finally { held.Release.TrySetResult(); }
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
                // Drain preference writes started by channel bindings before inspecting persisted consent.
                await fixture.Workspace.UpdatePreferencesAsync(preferences => preferences);
                var noCancelledDownload = scenario == "channel-during-install" || !Directory.EnumerateFiles(fixture.StateRoot, "*.exe", SearchOption.AllDirectories).Any();
                passed = model.Downloaded is null && !model.CanInstall && fixture.InstallCalls == 0 && noCancelledDownload
                    && model.Status.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
                    && fixture.Workspace.Preferences.UpdateChannel == model.Channel;
            }
            var sourcePreserved = await File.ReadAllTextAsync(fixture.SourcePath) == Fixture.Source;
            passed &= sourcePreserved;
            outcomes.Add(new { Name = scenario, Passed = passed, model.Channel, DownloadPresented = model.Downloaded is not null,
                model.CanInstall, fixture.InstallCalls, VerificationCalls = fixture.Trust.Checks, SourcePreserved = sourcePreserved, model.Status });
            if (!passed) failures.Add(scenario);
        }
        var unchanged = originalTrust == JsonSerializer.Serialize(UpdateTrustConfiguration.Embedded(), UpdatePolicy.Json);
        if (!unchanged) failures.Add("embedded-trust");
        File.WriteAllText(Path.Combine(output, "native-update-interactions.json"), JsonSerializer.Serialize(new
        {
            Passed = failures.Count == 0, Cases = outcomes, MockedInstallerVerification = true, NetworkRequests = false,
            CertificateStoreChanged = false, InstallerLaunched = false, EmbeddedTrustUnchanged = unchanged,
            Limitation = "View-model/service interaction checks with in-memory signing keys and inert bytes; not production signing or an actual installer upgrade."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count != 0) throw new IOException("Update interaction checks failed: " + string.Join(", ", failures));
    }

    private sealed class HeldVerification
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class HeldTrust : IInstallerTrustVerifier
    {
        private HeldVerification? next;
        public int Checks { get; private set; }
        public HeldVerification HoldNext() => next = new();
        public Task VerifyAsync(string path, IReadOnlyList<PublisherIdentity> publishers, CancellationToken cancellationToken)
        {
            Checks++; var held = next; next = null;
            if (held is null) return Task.CompletedTask;
            held.Entered.TrySetResult();
            // Deliberately return normally after cancellation, like an already-completed external check.
            return held.Release.Task;
        }
    }
    private sealed class FixtureHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Fixture.Bytes) });
        }
    }
    private sealed class Fixture : IDisposable
    {
        public static byte[] Bytes { get; } = "Inert native update interaction fixture"u8.ToArray();
        public const string Source = "# Preserved workspace\r\nCafé remains unchanged.\r\n";
        public required string StateRoot { get; init; }
        public required string SourcePath { get; init; }
        public required WorkspaceViewModel Workspace { get; init; }
        public required UpdateTransport Transport { get; init; }
        public required FixtureHandler Handler { get; init; }
        public required HeldTrust Trust { get; init; }
        public UpdatesViewModel Model { get; private set; } = null!;
        public int InstallCalls { get; private set; }
        public DownloadedUpdate? Installed { get; private set; }
        public UpdateEnvironment? InstalledEnvironment { get; private set; }

        public static async Task<Fixture> CreateAsync(string root, RSA key, bool unsigned)
        {
            Directory.CreateDirectory(root);
            var state = Path.Combine(root, "state"); var source = Path.Combine(root, "source.md");
            await File.WriteAllTextAsync(source, Source);
            var workspace = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
            await workspace.UpdatePreferencesAsync(preferences => preferences with { AutomaticUpdateChecks = false });
            var handler = new FixtureHandler(); var transport = new UpdateTransport(handler); var verifier = new HeldTrust();
            var policy = new UpdatePolicy(unsigned ? new(1, [], []) : new(1,
                new() { ["interaction-fixture"] = key.ExportSubjectPublicKeyInfoPem() }, [new("CN=Test fixture only", new string('A', 64))]));
            var fixture = new Fixture { StateRoot = state, SourcePath = source, Workspace = workspace, Transport = transport, Handler = handler, Trust = verifier };
            fixture.Model = new(workspace, (update, environment) =>
            {
                fixture.InstallCalls++; fixture.Installed = update; fixture.InstalledEnvironment = environment;
                return Task.CompletedTask;
            }, new UpdateService(transport, policy, verifier));
            var environment = fixture.Model.Environment;
            var manifest = new UpdateManifest(1, UpdatePolicy.ProductId, "99.0.0", "Stable", environment.Architecture, "10.0.17763",
                "native-v99.0.0", "LensDocsStudio.Native-99.0.0-win-" + environment.Architecture + "-Setup.exe", Bytes.Length, Convert.ToHexString(SHA256.HashData(Bytes)));
            var release = new NativeRelease(manifest.ReleaseTag, UpdatePolicy.ReleasePage(manifest.ReleaseTag), false, false, "Inert interaction fixture",
                [new(manifest.AssetName, UpdatePolicy.AssetUri(manifest.ReleaseTag, manifest.AssetName), manifest.Size)]);
            var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, UpdatePolicy.Json);
            var envelope = JsonSerializer.SerializeToUtf8Bytes(new SignedUpdateEnvelope(1, Convert.ToBase64String(payload), "interaction-fixture",
                Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), UpdatePolicy.Json);
            fixture.Model.Candidate = policy.Evaluate(envelope, release, environment.CurrentVersion, environment.Channel, environment.Architecture, environment.WindowsVersion)
                ?? throw new IOException("The fixture candidate was not eligible.");
            return fixture;
        }
        public void Dispose() { Model.Dispose(); Transport.Dispose(); Workspace.Dispose(); }
    }
}
