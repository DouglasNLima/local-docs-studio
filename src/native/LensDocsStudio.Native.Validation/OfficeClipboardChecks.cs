using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

/// <summary>Optional desktop-only destination checks; Office is not an application or CI prerequisite.</summary>
internal static class OfficeClipboardChecks
{
    public static int Run(string output)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        var root = Path.Combine(output, "office-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        const string source = "# Native clipboard — Café\n\n**Formatted** local content.\n\n| Area | Status |\n|---|---|\n| Clipboard | Ready |\n\n```mermaid\nflowchart LR\nA[Copy] --> B[Paste]\n```\n";
        File.WriteAllText(Path.Combine(root, "source.md"), source);
        var content = new ClipboardExportService().PrepareAsync(source, null, null, false).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(root, "clipboard.html"), HtmlClipboardFormat.Decode(content.Html));
        File.WriteAllText(Path.Combine(root, "clipboard.txt"), content.PlainText);
        var expectedSequence = NativeClipboardService.Sequence;
        var previous = Clipboard.GetDataObject(); uint sequence = 0;
        if (NativeClipboardService.Sequence != expectedSequence) throw new IOException("The clipboard changed before destination validation began.");
        var results = new List<object>(); var failures = 0;
        try
        {
            Check("Word", "Word.Application", "WINWORD", (application, resultPath) =>
            {
                dynamic word = application;
                word.Visible = false; word.DisplayAlerts = 0; word.AutomationSecurity = 3;
                dynamic? document = null; dynamic? range = null;
                try
                {
                    document = word.Documents.Add(); range = document.Range(0, 0);
                    Copy(); range.PasteSpecial(DataType: 10); // wdPasteHTML; actual registered Windows clipboard data.
                    var text = (string)document.Content.Text;
                    var tables = (int)document.Tables.Count; var images = (int)document.InlineShapes.Count;
                    document.SaveAs2(FileName: resultPath + ".docx", FileFormat: 16, AddToRecentFiles: false);
                    File.WriteAllText(resultPath + "-text.txt", text);
                    if (!text.Contains("Café", StringComparison.Ordinal) || tables != 1 || images < 1)
                        throw new IOException($"Word paste retained {tables} tables and {images} inline images; Unicode, one table and a diagram image are required.");
                    dynamic table = document.Tables[1];
                    try
                    {
                        if (!((string)table.Cell(2, 1).Range.Text).Contains("Clipboard", StringComparison.Ordinal) || !((string)table.Cell(2, 2).Range.Text).Contains("Ready", StringComparison.Ordinal))
                            throw new IOException("Word paste changed the clipboard table cells.");
                    }
                    finally { Release(table); }
                    dynamic formatted = document.Content;
                    try
                    {
                        if (!(bool)formatted.Find.Execute(FindText: "Formatted") || (int)formatted.Font.Bold != -1) throw new IOException("Word paste lost bold formatting.");
                    }
                    finally { Release(formatted); }
                    document.Close(SaveChanges: 0); Release(document); document = null;
                    var html = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(content.Html));
                    var png = Convert.FromBase64String(html.QuerySelector("img")!.GetAttribute("src")!.Split(',', 2)[1]);
                    using var original = new MemoryStream(png);
                    var expectedImage = NativeRasterService.Read(original);
                    using var package = ZipFile.OpenRead(resultPath + ".docx");
                    var media = package.Entries.Where(e => e.FullName.StartsWith("word/media/", StringComparison.Ordinal) && e.Length > 0).ToArray();
                    var matching = false;
                    foreach (var image in media)
                    {
                        using var stored = image.Open(); using var imageBytes = new MemoryStream(); stored.CopyTo(imageBytes); imageBytes.Position = 0;
                        try
                        {
                            var actual = NativeRasterService.Read(imageBytes);
                            matching |= actual.PixelWidth == expectedImage.PixelWidth && actual.PixelHeight == expectedImage.PixelHeight && Pixels(actual).SequenceEqual(Pixels(expectedImage));
                        }
                        catch (Exception ex) when (ex is IOException or NotSupportedException) { }
                    }
                    if (!matching) throw new IOException("Word stored no image with the original diagram pixels.");
                    return new { Version = (string)word.Version, Tables = tables, Images = images, BoldPreserved = true, DiagramPixelsPreserved = true, Output = resultPath + ".docx" };
                }
                finally { if (document is not null) document.Close(SaveChanges: 0); Release(range); Release(document); }
            });
            Check("Excel", "Excel.Application", "EXCEL", (application, resultPath) =>
            {
                dynamic excel = application;
                excel.Visible = false; excel.DisplayAlerts = false; excel.AutomationSecurity = 3;
                dynamic? workbook = null; dynamic? sheet = null; dynamic? range = null;
                try
                {
                    workbook = excel.Workbooks.Add(); sheet = workbook.Worksheets[1]; range = sheet.Range["A1"];
                    Copy(); sheet.Paste(Destination: range);
                    var values = sheet.UsedRange.Value2 as Array ?? throw new IOException("Excel paste did not produce a cell grid.");
                    var adjacent = false; var unicode = false;
                    for (var row = values.GetLowerBound(0); row <= values.GetUpperBound(0); row++)
                    for (var column = values.GetLowerBound(1); column <= values.GetUpperBound(1); column++)
                    {
                        var value = values.GetValue(row, column)?.ToString() ?? "";
                        unicode |= value.Contains("Café", StringComparison.Ordinal);
                        if (value == "Clipboard" && column < values.GetUpperBound(1) && values.GetValue(row, column + 1)?.ToString() == "Ready") adjacent = true;
                    }
                    if (!adjacent || !unicode) throw new IOException("Excel paste lost Unicode or adjacent table cells.");
                    dynamic literalSheet = workbook.Worksheets.Add(); dynamic? literalRange = null;
                    try
                    {
                        literalRange = literalSheet.Range["A1"];
                        var literalContent = new ClipboardExporter().Export(new(new MarkdownService().Parse("| Name | Value |\n|---|---|\n| Literal | =1+1 |\n| Identifier | 000123 |"), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
                        Copy(literalContent); literalSheet.Paste(Destination: literalRange);
                        dynamic cell = literalSheet.Range["B2"];
                        try { if ((bool)cell.HasFormula || (string)cell.Value2 != "=1+1") throw new IOException("Excel evaluated literal document text as a formula."); }
                        finally { Release(cell); }
                        dynamic identifier = literalSheet.Range["B3"];
                        try { if (identifier.Value2 is not string id || id != "000123") throw new IOException("Excel removed leading zeroes from a document identifier."); }
                        finally { Release(identifier); }
                    }
                    finally { Release(literalRange); Release(literalSheet); }
                    workbook.SaveAs(Filename: resultPath + ".xlsx", FileFormat: 51, AddToMru: false);
                    return new { Version = (string)excel.Version, Rows = values.GetLength(0), Columns = values.GetLength(1), LiteralCellsPreserved = true, Output = resultPath + ".xlsx" };
                }
                finally { if (workbook is not null) workbook.Close(SaveChanges: false); Release(range); Release(sheet); Release(workbook); }
            });
        }
        finally
        {
            if (sequence != 0 && NativeClipboardService.Sequence == sequence)
            {
                try { if (previous is null) Clipboard.Clear(); else Clipboard.SetDataObject(previous, true); }
                catch (ExternalException) { }
            }
            File.WriteAllText(Path.Combine(output, "office-clipboard.json"), JsonSerializer.Serialize(new { Passed = failures == 0, Results = results, FixtureRoot = root }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return failures == 0 ? 0 : 1;

        void Copy(ExportedClipboard? value = null)
        {
            NativeClipboardService.WriteRendered(value ?? content, expectedSequence);
            sequence = expectedSequence = NativeClipboardService.Sequence;
        }
        void Check(string name, string progId, string processName, Func<object, string, object> validate)
        {
            object? instance = null; var ownsProcess = false; Process? ownedProcess = null;
            try
            {
                Console.WriteLine("Starting isolated " + name + " clipboard validation.");
                var existing = Process.GetProcessesByName(processName).Select(p => p.Id).ToHashSet();
                // Word does not expose an application HWND before a document exists. Only start it when no Word session is present.
                if (processName == "WINWORD" && existing.Count > 0) throw new IOException("Word is already running; this optional check requires an unused Word session.");
                var type = Type.GetTypeFromProgID(progId) ?? throw new NotSupportedException(name + " is not registered on this machine.");
                instance = Activator.CreateInstance(type) ?? throw new IOException(name + " did not create an automation instance.");
                uint pid; var window = IntPtr.Zero;
                if (processName == "WINWORD")
                {
                    var created = Process.GetProcessesByName(processName).Where(p => !existing.Contains(p.Id)).ToArray();
                    if (created.Length != 1) throw new IOException("Word did not provide an unambiguous new process.");
                    pid = (uint)created[0].Id;
                }
                else { window = new IntPtr((int)((dynamic)instance).Hwnd); GetWindowThreadProcessId(window, out pid); }
                if (pid == 0 || existing.Contains((int)pid)) throw new IOException(name + " did not provide a separate process; existing sessions were not touched.");
                ownsProcess = true;
                ownedProcess = Process.GetProcessById((int)pid);
                File.WriteAllText(Path.Combine(root, name.ToLowerInvariant() + "-process.json"), JsonSerializer.Serialize(new { ProcessId = pid, Window = window.ToInt64() }));
                var build = ownedProcess.MainModule?.FileVersionInfo.FileVersion;
                var evidence = validate(instance, Path.Combine(root, name.ToLowerInvariant() + "-paste"));
                results.Add(new { Application = name, Passed = true, Build = build, Evidence = evidence });
                Console.WriteLine(name + " clipboard destination check passed.");
            }
            catch (Exception ex) { failures++; results.Add(new { Application = name, Passed = false, Error = ex.ToString() }); Console.WriteLine(name + " clipboard destination check failed: " + ex.Message); }
            finally
            {
                if (ownsProcess) { try { ((dynamic)instance!).Quit(); } catch (COMException) { } }
                Release(instance);
                if (ownedProcess is not null)
                {
                    // COM may retain temporary ranges after Quit. Only this newly verified process is eligible for cleanup.
                    try { if (!ownedProcess.WaitForExit(5000)) { ownedProcess.Kill(); ownedProcess.WaitForExit(5000); } }
                    catch (InvalidOperationException) { }
                    finally { ownedProcess.Dispose(); }
                }
            }
        }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    private static byte[] Pixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0); return bytes;
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
