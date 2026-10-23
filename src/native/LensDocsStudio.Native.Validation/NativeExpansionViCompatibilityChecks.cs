using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

/// <summary>Public ordinary render/export proof, captured before any Expansion VI editor controls exist.</summary>
internal static class NativeExpansionViCompatibilityChecks
{
    public static void Run(string output, string repository, bool baseline)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var observations = new List<object>(); var gaps = new List<string>();
        var diagrams = new DiagramService(); var native = new NativeSvgService();
        try
        {
            foreach (var fixture in new[] { "packet", "treeview", "treeview-box", "treemap", "venn" })
            foreach (var dark in new[] { false, true })
            {
                var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid/expansion-vi", fixture + ".mmd"));
                var name = fixture + (dark ? "-dark" : "-light"); var result = Render(source, name, dark);
                var png = NativeSvgService.ToPng(native.Render(result.Svg!)); File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                var markdown = "# Pinned native compatibility Ω\r\n\r\n```mermaid\r\n" + source + "\r\n```\r\n\r\nNeighbour remains exact.\r\n";
                var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single();
                var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { });
                Check(Pixels(png).SequenceEqual(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source))), name + ": ordinary preview equals trusted native PNG at zero pixel tolerance");
                var standalone = new ExportPreparationService(new MarkdownService(), diagrams, native).Prepare(source, "source.mermaid", null, dark);
                Check(standalone.Diagrams.Count == 1 && Pixels(standalone.Diagrams.Values.Single().PngBytes).SequenceEqual(Pixels(png)), name + ": standalone Mermaid uses the ordinary canonical export route");
                foreach (var format in new[] { "HTML", "DOCX", "PDF" })
                {
                    var bytes = new DocumentExportService().ExportAsync(format, markdown, null, null, dark, null).GetAwaiter().GetResult();
                    File.WriteAllBytes(Path.Combine(output, name + "." + format.ToLowerInvariant()), bytes);
                    if (format == "HTML")
                    {
                        var html = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(Encoding.UTF8.GetString(bytes)); var image = html.QuerySelector("figure img")?.GetAttribute("src") ?? "";
                        Check(image.StartsWith("data:image/png;base64,", StringComparison.Ordinal) && Pixels(Convert.FromBase64String(image[22..])).SequenceEqual(Pixels(png)), name + ": HTML embeds exact canonical pixels");
                    }
                    else if (format == "DOCX")
                    {
                        using var docx = WordprocessingDocument.Open(new MemoryStream(bytes), false); Check(!new OpenXmlValidator().Validate(docx).Any(), name + ": DOCX validates");
                        using var image = new MemoryStream(); docx.MainDocumentPart!.ImageParts.Single(p => p.ContentType == "image/png").GetStream().CopyTo(image);
                        Check(Pixels(image.ToArray()).SequenceEqual(Pixels(png)), name + ": DOCX embeds exact canonical pixels");
                    }
                    else { using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes); Check(pdf.GetPages().SelectMany(p => p.GetImages()).Any(), name + ": ordinary PDF contains an actual image"); }
                }
                var workspace = Path.Combine(output, name + "-workspace"); Directory.CreateDirectory(workspace);
                File.WriteAllText(Path.Combine(workspace, "document.md"), markdown, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(workspace, "source.mmd"), source, new UTF8Encoding(false));
                var bundle = new MarkdownBundleService().ExportAsync(new WorkspacePath(workspace)).GetAwaiter().GetResult();
                var bundlePath = Path.Combine(output, name + "-source.zip"); File.WriteAllBytes(bundlePath, bundle);
                var imported = new MarkdownBundleService().ImportAsync(bundlePath, Path.Combine(output, name + "-imported")).GetAwaiter().GetResult();
                Check(File.ReadAllBytes(Path.Combine(imported.Root, "document.md")).SequenceEqual(Encoding.UTF8.GetBytes(markdown)) && File.ReadAllBytes(Path.Combine(imported.Root, "source.mmd")).SequenceEqual(Encoding.UTF8.GetBytes(source)), name + ": ordinary bundle/import preserves all Markdown and standalone source bytes");
                var exporter = new DocsSiteExporter(); var content = new ExportPreparationService(new MarkdownService(), diagrams, native).Prepare(markdown, null, null, dark);
                var page = exporter.PreparePage(fixture + ".md", content); var site = exporter.Export([page], new("Native compatibility", Theme: dark ? "dark" : "light"));
                File.WriteAllBytes(Path.Combine(output, name + "-docs-site.zip"), site.Bytes);
                using var archive = new ZipArchive(new MemoryStream(site.Bytes));
                using var data = new StreamReader(archive.GetEntry("assets/search-index.json")!.Open()); using var json = JsonDocument.Parse(data.ReadToEnd());
                var body = json.RootElement.GetProperty("pages").EnumerateArray().Single(p => p.GetProperty("path").GetString() == fixture + ".md").GetProperty("html").GetString()!;
                var embedded = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(body).QuerySelector("figure img")!.GetAttribute("src")!;
                Check(site.Diagrams == 1 && Pixels(Convert.FromBase64String(embedded[22..])).SequenceEqual(Pixels(png)), name + ": actual Docs Site archive embeds canonical pixels");
            }
            foreach (var (header, simple) in new[] { ("packet", "0: \"Bit\""), ("packet-beta", "+1: \"Bit\""), ("treeView", "file.txt"), ("treeView-beta", "folder/"), ("treemap-beta", "\"Tile\": 1"), ("venn-beta", "set A") })
            {
                Render(header + "\n", "empty-" + header); Render(header + "\n" + simple + "\n", "simple-" + header);
                var trivia = "%% inert leading comment Ω\r\n\r\n" + header + "\r\n" + simple + "\r\n";
                var r = diagrams.Render(trivia); Observe("leading-trivia-" + header, r);
                if (!r.Success) { if (!baseline) Check(false, header + ": leading trivia adapter must render preserved source"); gaps.Add(header + ": leading inert trivia dispatch fails before adapter"); }
                else Check(r.Source == trivia, header + ": leading trivia retains exact source");
            }
            var packet = Render("packet title Earlier\ntitle Effective Ω\n0-30: \"First\"\n+4: \"Cross\"\n+0: \"Skipped\"\n4096: \"Outside\"\n+2: \"After\"\n", "packet-cursor");
            Check(G(packet, "packet-field").Select(e => A(e, "data-start") + ":" + A(e, "data-end")).SequenceEqual(new[] { "0:30", "31:31", "32:34", "35:36" }), "Packet: accepted cursor only, inclusive ends and every actual multirow segment");
            Check(XDocument.Parse(packet.Svg!).Descendants().Any(e => e.Name.LocalName == "text" && e.Value == "Effective Ω"), "Packet: later effective title overrides header");
            var segments = G(packet, "packet-field"); var a = Rect(segments[1]); var b = Rect(segments[2]);
            Check(b.Y > a.Y && a.X > b.X && b.W > a.W && segments[1].Value.Contains("31", StringComparison.Ordinal) && segments[2].Value.Contains("34", StringComparison.Ordinal), "Packet: actual native rows, widths and visible inclusive numbering");
            Check(G(Render("packet\n0-7: \"Same\"\n0-7: \"Same\"\n+99999999999999: \"Skipped\"\n\"bad\"\n", "packet-duplicate-skipped"), "packet-field").Length == 2, "Packet: identical ranges retain duplicate native segments; overflow and unknown text are ignored");
            Check(G(Render("packet\n4095: \"Last\"\n+1: \"Skipped\"\n", "packet-hard-cap"), "packet-field").Length == 1, "Packet: 4095 is inclusive and cursor past cap is skipped");
            var tree = Render("treeView\nroot/\n\tchild.txt\n    peer.txt\nsecond/\n", "tree-tabs");
            Check(G(tree, "treeview-node").Select(e => A(e, "data-depth")).SequenceEqual(new[] { "0", "1", "1", "0" }), "TreeView: TAB expands to four spaces; exact native root/preorder hierarchy");
            Check(G(tree, "treeview-node").All(e => e.Descendants().Any(x => A(x, "class") == "treeview-icon")), "TreeView: default folder/file are actual native vector glyphs");
            var box = Render("treeView-beta\n├── source/\n│   ┗━━ leaf Ω\n└── other\n", "tree-box");
            Check(G(box, "treeview-node").Select(e => A(e, "data-depth")).SequenceEqual(new[] { "0", "1", "0" }), "TreeView: first nonblank body line chooses box mode, including mixed light/heavy native prefixes");
            var indentation = Render("treeView\nroot/\n  └── literal\n", "tree-mode-first-line");
            Check(G(indentation, "treeview-node")[1].Value.Contains("└── literal", StringComparison.Ordinal), "TreeView: later branch glyph cannot change indentation mode");
            var annotated = Render("treeView\n\"folder Ω\"/ :::highlight ## Description\n  hidden icon()\n  hidden2 icon(none)\n", "tree-annotations");
            Check(G(annotated, "treeview-node")[0].Value.Contains("Description", StringComparison.Ordinal) && G(annotated, "treeview-node")[0].Value.Contains("folder Ω", StringComparison.Ordinal) && G(annotated, "treeview-node").Skip(1).All(e => !e.Descendants().Any(x => A(x, "class") == "treeview-icon")), "TreeView: quotes/slash, class/description and explicit empty/none icons retain distinct semantics");
            foreach (var icon in new[] { "file", "server", "unknown-provider:remote", "https://invalid.example/icon" })
            {
                var r = Render("treeView\nnode icon(" + icon + ")\n", "tree-explicit-icon-" + Array.IndexOf(new[] { "file", "server", "unknown-provider:remote", "https://invalid.example/icon" }, icon));
                var rejected = false; try { native.Render(r.Svg!); } catch (InvalidDataException) { rejected = true; }
                Check(r.Svg!.Contains("<image", StringComparison.Ordinal) && rejected, "TreeView: explicit icon " + icon + " remains protected by existing image sanitisation");
            }
            var map = Render("treemap-beta\n\"Parent\"\n\t\"One\": 030.50\n \"Two\": 20\n \"Hidden\": 0\n\"Zero root\": 0\n", "treemap-aggregation");
            Check(G(map, "treemap-section").Single().Value.Contains("50.5", StringComparison.Ordinal) || map.Svg!.Contains("50.5", StringComparison.Ordinal), "Treemap: derived parent total uses exact explicit descendants");
            Check(G(map, "treemap-tile").Select(e => A(e, "data-label")).SequenceEqual(new[] { "One", "Two" }), "Treemap: tabs and spaces each count one; zero child/root has no invented tile");
            Check(Rect(G(map, "treemap-tile")[0]).W * Rect(G(map, "treemap-tile")[0]).H > Rect(G(map, "treemap-tile")[1]).W * Rect(G(map, "treemap-tile")[1]).H, "Treemap: actual native squarified geometry reflects positive values");
            var fallback = Render("treemap-beta\nraw fallback\n \"Same\": 1\n \"Same\": 1\n", "treemap-fallback-duplicates");
            Check(G(fallback, "treemap-tile").Length == 2 && G(fallback, "treemap-section").Length == 1, "Treemap: fallback raw section and duplicate visible labels are not unique source identities");
            var vennSource = "venn-beta\nset A[\"Alpha\"]: 1\nset \"B\": 999\nunion A, \"B\"[\"Shared\"]: 2\nunion A, missing[\"Hidden\"]\nunion A, B\n";
            var venn = Render(vennSource, "venn-semantics");
            Check(G(venn, "venn-set").Select(e => A(e, "data-set")).SequenceEqual(new[] { "A", "B" }), "Venn: ordered set IDs remain separate from display labels");
            Check(Geometry(venn) == Geometry(Render(vennSource.Replace(": 1\n", ": 10000\n", StringComparison.Ordinal).Replace(": 999", ": 0", StringComparison.Ordinal).Replace(": 2", ": 500", StringComparison.Ordinal), "venn-size-metadata")), "Venn: set/union size metadata does not change any actual circle geometry");
            Check(G(venn, "venn-set").All(e => double.Parse(A(e.Element(e.Name.Namespace + "circle")!, "r"), CultureInfo.InvariantCulture) == 130) && !venn.Svg!.Contains(">Hidden<", StringComparison.Ordinal), "Venn: fixed 130 radius; unresolved and absent union labels have no pointer region");
            Check(G(Render("venn-beta\nset A\nset A\nunion A,A[\"Repeated\"]\nunknown\n", "venn-duplicates-unknown"), "venn-set").Length == 2, "Venn: duplicate IDs remain separate circles but reference lookup is ambiguous");
            Receipt(gaps.Count == 0 ? "PASS" : "BASELINE_GAPS", null);
        }
        catch (Exception error) { Receipt("FAIL", error.ToString()); throw; }

        DiagramResult Render(string source, string name, bool dark = false)
        {
            var result = diagrams.Render(source, dark); Observe(name, result);
            Check(result.Success && result.Source == source, name + ": exact authoritative source renders"); return result;
        }
        void Observe(string name, DiagramResult result)
        {
            File.WriteAllText(Path.Combine(output, name + ".mmd"), result.Source); File.WriteAllText(Path.Combine(output, name + ".svg"), result.Svg ?? result.Error);
            observations.Add(new { Name = name, SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(result.Source))), result.Success, result.Error });
        }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Receipt(string status, string? error) => File.WriteAllText(Path.Combine(output, "expansion-vi-compatibility.json"), JsonSerializer.Serialize(new { Status = status, Error = error, Baseline = baseline, SourceRevision = typeof(DiagramService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Renderer = "Mermaider 0.15.1", RendererSource = "71b64845570d633a94d29e85579c0a02786fece8", Checks = checks, Gaps = gaps, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static byte[] Pixels(byte[] png) => NativeSourceEditorChecks.Pixels(png);
    private static XElement[] G(DiagramResult result, string cls) => SourceDeclarations.Groups(result.Svg!, cls);
    private static string A(XElement element, string name) => (string?)element.Attribute(name) ?? "";
    private static (double X, double Y, double W, double H) Rect(XElement group)
    { var e = group.DescendantsAndSelf().First(x => x.Name.LocalName == "rect"); return (ChartSource.Attribute(e, "x"), ChartSource.Attribute(e, "y"), ChartSource.Attribute(e, "width"), ChartSource.Attribute(e, "height")); }
    private static string Geometry(DiagramResult result) => string.Join("|", XDocument.Parse(result.Svg!).Descendants().Where(e => e.Name.LocalName is "path" or "circle" or "rect").Select(e => string.Join(";", e.Attributes().Where(a => a.Name.LocalName is "d" or "cx" or "cy" or "r" or "x" or "y" or "width" or "height"))));
}
