#:property PublishAot=false
#:property PublishTrimmed=false
#:package Microsoft.Diagnostics.Tracing.TraceEvent@4.0.0
// Read-only EventPipe inspection. No prototype execution or runtime mutation.
// Public raw QPC is retained to align independent Stopwatch observations.
#pragma warning disable CS0618
using Microsoft.Diagnostics.Tracing;
using System.Text.Json;
if (args.Length != 2 || File.Exists(args[1])) throw new ArgumentException("Supply a retained trace and a fresh output file; existing evidence is never overwritten.");
using var source = new EventPipeEventSource(args[0]);
var rows = new List<object>();
source.Clr.All += data => {
    if (!data.EventName.StartsWith("GC/", StringComparison.Ordinal)) return;
    if (data.EventName.Contains("AllocationTick", StringComparison.Ordinal) || data.EventName.Contains("Bulk", StringComparison.Ordinal)) return;
    rows.Add(new { Event=data.EventName, data.TimeStampRelativeMSec, data.TimeStamp, Qpc=data.TimeStampQPC, data.ProcessID, data.ThreadID,
        Payload=data.PayloadNames.Select((name,index) => new { Name=name, Value=Convert.ToString(data.PayloadValue(index),System.Globalization.CultureInfo.InvariantCulture) }).ToArray() });
};
source.Process();
File.WriteAllText(args[1],JsonSerializer.Serialize(new { Tool="Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0", Input=Path.GetFullPath(args[0]), InputSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[0]))), source.SessionStartTime, source.SessionEndTime, source.EventsLost, Events=rows },new JsonSerializerOptions { WriteIndented=true }));
Console.WriteLine($"Retained {rows.Count} GC/suspension events.");
