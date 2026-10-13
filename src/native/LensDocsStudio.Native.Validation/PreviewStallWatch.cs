using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace LensDocsStudio.Native.Validation;

/// <summary>Observes validation-owned input pulses without adding pipe I/O to the WPF thread.</summary>
internal sealed class PreviewStallWatch : IDisposable
{
    internal const double CaptureAfterMilliseconds = 300;
    private readonly Process helper;
    private readonly string evidence;
    private readonly Task<string> errors;
    private readonly CancellationTokenSource stop = new();
    private readonly Task writer;
    private Phase? phase;
    private long lastInput;
    private int sequence;
    private bool disposed;
    internal int HelperProcessId => helper.Id;
    private sealed record Phase(int Sequence, string Name, long Start, uint Thread);
    private sealed class Observation(string fixture)
    {
        public string Fixture { get; } = fixture;
        public int Frames { get; set; }
        public int StaleFrames { get; set; }
        public double MaximumObservedGapMilliseconds { get; set; }
    }

    public PreviewStallWatch(string output)
    {
        evidence = Path.GetFullPath(output);
        var executable = Environment.ProcessPath ?? throw new IOException("The validation executable path is unavailable.");
        if (!Path.GetFileNameWithoutExtension(executable).Equals("LensDocsStudio.Native.Validation", StringComparison.Ordinal))
            throw new IOException("Run preview diagnostics through the validation apphost executable, including dotnet run.");
        var start = new ProcessStartInfo(executable)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--watch-preview-stalls"); start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(Path.GetFullPath(output));
        helper = Process.Start(start) ?? throw new IOException("The preview diagnostic helper did not start.");
        errors = helper.StandardError.ReadToEndAsync();
        try
        {
            if (helper.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult() != "ready")
                throw new IOException("The preview diagnostic helper did not become ready.");
            writer = Task.Run(WritePulses);
        }
        catch { if (!helper.HasExited) helper.Kill(entireProcessTree: true); helper.Dispose(); stop.Dispose(); throw; }
    }

    public void Begin(string name)
    {
        if (name is not ("table" or "quote" or "details" or "list" or "probe" or "healthy")) throw new ArgumentException("Unknown preview fixture.");
        var now = Stopwatch.GetTimestamp(); Interlocked.Exchange(ref lastInput, now);
        Volatile.Write(ref phase, new(++sequence, name, now, GetCurrentThreadId()));
    }
    public void Pulse() => Interlocked.Exchange(ref lastInput, Stopwatch.GetTimestamp());
    public void End() => Volatile.Write(ref phase, null);

