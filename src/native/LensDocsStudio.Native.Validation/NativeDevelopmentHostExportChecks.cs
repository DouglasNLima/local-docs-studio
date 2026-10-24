using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Real files from the native export services, attributed to this client build.
    // This does not exercise the portable app's shell file dialogues or claim that it did.
    private static void RunDevelopmentHostExports(string sandbox, string portableRevision)
    {
        var workspace = Path.Combine(sandbox, "workspace"); var state = Path.Combine(sandbox, "state", "export-api");
        var output = Path.Combine(sandbox, "exports"); var path = Path.Combine(workspace, "acceptance-lf.md");
        var source = File.ReadAllText(path); var sourceHash = Hash(path); var exports = new List<object>();
        var service = new DocumentExportService();
        foreach (var format in new[] { "DOCX", "PDF", "HTML" })
        {
            var bytes = service.ExportAsync(format, source, path, workspace, false, null).GetAwaiter().GetResult();
            Save("host." + format.ToLowerInvariant(), bytes);
        }
        using (var document = WordprocessingDocument.Open(Path.Combine(output, "host.docx"), false))
        {
            if (new OpenXmlValidator().Validate(document).Any() || document.MainDocumentPart?.Document?.InnerText.Contains("Café😀🚀", StringComparison.Ordinal) != true)
                throw new IOException("DOCX schema/Unicode inspection failed.");
        }
        var html = System.Net.WebUtility.HtmlDecode(File.ReadAllText(Path.Combine(output, "host.html")));
        if (!html.Contains("Café😀🚀", StringComparison.Ordinal) || !html.Contains("alt=\"Synthetic orange square\" src=\"data:image/", StringComparison.Ordinal) ||
            !html.Contains("alt=\"Mermaid diagram\" src=\"data:image/", StringComparison.Ordinal))
            throw new IOException("HTML Unicode, embedded fixture image or native diagram image is missing.");
        if (!Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(output, "host.pdf")), 0, 5).Equals("%PDF-", StringComparison.Ordinal)) throw new IOException("PDF bytes are missing their actual PDF header.");
        var match = Regex.Match(source, "```mermaid\\r?\\n(?<source>.*?)```", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!match.Success) throw new IOException("Synthetic canonical Mermaid source is missing.");
        var diagramSource = match.Groups["source"].Value;
        var canonical = new DiagramService().Render(diagramSource);
        if (canonical.Svg is null) throw new IOException("Synthetic native diagram did not render.");
        Save("host-diagram.mmd", Encoding.UTF8.GetBytes(diagramSource)); Save("host-diagram.svg", Encoding.UTF8.GetBytes(canonical.Svg));
        var diagramPng = StaWorker.RunAsync(() => NativeSvgService.ToPng(new NativeSvgService().Render(canonical.Svg))).GetAwaiter().GetResult();
        Save("host-diagram.png", diagramPng);
        if (!html.Contains("data:image/png;base64," + Convert.ToBase64String(diagramPng), StringComparison.Ordinal)) throw new IOException("HTML diagram image differs from the independently rendered canonical source.");
        // The deliberately invalid editor fixture belongs to source-protection acceptance,
        // not a successful whole-workspace export. Preserve it and use a fresh valid site fixture.
        var siteWorkspace = Path.Combine(sandbox, "workspace-site");
        if (Directory.EnumerateFileSystemEntries(sandbox).Any(entry => Path.GetFileName(entry).Equals("workspace-site", StringComparison.OrdinalIgnoreCase))) throw new IOException("Site fixture already exists; preserve it and prepare a new sandbox.");
        Directory.CreateDirectory(Path.Combine(siteWorkspace, "assets"));
        var siteFiles = new[] { "acceptance-lf.md", "acceptance-crlf.md", "relative.md", "standalone.mmd", "assets/square.png" };
        foreach (var file in siteFiles) File.Copy(Path.Combine(workspace, file), Path.Combine(siteWorkspace, file), false);
        var siteBaseline = siteFiles.ToDictionary(file => file, file => Hash(Path.Combine(siteWorkspace, file)));
        WriteNew(Path.Combine(output, "site-before.json"), siteBaseline);
        var site = new DocsSiteExportService().ExportAsync(siteWorkspace, new Dictionary<string, string>(), state, new DocsSiteOptions("Synthetic host documentation")).GetAwaiter().GetResult();
        Save("host-site.zip", site.Bytes);
        using (var archive = ZipFile.OpenRead(Path.Combine(output, "host-site.zip")))
        {
            if (archive.GetEntry("index.html") is null || archive.Entries.Any(entry => entry.FullName.Contains("LensDocsStudio.Native", StringComparison.Ordinal) || entry.FullName.Contains("preferences.json", StringComparison.Ordinal)))
                throw new IOException("Docs Site shell is missing or contains private-state entries.");
        }
        if (siteBaseline.Any(file => Hash(Path.Combine(siteWorkspace, file.Key)) != file.Value)) throw new IOException("Docs Site export changed synthetic source/asset bytes.");
        if (Hash(path) != sourceHash) throw new IOException("Native export services changed synthetic source bytes.");
        WriteNew(Path.Combine(output, "completion.json"), new { status = "PASS", scope = "DEVELOPMENT_HOST_NATIVE_EXPORT_API", portableUiSourceRevision = portableRevision,
            apiAssembly = typeof(DocumentExportService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            processId = Environment.ProcessId, recordedAtUtc = DateTimeOffset.UtcNow,
            validationRuntime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            validationWpfVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(System.Windows.Window).Assembly.Location).ProductVersion,
            runtimeScope = "Framework-dependent validation client; not the verified self-contained portable process runtime",
            apiNativeSha256 = Hash(typeof(DocumentExportService).Assembly.Location), apiCoreSha256 = Hash(typeof(DiagramService).Assembly.Location), sourceSha256 = sourceHash,
            exports, docsSite = new { site.SourcePages, site.Diagrams, site.Warnings },
            notRun = "Portable UI file dialogues: shared Windows shell history cannot be isolated. PDF text/colour and saved PNG dimensions require the independent inspection receipt." });

        void Save(string name, byte[] bytes)
        {
            if (bytes.Length == 0) throw new IOException("Empty export: " + name);
            var destination = Path.Combine(output, name);
            using (var stream = new FileStream(destination, FileMode.CreateNew)) { stream.Write(bytes); stream.Flush(true); }
            exports.Add(new { file = name, bytes = bytes.Length, sha256 = Hash(destination) });
        }
    }
}
