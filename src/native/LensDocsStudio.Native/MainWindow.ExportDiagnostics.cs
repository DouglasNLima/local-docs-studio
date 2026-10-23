using System.Text.Json;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    // Opt-in fixture evidence, bounded and local. Never contains document text.
    private void TraceExport(string phase, string format, string? path, long? bytes = null, Exception? error = null)
    {
        if (Environment.GetEnvironmentVariable("LENS_NATIVE_EXPORT_DIAGNOSTICS") != "1") return;
        try
        {
            var log = Path.Combine(model.StateDirectory, "export-lifecycle.jsonl");
            if (File.Exists(log) && new FileInfo(log).Length >= 1_000_000) return;
            File.AppendAllText(log, JsonSerializer.Serialize(new
            {
                AtUtc = DateTimeOffset.UtcNow, Phase = phase, Format = format, Destination = path, Bytes = bytes,
                Status = model.Status, Busy = model.IsBusy, Cancelled = model.OperationToken.IsCancellationRequested,
                CompletedPath = model.CompletedExport?.Path, CompletedBytes = model.CompletedExport?.Bytes,
                Document = model.ActiveDocument?.Id, Error = error?.ToString()
            }) + Environment.NewLine);
        }
        catch (Exception diagnosticError) when (diagnosticError is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Export diagnostics could not be retained: " + diagnosticError.Message); }
    }
}
