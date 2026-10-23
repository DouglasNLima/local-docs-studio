using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;

namespace LensDocsStudio.Native.Validation;

internal sealed record ExportCompletionObservation(string? Status, bool OpenEnabled, bool OpenVisible,
    string? OpenPath, bool RevealEnabled, bool RevealVisible, string? RevealPath)
{
    public bool Matches(string path) => Status?.StartsWith("Export completed — " + Path.GetFileName(path), StringComparison.Ordinal) == true
        && OpenEnabled && OpenVisible && RevealEnabled && RevealVisible
        && OpenPath == Path.GetFullPath(path) && RevealPath == Path.GetFullPath(path);
}

internal sealed record ExportFileObservation(bool Exists, long? Bytes, DateTime? LastWriteUtc, string? Error)
{
    public static ExportFileObservation Read(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return new(file.Exists, file.Exists ? file.Length : null, file.Exists ? file.LastWriteTimeUtc : null, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(false, null, null, error.ToString()); }
    }
}

internal sealed record ExportObservation(ExportFileObservation File, ExportCompletionObservation? Completion, string? UiError)
{
    public bool Matches(string path) => File.Exists && File.Bytes is > 0 && File.Error is null && UiError is null && Completion?.Matches(path) == true;

    public static ExportObservation Read(string path, Func<ExportCompletionObservation> observeUi)
    {
        // Always sample disk and UI independently: a missing file must not short-circuit UI evidence.
        var file = ExportFileObservation.Read(path);
        try { return new(file, observeUi(), null); }
        catch (Exception error) when (NativeUiChecks.IsRetryableObservation(error))
        { return new(file, null, error.ToString()); }
    }
}

internal static partial class NativeUiChecks
{
    private static ExportCompletionObservation ReadExportCompletion(Window window)
    {
        window = ObserveWindow(window);
        var open = window.FindFirstDescendant(cf => cf.ByAutomationId("OpenExportedFile"));
        var reveal = window.FindFirstDescendant(cf => cf.ByAutomationId("RevealExportedFile"));
        var status = window.FindFirstDescendant(cf => cf.ByAutomationId("Status"));
        return new(status?.Name, open?.IsEnabled == true, open?.IsOffscreen == false,
            open?.Properties.HelpText.ValueOrDefault, reveal?.IsEnabled == true, reveal?.IsOffscreen == false,
            reveal?.Properties.HelpText.ValueOrDefault);
    }

    private static void WaitForPdfExport(Window window, string path, string output, Process process)
    {
        var observations = new List<object>();
        ExportObservation? last = null;
        var watch = Stopwatch.StartNew();
        var passed = false;
        string? failure = null;
        try
        {
            Wait(() =>
            {
                last = ExportObservation.Read(path, () => ReadExportCompletion(window));
                observations.Add(new { ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds, Observation = last });
                return last.Matches(path);
            }, "Coloured PDF export did not complete. See native-export-observation-coloured-code.pdf.json for separate disk and UI observations.");
            passed = true;
        }
        catch (Exception error) { failure = error.ToString(); throw; }
        finally
        {
            // Evidence cannot satisfy the assertion or mask its failure. No export action is repeated.
            try
            {
                var finalFile = ExportFileObservation.Read(path);
                string? hash = null, hashError = null;
                if (finalFile.Exists)
                {
                    try
                    {
                        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        hash = Convert.ToHexStringLower(SHA256.HashData(file));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { hashError = error.ToString(); }
                }
                File.WriteAllText(Path.Combine(output, "native-export-observation-" + Path.GetFileName(path) + ".json"),
                    JsonSerializer.Serialize(new
                    {
                        Passed = passed, Failure = failure, Target = Path.GetFullPath(path), DeadlineSeconds = 20,
                        ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds, Samples = observations,
                        FinalFile = finalFile, Sha256 = hash, HashError = hashError,
                        CompletedDestination = last?.Completion?.OpenPath,
                        CompletedDestinationFile = last?.Completion?.OpenPath is { } actual ? ExportFileObservation.Read(actual) : null,
                        Process = NativeUiFailureDiagnostics.ObserveProcess(process),
                        Windows = NativeUiFailureDiagnostics.ObserveWindows(process.Id),
                        Directory = ObserveExportDirectory(Path.GetDirectoryName(path)!),
                        ApplicationDiagnosticLog = Path.Combine(Path.GetDirectoryName(path)!, "state", "export-lifecycle.jsonl"),
                        Scope = "File and completion UI assertion only; PDF header, selectable text and unchanged source are checked afterwards.",
                        Limitation = "UIA status/actions are observations, not a model call. Unavailable UIA cannot establish status, cancellation or absence of an error. Win32 windows are sampled after the original wait."
                    }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            { Console.Error.WriteLine("PDF observation evidence could not be retained: " + error); }
        }
    }

    private static object ObserveExportDirectory(string directory)
    {
        try { return Directory.EnumerateFiles(directory).Take(256).Select(file => new { Path = file, File = ExportFileObservation.Read(file) }).ToArray(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return new { Error = error.ToString() }; }
    }
}
