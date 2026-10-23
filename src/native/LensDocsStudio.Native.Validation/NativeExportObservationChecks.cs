using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Exports;

namespace LensDocsStudio.Native.Validation;

internal static class NativeExportObservationChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "export-observation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        const string source = "```js\nconst nativeContext = 'Café😀🚀';\n  keepIndent();\n```\n";
        var sourcePath = Path.Combine(root, "source.md"); File.WriteAllText(sourcePath, source);
        var sourceHash = SHA256.HashData(File.ReadAllBytes(sourcePath));
        var path = Path.Combine(root, "coloured-code.pdf");
        var exporter = new DocumentExportService();
        var bytes = exporter.ExportAsync("PDF", source, sourcePath, root, false, null).GetAwaiter().GetResult();
        new ExportFileStore(Path.Combine(root, "recovery")).WriteAsync(path, bytes, null).GetAwaiter().GetResult();
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes) || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new IOException("The observation control did not produce real native PDF bytes.");
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(path))
        {
            var text = string.Concat(pdf.GetPages().SelectMany(page => page.Letters).Select(letter => letter.Value));
            if (!text.Contains("Café😀🚀", StringComparison.Ordinal) || !text.Contains("keepIndent();", StringComparison.Ordinal))
                throw new IOException("The real PDF observation fixture lost selectable Unicode code.");
        }
        var checks = new List<object>();
        // Provider controls below are deliberately injected. They exercise rejection;
        // real menu/Save/UIA completion remains mandatory in every installed phase.
        var completed = new ExportCompletionObservation("Export completed — coloured-code.pdf", true, true, path, true, true, path);
        var realFile = ExportFileObservation.Read(path);
        if (realFile.Bytes != bytes.LongLength || realFile.LastWriteUtc is null) throw new IOException("Disk evidence lost file metadata.");
        Reject("File written without completion feedback", ExportObservation.Read(path, () => completed with { Status = "Preparing PDF export…" }));
        Reject("Completed filename with wrong exact destination", ExportObservation.Read(path, () => completed with { OpenPath = path + ".wrong" }));
        Reject("Reveal destination disagrees", ExportObservation.Read(path, () => completed with { RevealPath = path + ".wrong" }));
        Reject("Visible exporter error cannot pass", ExportObservation.Read(path, () => completed with { Status = "The export destination changed. Choose a new filename." }));
        Reject("Cancellation cannot pass", ExportObservation.Read(path, () => completed with { Status = "PDF export cancelled." }));
        Reject("Missing completion actions cannot pass", ExportObservation.Read(path, () => completed with { OpenVisible = false, RevealEnabled = false }));
        Reject("UIA absent cannot pass", ExportObservation.Read(path, () => new(null, false, false, null, false, false, null)));
        Reject("Stale UIA cannot pass", ExportObservation.Read(path, () => throw new COMException("Deterministic stale UIA", unchecked((int)0x80040201))));
        var absent = Path.Combine(root, "not-written.pdf"); var uiReads = 0;
        var missing = ExportObservation.Read(absent, () => { uiReads++; return completed; });
        if (uiReads != 1 || missing.File.Exists || missing.Completion != completed || missing.Matches(absent))
            throw new IOException("A missing file short-circuited completion evidence or passed.");
        checks.Add(new { Control = "File not written still captures UI", Observation = missing });
        var wrong = Path.Combine(root, "unexpected.pdf"); File.Move(path, wrong);
        missing = ExportObservation.Read(path, () => completed with { OpenPath = wrong, RevealPath = wrong });
        if (missing.Matches(path) || !File.Exists(wrong)) throw new IOException("An unexpected destination satisfied the requested path.");
        checks.Add(new { Control = "Real PDF at unexpected destination", Observation = missing });
        File.Move(wrong, path); File.WriteAllBytes(path, []);
        Reject("Empty file cannot pass", ExportObservation.Read(path, () => completed));
        File.WriteAllBytes(path, bytes);
        var providerError = new COMException("Access denied", unchecked((int)0x80070005));
        try { ExportObservation.Read(path, () => throw providerError); throw new IOException("An unrelated provider error was swallowed."); }
        catch (COMException actual) when (ReferenceEquals(actual, providerError)) { }
        try { exporter.ExportAsync("unsupported", source, sourcePath, root, false, null).GetAwaiter().GetResult(); throw new IOException("The real exporter exception was suppressed."); }
        catch (ArgumentException error) when (error.Message == "Unsupported export format.")
        { checks.Add(new { Control = "Actual exporter exception propagates", Error = error.ToString() }); }
        var latest = ExportObservation.Read(path, () => completed with { Status = "Preparing PDF export…" });
        try { NativeUiChecks.Wait(() => latest.Matches(path), "Export control: real file without completion UI", 1); throw new IOException("Incomplete export feedback passed the deadline control."); }
        catch (InvalidOperationException error) when (error.Message == "Export control: real file without completion UI")
        { checks.Add(new { Control = "Persistent incomplete feedback fails closed at bounded test deadline", Observation = latest, Error = error.ToString() }); }
        if (!SHA256.HashData(File.ReadAllBytes(sourcePath)).AsSpan().SequenceEqual(sourceHash) || File.ReadAllText(sourcePath) != source)
            throw new IOException("Export observation controls changed saved source.");
        File.WriteAllText(Path.Combine(output, "native-export-observation-controls.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = checks, ActualNativePdf = path, Bytes = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), SavedSourceUnchanged = true,
            Limitation = "Injected provider rejection controls plus real exporter/file bytes. Success is accepted only by actual menu/Save/UIA installed lifecycle gates. No synthetic success marker establishes GUI acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));

        void Reject(string name, ExportObservation observation)
        {
            if (observation.Matches(path)) throw new IOException("Export observation failed open: " + name);
            checks.Add(new { Control = name, Observation = observation });
        }
    }
}
