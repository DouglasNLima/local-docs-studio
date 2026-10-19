#:property PublishAot=false
#:property PublishTrimmed=false
#:package Microsoft.Diagnostics.Tracing.TraceEvent@4.0.0
// Offline inspection only. Derived files are written to a fresh directory;
// original ETL/application bytes, runtime and machine configuration stay intact.
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Symbols;
using System.Text.Json;
using System.Security.Cryptography;
#pragma warning disable CS0618 // Raw QPC is required to align independent Stopwatch timelines.

if (args.Length == 1 && args[0] == "--schema")
{
    Console.WriteLine(string.Join(",", typeof(Microsoft.Diagnostics.Tracing.Parsers.Kernel.CSwitchTraceData).GetProperties().Select(p => p.Name)));
    return;
}
if (args.Length is < 3 or > 4 || Directory.Exists(args[2])) throw new ArgumentException("Supply native.etl, original observations, fresh derived directory and optional --symbols.");
var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
var observations = Directory.GetFiles(args[1], "*-trace.json", SearchOption.AllDirectories).Select(path => (Path: path, Json: JsonDocument.Parse(File.ReadAllText(path)))).ToArray();
var starts = observations.SelectMany(pair => pair.Json.RootElement.EnumerateArray()).Where(row => row.GetProperty("Stage").GetString() == "trace-start").ToArray();
var threads = starts.Select(row => row.GetProperty("Detail").GetProperty("NativeThread").GetInt32()).ToHashSet();
var processes = starts.Select(row => row.GetProperty("Detail").GetProperty("Process").GetInt32()).ToHashSet();
if (threads.Count == 0 || processes.Count != 1) throw new IOException("Missing or ambiguous original UI native-thread/process identity.");
var etlx = TraceLog.CreateFromEventTraceLogFile(args[0], Path.Combine(output, "derived.etlx"));
using var log = new TraceLog(etlx);
var symbols = new List<object>();
if (args.ElementAtOrDefault(3) == "--symbols")
{
    using var symbolLog = File.CreateText(Path.Combine(output, "symbol-lookup.txt"));
    using var reader = new SymbolReader(symbolLog, "srv*" + Path.Combine(Path.GetDirectoryName(output)!, "official-symbol-cache") + "*https://msdl.microsoft.com/download/symbols");
    reader.Options = SymbolReaderOptions.NoNGenSymbolCreation;
    // Bounded official native modules; no ngen/PDB generation, binaries or global paths changed.
    var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "user32", "win32u", "ntdll", "combase", "uxtheme", "msctf", "wpfgfx_cor3", "shell32", "windows.storage", "CoreMessaging" };
    foreach (var module in log.ModuleFiles.Where(module => wanted.Contains(Path.GetFileNameWithoutExtension(module.FilePath))))
    {
        try { log.CodeAddresses.LookupSymbolsForModule(reader, module); symbols.Add(new { module.FilePath, Status = "LOOKUP_ATTEMPTED" }); }
        catch (Exception exception) { symbols.Add(new { module.FilePath, Status = exception.GetType().Name }); }
    }
}
object[] Stack(TraceCallStack? stack)
{
    var frames = new List<object>();
    for (var at = stack; at is not null && frames.Count < 128; at = at.Caller)
        frames.Add(new { at.CodeAddress.Address, at.CodeAddress.ModuleName, at.CodeAddress.FullMethodName });
    return frames.ToArray();
}
object Payload(TraceEvent value) => value.PayloadNames.Select((name, index) => new { Name = name, Value = Convert.ToString(value.PayloadValue(index), System.Globalization.CultureInfo.InvariantCulture) }).ToArray();
var rows = new List<object>(); var runtime = new List<object>();
using var source = log.Events.GetSource();
source.Kernel.All += value =>
{
    if (!value.EventName.Contains("ReadyThread", StringComparison.Ordinal)) return;
    if (!value.PayloadNames.Select((name, index) => (name, index)).Any(pair => pair.name.Contains("Thread", StringComparison.Ordinal)
        && int.TryParse(Convert.ToString(value.PayloadValue(pair.index)), out var target) && threads.Contains(target))) return;
    rows.Add(new { Kind = "ReadyThread", Qpc = value.TimeStampQPC, value.TimeStampRelativeMSec, value.ThreadID, value.ProcessID, Payload = Payload(value), Stack = Stack(value.CallStack()) });
};
source.Kernel.ThreadCSwitch += value =>
{
    if (!threads.Contains(value.OldThreadID) && !threads.Contains(value.NewThreadID)) return;
    var index = value.BlockingStack();
    var blocking = index == CallStackIndex.Invalid ? null : log.CallStacks[index];
    rows.Add(new { Kind = "ContextSwitch", Qpc = value.TimeStampQPC, value.TimeStampRelativeMSec, value.ThreadID, value.ProcessID,
        value.OldThreadID, value.NewThreadID, Payload = Payload(value), Stack = Stack(blocking) });
};
source.Kernel.PerfInfoSample += value =>
{
    if (!threads.Contains(value.ThreadID) || !processes.Contains(value.ProcessID)) return;
    rows.Add(new { Kind = "CpuSample", Qpc = value.TimeStampQPC, value.TimeStampRelativeMSec, value.ThreadID, value.ProcessID, Stack = Stack(value.CallStack()) });
};
source.Clr.All += value =>
{
    if (!processes.Contains(value.ProcessID) || !value.EventName.StartsWith("GC/", StringComparison.Ordinal)
        || value.EventName.Contains("Bulk", StringComparison.Ordinal) || value.EventName.Contains("AllocationTick", StringComparison.Ordinal)) return;
    runtime.Add(new { Event = value.EventName, Qpc = value.TimeStampQPC, value.TimeStampRelativeMSec, value.ThreadID, value.ProcessID, Payload = Payload(value) });
};
source.Process();
var result = new { Tool = "Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0", DiagnosticOnly = true, OriginalTraceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    ApplicationTraceFiles = observations.Select(pair => new { pair.Path, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Path))) }),
    log.EventsLost, log.SessionStartTime, log.SessionEndTime, UiNativeThreads = threads, Processes = processes, SymbolLookups = symbols, UiEvents = rows, RuntimeEvents = runtime,
    Meaning = "Scheduling/stacks from a separately profiled execution, not acceptance. Missing/unresolved symbols and runtime events remain missing; overlap is not causality." };
File.WriteAllText(Path.Combine(output, "native-ui-events.json"), JsonSerializer.Serialize(result));
Console.WriteLine($"Retained {rows.Count} UI scheduling/CPU and {runtime.Count} runtime events; lost {log.EventsLost}.");
