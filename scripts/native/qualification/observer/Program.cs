using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using LensDocsStudio.Native;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.Native.Qualification;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    internal static string Output = "";
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static void Write(string name, object value)
    {
        using var stream = new FileStream(Path.Combine(Output, name), FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, Json);
    }
    private static void FailureRecord(string name, Exception error)
    {
        try { Write(name, new { processId = Environment.ProcessId, error = error.ToString() }); }
        catch (Exception recording) { Console.Error.WriteLine($"Evidence write failed: {recording.Message}; original error: {error.Message}"); }
    }
    internal static object Identity(Assembly assembly) => new {
        name = assembly.GetName().Name, sha256 = Hash(assembly.Location),
        version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion };

    [STAThread]
    private static int Main(string[] args)
    {
        // The only locally executable mode is pure calculation. No WPF resources,
        // window, input, SystemParameters or Windows appearance API is touched.
        if (args.SequenceEqual(new[] { "--self-test" })) return SelfTest();
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||
            Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows" ||
            !Guid.TryParse(Environment.GetEnvironmentVariable("LENS_QUALIFICATION_EXECUTION_ID"), out _) ||
            Environment.GetEnvironmentVariable("LENS_QUALIFICATION_CONFIG_SHA256")?.Length != 64)
        { Console.Error.WriteLine("BLOCKED: separately authorised disposable hosted execution required."); return 2; }
        if (args.Length != 6 || args[0] is not ("preflight" or "application" or "stock") || args[3] is not ("10.0.13" or "10.0.14")) return 2;
        Output = Path.GetFullPath(args[1]);
        if (Directory.Exists(Output)) throw new IOException("Fresh evidence required.");
        Directory.CreateDirectory(Output);
        var initial = Appearance.Read(); // Win32 only; never touch WPF before the original sequence.
        var code = 1;
        NativeNotifications? notifications = null;
        try
        {
            if (Environment.Version.ToString() != args[3]) throw new IOException("Loaded core runtime differs from the exact requested version.");
            if (args[0] != "stock" && !typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.EndsWith("+" + args[2], StringComparison.Ordinal))
                throw new IOException("Loaded product source identity mismatch.");
            Write("entry.json", new { mode = args[0], processId = Environment.ProcessId, tick = Stopwatch.GetTimestamp(), initial,
                firstReadBoundary = "Win32 only; no WPF colours or resource access before OriginalPreflight" });
            if (args[0] != "application") notifications = new NativeNotifications();
            if (args[0] == "preflight")
            {
                if (initial.Enabled) throw new IOException("Original preflight must start with HC disabled.");
                NativeUiChecks.Preflight(Output, args[4], args[2]);
                Write("result.json", new { mode = "preflight", processId = Environment.ProcessId, acceptance = "PASS", requirement = 4.5,
                    scope = "Original validation-process sequence; no application window launched" });
                code = 0;
            }
            else if (args[0] == "stock") { Stock(); code = 0; }
            else
            {
                if (!initial.Enabled) throw new IOException("The independent application process must start with actual HC active.");
                code = ApplicationObservation.Run(args[5]);
            }
        }
        catch (Exception error)
        {
            Write("failure.json", new { mode = args[0], processId = Environment.ProcessId, acceptance = "FAIL", error = error.ToString() });
            Console.Error.WriteLine(error.Message);
        }
        finally
        {
            // WPF assembly identity is deliberately inspected only after the original
            // first-read/resource sequence (including its failed attempt).
            try { Write("identity.json", new { mode = args[0], processId = Environment.ProcessId, productRevision = args[2],
                runtimeVersion = Environment.Version.ToString(), harness = Identity(typeof(Program).Assembly),
                product = Identity(typeof(MainWindow).Assembly), core = Identity(typeof(LensDocsStudio.Core.State.SessionStore).Assembly),
                runtimeCore = Identity(typeof(object).Assembly), wpf = Identity(typeof(Window).Assembly),
                stopwatchFrequency = Stopwatch.Frequency, os = Environment.OSVersion.VersionString,
                image = Environment.GetEnvironmentVariable("ImageVersion"), execution = Environment.GetEnvironmentVariable("LENS_QUALIFICATION_EXECUTION_ID") }); }
            catch (Exception error) { code = 1; FailureRecord("identity-failure.json", error); }
            try { Appearance.Restore(initial); Write("cleanup.json", new { processId = Environment.ProcessId, restored = Appearance.Read() == initial, initial, final = Appearance.Read() }); }
            catch (Exception error) { code = 1; FailureRecord("cleanup-failure.json", error); }
            try { notifications?.Dispose(); }
            catch (Exception error) { code = 1; FailureRecord("notification-failure.json", error); }
        }
        return code;
    }

    private static void Stock()
    {
        if (Appearance.Read().Enabled) throw new IOException("Stock control must preserve normal-to-HC preflight order.");
        using var contrast = new NativeTestHighContrast();
        if (!SystemParameters.HighContrast) throw new IOException("The hosted Windows high-contrast setting was not applied.");
        _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Stock system pairs, no product resources and no preflight cache priming.
        var text = SystemColors.HighlightTextColor; var background = SystemColors.HighlightColor;
        var ratio = NativeSyntaxChecks.Contrast(text, background);
        Program.Write("result.json", new { mode = "stock", processId = Environment.ProcessId,
            foreground = text.ToString(), background = background.ToString(), ratio, requirement = 4.5,
            os = Appearance.Read(), acceptance = ratio >= 4.5 ? "PASS" : "FAIL", scope = "Stock WPF pair only; no application safety inference" });
        if (ratio < 4.5) throw new IOException("Stock pair fails the unchanged contrast requirement.");
    }

    private static int SelfTest()
    {
        var ratio = NativeSyntaxChecks.Contrast(Color.FromRgb(0x26, 0x3b, 0x50), Color.FromRgb(0, 0x78, 0xd4));
        if (Math.Abs(ratio - 2.542596386) > 0.000001 || ratio >= 4.5 ||
            NativeSyntaxChecks.Contrast(Colors.Black, Colors.White) != 21 || NativeSyntaxChecks.Contrast(Colors.White, Colors.White) != 1) return 1;
        Console.WriteLine(JsonSerializer.Serialize(new { status = "OFFLINE_PASS", ratio, historicalAcceptance = "FAIL", requirement = 4.5,
            appearanceChanges = 0, windows = 0, foregroundInput = 0,
            product = Identity(typeof(MainWindow).Assembly), core = Identity(typeof(LensDocsStudio.Core.State.SessionStore).Assembly), harness = Identity(typeof(Program).Assembly) }, Json));
        return 0;
    }
}
