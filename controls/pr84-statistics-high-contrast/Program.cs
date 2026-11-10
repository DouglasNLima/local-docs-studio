using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using LensDocsStudio.Native.Validation;

internal static class Program
{
    private const string Head = "34808f59f9fe60a3d54b5bd5d82928f7c57d5a43";
    private const string Tree = "7685cd6939275d3bef442b3ff1df579616a348aa";
    private const string ManifestSha = "7dd1bc0e1a4c1ccf798495cba950a422af06729ee3876d86a04e028f6141ec22";
    private const string ControlRef = "refs/heads/control/pr84-statistics-high-contrast-once";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Contrast { public uint Size; public uint Flags; public IntPtr Scheme; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref Contrast contrast, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int index);

    private sealed record Appearance(bool Win32HighContrast, bool WpfHighContrast, uint Flags, string Scheme,
        uint[] Win32Colours, string WindowColour, string WindowTextColour, string ControlColour, string HighlightColour);

    private static Appearance Observe()
    {
        var value = new Contrast { Size = (uint)Marshal.SizeOf<Contrast>() };
        if (!SystemParametersInfo(0x42, value.Size, ref value, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return new((value.Flags & 1) != 0, SystemParameters.HighContrast, value.Flags,
            Marshal.PtrToStringUni(value.Scheme) ?? "", Enumerable.Range(0, 31).Select(GetSysColor).ToArray(),
            SystemColors.WindowColor.ToString(), SystemColors.WindowTextColor.ToString(),
            SystemColors.ControlColor.ToString(), SystemColors.HighlightColor.ToString());
    }

    private static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json) + "\n");
    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] GitBytes(string root, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("Git did not start.");
        using var output = new MemoryStream();
        var read = process.StandardOutput.BaseStream.CopyToAsync(output);
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000)) { process.Kill(true); throw new TimeoutException("Git identity read exceeded its bound."); }
        read.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new IOException(error.GetAwaiter().GetResult());
        return output.ToArray();
    }
    private static string Git(string root, params string[] args) => Encoding.UTF8.GetString(GitBytes(root, args)).Trim();

    private static void VerifySource(string root)
    {
        if (Git(root, "rev-parse", "HEAD") != Head || Git(root, "rev-parse", "HEAD^{tree}") != Tree
            || Git(root, "rev-parse", "HEAD:src/native") != "5aeb8c1f09f16c2b790de1542e45dcd3b0e41649"
            || Git(root, "rev-parse", "HEAD:tests/native/document-statistics") != "1c751d38a4239cff4fa917cb61fc5fc86bc80e6b"
            || Git(root, "status", "--porcelain=v1", "--untracked-files=all") != "")
            throw new IOException("The exact approved source identity or clean checkout differs.");
        var manifest = GitBytes(root, "show", "HEAD:docs/open-source/evidence/document-statistics/candidate-refresh.json");
        if (Digest(manifest) != ManifestSha) throw new IOException("Canonical manifest identity differs.");
        using var document = JsonDocument.Parse(manifest);
        foreach (var entry in document.RootElement.GetProperty("files").EnumerateObject())
            if (Digest(GitBytes(root, "show", "HEAD:" + entry.Name)) != entry.Value.GetProperty("current").GetString())
                throw new IOException("Protected source differs: " + entry.Name);
        foreach (var entry in document.RootElement.GetProperty("addedFiles").EnumerateObject())
            if (Digest(GitBytes(root, "show", "HEAD:" + entry.Name)) != entry.Value.GetString())
                throw new IOException("Added source differs: " + entry.Name);
    }

    private static int RunChild(string executable, string directory, string output, TimeSpan limit, Action? started, params string[] args)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("The controlled process did not start.");
        started?.Invoke();
        using var stdout = File.Create(output + ".stdout.txt");
        using var stderr = File.Create(output + ".stderr.txt");
        var copyOut = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var copyError = process.StandardError.BaseStream.CopyToAsync(stderr);
        var completed = process.WaitForExit((int)limit.TotalMilliseconds);
        if (!completed) { process.Kill(true); process.WaitForExit(); }
        Task.WhenAll(copyOut, copyError).GetAwaiter().GetResult();
        if (!completed) throw new TimeoutException("The single controlled process exceeded " + limit + "; no retry is permitted.");
        return process.ExitCode;
    }

    private static Appearance Probe(string directory, string name)
    {
        var path = Path.Combine(directory, name + ".json");
        if (RunChild(Environment.ProcessPath!, directory, Path.Combine(directory, name), TimeSpan.FromSeconds(30), null, "--probe", path) != 0)
            throw new IOException("The actual Windows/WPF appearance probe failed: " + name);
        return JsonSerializer.Deserialize<Appearance>(File.ReadAllText(path))!;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--probe") { Write(args[1], Observe()); return 0; }
        if (args.Length != 2) throw new ArgumentException("Specify the pinned candidate checkout and a fresh execution evidence directory.");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT") != "1"
            || Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") != "create"
            || Environment.GetEnvironmentVariable("GITHUB_REF") != ControlRef)
            throw new InvalidOperationException("Only the single authorised branch-creation execution on a disposable hosted Windows runner is permitted.");
        var candidate = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output)) throw new IOException("The execution evidence directory must be fresh.");
        Directory.CreateDirectory(output);
        var result = "NOT RUN"; var invocations = 0; var checks = 0; int? harnessExit = null;
        var sourceUnchanged = false; var appearanceRestored = false;
        var failures = new List<string>(); Appearance? before = null; NativeTestHighContrast? contrast = null;
        try
        {
            VerifySource(candidate);
            Write(Path.Combine(output, "source-identity.json"), new { head = Head, tree = Tree, manifestSha256 = ManifestSha,
                nativeTree = Git(candidate, "rev-parse", "HEAD:src/native"),
                statisticsControlTree = Git(candidate, "rev-parse", "HEAD:tests/native/document-statistics"),
                controlCommit = Environment.GetEnvironmentVariable("GITHUB_SHA"),
                workflowRun = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"), attempt = 1,
                framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
                imageVersion = Environment.GetEnvironmentVariable("ImageVersion") });
            var binaryDirectory = Path.Combine(candidate, "tests/native/document-statistics/bin/Release/net10.0-windows");
            var identities = Directory.EnumerateFiles(binaryDirectory).Order().Select(path => new
            {
                path = Path.GetFileName(path), sha256 = Digest(File.ReadAllBytes(path)),
                informationalVersion = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? FileVersionInfo.GetVersionInfo(path).ProductVersion : null
            }).ToArray();
            Write(Path.Combine(output, "build-identities.json"), identities);
            foreach (var name in new[] { "DocumentStatisticsControls.dll", "LensDocsStudio.Native.dll", "LensDocsStudio.Core.dll" })
                if (!identities.Single(item => item.path == name).informationalVersion!.Contains(Head, StringComparison.Ordinal))
                    throw new IOException("Compiled source revision differs: " + name);
            before = Probe(output, "appearance-before");
            if (before.Win32HighContrast != before.WpfHighContrast) throw new IOException("Original Win32 and WPF appearance disagree.");
            contrast = new NativeTestHighContrast();
            var enabled = Probe(output, "appearance-enabled");
            if (!enabled.Win32HighContrast || !enabled.WpfHighContrast) throw new IOException("Actual Windows and WPF High Contrast could not both be verified; harness not run.");
            harnessExit = RunChild(Path.Combine(binaryDirectory, "DocumentStatisticsControls.exe"), candidate,
                Path.Combine(output, "harness"), TimeSpan.FromMinutes(10), () => { invocations++; result = "FAIL"; },
                Path.Combine(output, "shown-statistics-high-contrast"), "--expect-high-contrast");
            var after = Probe(output, "appearance-after-harness");
            if (!after.Win32HighContrast || !after.WpfHighContrast) throw new IOException("Actual High Contrast was not active after the harness.");
            var resultsPath = Path.Combine(output, "shown-statistics-high-contrast", "results.json");
            if (File.Exists(resultsPath))
            {
                using var evidence = JsonDocument.Parse(File.ReadAllBytes(resultsPath));
                checks = evidence.RootElement.GetProperty("checks").GetArrayLength();
                if (harnessExit == 0 && evidence.RootElement.GetProperty("result").GetString() == "PASS"
                    && evidence.RootElement.GetProperty("actualHighContrast").GetBoolean() && checks == 48) result = "PASS";
                else failures.Add("Original harness outcome did not satisfy all 48 unchanged focused checks.");
            }
            else
            {
                var errorPath = Path.Combine(output, "harness.stderr.txt");
                if (File.Exists(errorPath) && File.ReadAllText(errorPath).Contains("Actual Windows and WPF High Contrast must match the requested appearance.", StringComparison.Ordinal))
                    result = "NOT RUN";
                failures.Add("The original harness produced no results.json; retain its process logs.");
            }
        }
        catch (Exception error) { failures.Add(error.ToString()); File.WriteAllText(Path.Combine(output, "control-failure.txt"), error.ToString()); }
        finally
        {
            try { contrast?.Dispose(); }
            catch (Exception error) { failures.Add("Appearance restoration: " + error); }
            try
            {
                var restored = Probe(output, "appearance-restored");
                appearanceRestored = before is not null && JsonSerializer.Serialize(before) == JsonSerializer.Serialize(restored);
                if (!appearanceRestored) failures.Add("Original Win32 flags/scheme/colours and WPF appearance were not verified restored.");
            }
            catch (Exception error) { failures.Add("Restoration verification: " + error); }
            try { VerifySource(candidate); sourceUnchanged = true; }
            catch (Exception error) { failures.Add("Final source identity: " + error); }
            if (failures.Count > 0 && result == "PASS") result = "FAIL";
            Write(Path.Combine(output, "control-results.json"), new { result, harnessInvocations = invocations, harnessExitCode = harnessExit,
                focusedChecks = checks, sourceUnchanged, appearanceRestored, failures,
                head = Head, tree = Tree, expectedChecks = 48, physicalScreenReaderCertification = "NOT RUN",
                measuredContrastRatioCompliance = "NOT RUN", scope = "One unchanged shown WPF/UIA-peer and routed-keyboard harness execution on actual OS High Contrast; no retry." });
        }
        Console.WriteLine($"Statistics High Contrast: {result}; checks {checks}/48; invocations {invocations}; appearance restored {appearanceRestored}.");
        return result == "PASS" ? 0 : result == "NOT RUN" ? 2 : 1;
    }
}
