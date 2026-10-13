using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

internal static class PreviewStallWatchChecks
{
    public static void Run(string output)
    {
        var directory = Path.Combine(output, "stall-watch-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var refused = Path.Combine(directory, "unrelated-target");
        var launch = new ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        launch.ArgumentList.Add("--watch-preview-stalls"); launch.ArgumentList.Add("0"); launch.ArgumentList.Add(refused);
        using (var child = Process.Start(launch) ?? throw new IOException("The diagnostic refusal check did not start."))
        {
            var error = child.StandardError.ReadToEndAsync(); var response = child.StandardOutput.ReadToEndAsync();
            if (!child.WaitForExit(20_000)) { child.Kill(entireProcessTree: true); throw new IOException("The diagnostic refusal check did not finish."); }
            if (child.ExitCode != 1 || !error.GetAwaiter().GetResult().Contains("invoking validation process", StringComparison.Ordinal)
                || response.GetAwaiter().GetResult().Length != 0 || Directory.Exists(refused))
                throw new IOException("The diagnostic helper did not refuse an unrelated target before creating evidence.");
        }
        using (var watcher = new PreviewStallWatch(directory))
        {
            watcher.Begin("healthy");
            for (var index = 0; index < 20; index++) { Thread.Sleep(10); watcher.Pulse(); }
            watcher.End(); Thread.Sleep(350);
            watcher.Begin("probe"); var wait = Stopwatch.StartNew();
            // Keep the intentional wait active while DbgHelp samples the target thread.
            Thread.Sleep(1000);
            while (!Directory.EnumerateFiles(directory, "*.dmp").Any() && wait.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(10);
            watcher.End(); watcher.Complete();
        }
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "native-preview-stall-watch.json")));
        var root = report.RootElement; var captures = root.GetProperty("Captures");
        var observed = root.GetProperty("Observations").EnumerateArray().ToArray();
        if (!root.GetProperty("HelperCompleted").GetBoolean() || root.GetProperty("TargetProcess").GetInt32() != Environment.ProcessId
            || root.GetProperty("HelperProcess").GetInt32() == Environment.ProcessId || captures.GetArrayLength() != 1
            || !new[] { "healthy", "probe" }.All(name => observed.Any(item => item.GetProperty("Fixture").GetString() == name && item.GetProperty("Frames").GetInt32() > 0)))
            throw new IOException("The preview watchdog failed its owned-process or healthy-input check.");
        var capture = captures[0];
        if (capture.GetProperty("Fixture").GetString() != "probe" || capture.GetProperty("ObservedGapMilliseconds").GetDouble() < PreviewStallWatch.CaptureAfterMilliseconds
            || capture.GetProperty("FrameAgeMilliseconds").GetDouble() > 100)
            throw new IOException("The preview watchdog captured before its diagnostic deadline or used a stale input frame.");
        var dump = Path.Combine(directory, capture.GetProperty("Dump").GetString()!);
        if (new FileInfo(dump).Length is < 32 or > 64 * 1024 * 1024) throw new IOException("The diagnostic minidump is missing or unexpectedly large.");
        var bytes = File.ReadAllBytes(dump);
        uint UInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        var flags = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(24, 8));
        // Windows can add AVX register context to the requested thread information.
        if (UInt32(0) != 0x504d444d || (flags & 0x1000) == 0 || (flags & ~0x201000UL) != 0)
            throw new IOException("The diagnostic file is not the requested native stack/thread minidump.");
        var streams = checked((int)UInt32(8)); var table = checked((int)UInt32(12)); var found = false;
        for (var index = 0; index < streams; index++)
        {
            var entry = checked(table + index * 12);
            if (UInt32(entry) != 3) continue;
            var data = checked((int)UInt32(entry + 8)); var count = checked((int)UInt32(data));
            for (var thread = 0; thread < count; thread++)
                if (UInt32(checked(data + 4 + thread * 48)) == capture.GetProperty("InputThread").GetUInt32()) found = true;
        }
        if (!found) throw new IOException("The minidump does not contain the watched validation input thread.");
        var failedDirectory = Path.Combine(directory, "helper-failure");
        var failedWatcher = new PreviewStallWatch(failedDirectory);
        using (var child = Process.GetProcessById(failedWatcher.HelperProcessId)) { child.Kill(); child.WaitForExit(5000); }
        const string originalError = "Controlled original layout failure";
        try { using (failedWatcher) throw new IOException(originalError); }
        catch (IOException error) when (error.Message == originalError) { }
        if (!File.Exists(Path.Combine(failedDirectory, "native-preview-stall-watch-error.txt")))
            throw new IOException("A failed diagnostic helper lost its secondary error evidence.");
        File.WriteAllText(Path.Combine(output, "native-preview-stall-watch-checks.json"), JsonSerializer.Serialize(new
        { Passed = true, OwnedProcessOnly = true, UnrelatedTargetRefused = true, SeparateHelper = true, HealthyAndIdleNotCaptured = true, CapturedAfterDeadline = true,
            WatchedThreadInDump = true, NativeStackDumpOnly = true, PrimaryFailurePreserved = true, SecondaryFailureRecorded = true,
            ActualDumpFlags = flags, Capture = capture.Clone(), Evidence = Path.GetRelativePath(output, directory),
            Limitation = "This deliberately stalled windowless fixture verifies diagnostic capture, not renderer responsiveness." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