    private async Task WritePulses()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
        try
        {
            do
            {
                var current = Volatile.Read(ref phase); var now = Stopwatch.GetTimestamp();
                var line = current is null ? "idle" : FormattableString.Invariant($"{current.Sequence}|{current.Name}|{current.Start}|{Interlocked.Read(ref lastInput)}|{current.Thread}|{now}");
                await helper.StandardInput.WriteLineAsync(line); await helper.StandardInput.FlushAsync(stop.Token);
            } while (await timer.WaitForNextTickAsync(stop.Token));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public void Complete()
    {
        if (disposed) return; disposed = true; End(); stop.Cancel();
        try
        {
            writer.GetAwaiter().GetResult(); helper.StandardInput.Close();
            if (!helper.WaitForExit(20_000)) { helper.Kill(entireProcessTree: true); throw new IOException("The preview diagnostic helper did not finish."); }
            if (helper.ExitCode != 0) throw new IOException("Preview diagnostic capture failed: " + errors.GetAwaiter().GetResult());
        }
        finally { if (!helper.HasExited) helper.Kill(entireProcessTree: true); helper.Dispose(); stop.Dispose(); }
    }

    public void Dispose()
    {
        // Normal completion calls Complete explicitly and surfaces diagnostic failure.
        // During exception unwinding preserve the original rendering failure as primary.
        try { Complete(); }
        catch (Exception error)
        {
            try { File.WriteAllText(Path.Combine(evidence, "native-preview-stall-watch-error.txt"), error.ToString()); }
            catch (Exception) { /* The original validation failure still propagates if its evidence directory is unavailable. */ }
        }
    }

    public static int RunHelper(string[] args)
    {
        try
        {
            if (args.Length != 3 || !int.TryParse(args[1], out var parentId) || ParentProcessId() != parentId)
                throw new IOException("Preview diagnostics require the invoking validation process.");
            using var target = Process.GetProcessById(parentId);
            if (!string.Equals(target.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "LensDocsStudio.Native.Validation", StringComparison.Ordinal))
                throw new IOException("Preview diagnostics only inspect their own validation executable.");
            var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
            var targetHandle = target.SafeHandle;
            // Load DbgHelp before observing a fixture. The separate helper avoids an in-process loader deadlock.
            var library = NativeLibrary.Load("dbghelp.dll");
            var captures = new List<object>(); var captured = new HashSet<int>(); var stale = 0;
            var observations = new Dictionary<int, Observation>();
            try
            {
                Console.WriteLine("ready"); Console.Out.Flush();
                while (Console.ReadLine() is { } line)
                {
                    if (line == "idle") continue;
                    if (line.Length > 256) throw new IOException("Unexpected preview diagnostic input.");
                    var parts = line.Split('|');
                    if (parts.Length != 6 || !int.TryParse(parts[0], out var generation) || generation is < 1 or > 32
                        || parts[1] is not ("table" or "quote" or "details" or "list" or "probe" or "healthy")
                        || !long.TryParse(parts[2], out var started) || !long.TryParse(parts[3], out var input)
                        || !uint.TryParse(parts[4], out var thread) || !long.TryParse(parts[5], out var sent))
                        throw new IOException("Unexpected preview diagnostic frame.");
                    var now = Stopwatch.GetTimestamp();
                    if (started <= 0 || input < started || sent < input || now < sent) continue;
                    var age = (now - sent) * 1000d / Stopwatch.Frequency;
                    var gap = (sent - input) * 1000d / Stopwatch.Frequency;
                    if (!observations.TryGetValue(generation, out var observation)) observations.Add(generation, observation = new(parts[1]));
                    observation.Frames++; observation.MaximumObservedGapMilliseconds = Math.Max(observation.MaximumObservedGapMilliseconds, gap);
                    if (age > 100) { stale++; observation.StaleFrames++; continue; }
                    if (gap < CaptureAfterMilliseconds || !captured.Add(generation)) continue;
                    var fileName = $"native-preview-stall-{generation}-{parts[1]}.dmp";
                    var captureWatch = Stopwatch.StartNew();
                    using (var dump = File.Create(Path.Combine(output, fileName)))
                        if (!MiniDumpWriteDump(targetHandle, (uint)target.Id, dump.SafeFileHandle, 0x1000, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    captures.Add(new { Fixture = parts[1], Sequence = generation, TargetProcess = target.Id, InputThread = thread,
                        ObservedGapMilliseconds = gap, FrameAgeMilliseconds = age, CaptureMilliseconds = captureWatch.Elapsed.TotalMilliseconds,
                        Dump = fileName, DumpType = "Native stacks and thread information; no full heap", ThresholdMilliseconds = CaptureAfterMilliseconds });
                }
            }
            finally { NativeLibrary.Free(library); }
            File.WriteAllText(Path.Combine(output, "native-preview-stall-watch.json"), JsonSerializer.Serialize(new
            { HelperCompleted = true, TargetProcess = parentId, HelperProcess = Environment.ProcessId, StaleFramesSkipped = stale,
                Observations = observations.Values, Captures = captures,
                Limitation = "A capture can add delay after the input deadline has already failed. Its stack may show subsequent work if the original pause ended before capture." },
                new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static uint ParentProcessId()
    {
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        if (!Process32FirstW(snapshot, ref entry)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        do { if (entry.ProcessId == Environment.ProcessId) return entry.ParentProcessId; } while (Process32NextW(snapshot, ref entry));
        throw new IOException("The diagnostic parent process is unavailable.");
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId; public UIntPtr DefaultHeapId; public uint ModuleId, ThreadCount, ParentProcessId;
        public int BasePriority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExecutableName;
    }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool MiniDumpWriteDump(SafeProcessHandle process, uint processId, SafeFileHandle file,
        uint type, IntPtr exception, IntPtr streams, IntPtr callback);
}
