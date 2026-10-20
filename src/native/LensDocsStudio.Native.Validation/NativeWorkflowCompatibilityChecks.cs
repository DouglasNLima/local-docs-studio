using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

/// <summary>Public ordinary pipeline proof against the unchanged pinned package; raw outputs survive failures.</summary>
internal static class NativeWorkflowCompatibilityChecks
{
    public static void Run(string output, string repository)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var captures = new List<object>(); var diagrams = new DiagramService();
        try
        {
            foreach (var family in new[] { "kanban", "requirement", "gitgraph" })
            foreach (var dark in new[] { false, true })
            {
                var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid/expansion-iv", family + ".mmd"));
                var result = Render(source, family + (dark ? "-dark" : "-light"), dark);
                var native = new NativeSvgService(); var png = NativeSvgService.ToPng(native.Render(result.Svg!));
                var name = family + (dark ? "-dark" : "-light"); File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                var markdown = "# Pinned " + family + " compatibility\n\n```mermaid\n" + source + "\n```\n";
                var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single();
                var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { });
                Check(NativeSourceEditorChecks.Pixels(png).SequenceEqual(NativeSourceEditorChecks.Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source))), family + ": complete ordinary preview pixels equal trusted SVG/PNG");
                foreach (var format in new[] { "HTML", "DOCX", "PDF" })
                {
                    var bytes = new DocumentExportService().ExportAsync(format, markdown, null, null, dark, null).GetAwaiter().GetResult();
                    File.WriteAllBytes(Path.Combine(output, name + "." + format.ToLowerInvariant()), bytes);
                    if (format == "HTML")
                    {
                        var html = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(Encoding.UTF8.GetString(bytes)); var image = html.QuerySelector("figure img")?.GetAttribute("src") ?? "";
                        Check(image.StartsWith("data:image/png;base64,", StringComparison.Ordinal) && NativeSourceEditorChecks.Pixels(Convert.FromBase64String(image[22..])).SequenceEqual(NativeSourceEditorChecks.Pixels(png)), family + ": ordinary HTML embeds exact canonical pixels");
                    }
                    else if (format == "DOCX")
                    {
                        using var docx = WordprocessingDocument.Open(new MemoryStream(bytes), false); Check(!new OpenXmlValidator().Validate(docx).Any(), family + ": DOCX validates");
                        using var image = new MemoryStream(); docx.MainDocumentPart!.ImageParts.Single(p => p.ContentType == "image/png").GetStream().CopyTo(image);
                        Check(NativeSourceEditorChecks.Pixels(image.ToArray()).SequenceEqual(NativeSourceEditorChecks.Pixels(png)), family + ": DOCX embeds exact canonical pixels");
                    }
                    else { using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes); Check(pdf.GetPages().SelectMany(p => p.GetImages()).Any(), family + ": PDF contains a parsed image"); }
                }
                captures.Add(new { Family = family, Dark = dark, SourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant() });
            }
            foreach (var header in new[] { "kanban", "requirementDiagram", "gitGraph LR:" })
            { Render(header + "\n", "empty-" + header.Split(' ')[0]); Render(header + "\n" + (header == "kanban" ? "  Bare\n    [Card]\n" : header == "requirementDiagram" ? "requirement a {\n}\n" : "commit\n"), "small-" + header.Split(' ')[0]); }
            var board = Render("kanban\n  A\n    same[One]@{ assigned: old, ASSIGNED: 'new', priority: HIGH }\n      same[Two]\n B\n\t\t\tCard\n", "kanban-semantics");
            Check(G(board, "kanban-column").Select(e => (string?)e.Attribute("data-id")).SequenceEqual(new[] { "A", "B" }) && G(board, "kanban-card").Length == 3, "Kanban: first content indentation counts each tab as one character; all deeper content is cards");
            Check(G(Render("kanban\n  A\n\t\tB\n", "kanban-tab-boundary"), "kanban-column").Length == 2, "Kanban: two tabs equal two spaces at the fixed first-column boundary and create a column");
            Check(G(board, "kanban-card").Take(2).All(e => (string?)e.Attribute("data-id") == "same") && board.Svg!.Contains(">new<", StringComparison.Ordinal) && !board.Svg.Contains(">old<", StringComparison.Ordinal), "Kanban: duplicate IDs survive; case-insensitive metadata is last-wins");
            var title = Render("kanban\n  A\n    title Global\n    [Bare]\n", "kanban-global-title"); Check(G(title, "kanban-card").Length == 1 && G(title, "diagram-title").Single().Value.Contains("Global", StringComparison.Ordinal), "Kanban: title lines are global even at card indentation; bracket-only form couples ID and title");
            var literal = Render("kanban\n  A\n    [title Literal]\n    id[title \"Quoted\", {literal}]\n", "kanban-literal-forms");
            Check(G(literal, "diagram-title").Length == 0 && (string?)G(literal, "kanban-card")[0].Attribute("data-id") == "title Literal", "Kanban: bracket-only title words remain literal coupled ID/title, distinct from bare global title lines");
            Check(SourceDeclarations.VisibleText(G(literal, "kanban-card")[1]).Contains(SourceDeclarations.TextValue("title \"Quoted\", {literal}"), StringComparison.Ordinal), "Kanban: literal quotes, commas and braces survive explicit card titles without invented escapes");
            var req = Render("requirementDiagram\nelement same {\ntype: hidden\n}\nrequirement same {\nid: first\nID: effective\nrisk: unsupported\n}\nrequirement same {\nid: hidden\n}\nsame - traces -> missing\n", "requirement-hidden");
            Check(G(req, "req-node").Length == 1 && G(req, "req-node")[0].Value.Contains("effective", StringComparison.Ordinal) && !G(req, "req-node")[0].Value.Contains("hidden", StringComparison.Ordinal) && G(req, "req-relation").Length == 0, "Requirement: requirements precede elements, first symbolic box wins, properties last-win, unknown references are omitted");
            const string pair = "requirementDiagram\nrequirement a {\n}\nelement b {\n}\n";
            foreach (var direction in new[] { "TB", "BT", "LR", "RL" })
            {
                var directed = Render(pair.Replace("requirementDiagram\n", "requirementDiagram\ndirection " + direction + "\n", StringComparison.Ordinal) + "a - satisfies -> b\n", "requirement-direction-" + direction);
                var rectangles = G(directed, "req-node").ToDictionary(n => (string)n.Attribute("data-id")!, n => n.Elements().First(e => e.Name.LocalName == "rect"));
                var a = rectangles["a"]; var b = rectangles["b"]; var axis = direction is "LR" or "RL" ? "x" : "y";
                Check(direction is "LR" or "TB" ? ChartSource.Attribute(a, axis) < ChartSource.Attribute(b, axis) : ChartSource.Attribute(a, axis) > ChartSource.Attribute(b, axis), "Requirement: actual " + direction + " box positions follow the declared direction");
            }
            Check(Geometry(Render(pair + "a - verifies -> b\n", "requirement-forward")) == Geometry(Render(pair + "b <- verifies - a\n", "requirement-reverse")), "Requirement: reverse arrows retain actual source/target semantics");
            foreach (var kind in new[] { "requirement", "functionalRequirement", "interfaceRequirement", "performanceRequirement", "physicalRequirement", "designConstraint" }) Check(G(Render("requirementDiagram\n" + kind + " a {\n}\n", "requirement-kind-" + kind), "req-node").Length == 1, "Requirement kind renders: " + kind);
            foreach (var relation in new[] { "contains", "copies", "derives", "satisfies", "verifies", "refines", "traces" }) Check(G(Render(pair + "a - " + relation + " -> b\n", "requirement-relation-" + relation), "req-relation").Single().Attribute("data-type")!.Value == relation, "Requirement relation renders: " + relation);
            const string history = "commit\nbranch docs\ncommit\ncheckout main\ncommit\nmerge docs\ncommit\n";
            var git = Render("gitGraph LR:\n" + history, "gitgraph-history");
            Check(G(git, "commit").Select(e => e.Elements().FirstOrDefault(x => x.Name.LocalName == "text")?.Value ?? "").SequenceEqual(new[] { "0", "1", "2", "", "4" }), "GitGraph: generated counter includes merges; anonymous merge has no ID label");
            Check(Geometry(git) == Geometry(Render("gitGraph TB:\n" + history, "gitgraph-ignored-orientation")), "GitGraph limitation: TB orientation is parsed but canonical output remains LR");
            Check(G(Render("gitGraph\ncommit\ncheckout unknown\ncommit\nmerge missing\n", "gitgraph-missing"), "gitgraph-branch").Length == 1, "GitGraph limitation: unknown checkout uses fallback lane; missing merge head is not rejected");
            Check(G(Render("gitGraph\ncommit\nbranch docs order: 9\ncommit\nbranch docs\ncommit\n", "gitgraph-repeated-branch"), "gitgraph-branch").Length == 2, "GitGraph: repeated branch acts as checkout; lane order lexeme is ignored");
            var cherry = Render("gitGraph\ncommit id: \"root\"\ncherry-pick id: \"unresolved\" parent: \"missing\"\n", "gitgraph-cherry-pick"); Check(G(cherry, "commit").Length == 2, "GitGraph limitation: cherry-pick does not prove source or parent ancestry");
            Receipt("PASS", null);
        }
        catch (Exception error) { Receipt("FAIL", error.ToString()); throw; }

        DiagramResult Render(string source, string name, bool dark = false)
        { var r = diagrams.Render(source, dark); File.WriteAllText(Path.Combine(output, name + ".mmd"), source); File.WriteAllText(Path.Combine(output, name + ".svg"), r.Svg ?? r.Error); Check(r.Success && r.Source == source, name + ": exact authoritative source renders"); return r; }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Receipt(string status, string? error) => File.WriteAllText(Path.Combine(output, "workflow-compatibility.json"), JsonSerializer.Serialize(new { Status = status, Error = error, SourceRevision = typeof(DiagramService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Renderer = "Mermaider 0.15.1", RendererSource = "71b64845570d633a94d29e85579c0a02786fece8", Checks = checks, Captures = captures }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static XElement[] G(DiagramResult result, string cls) => SourceDeclarations.Groups(result.Svg!, cls);
    private static string Geometry(DiagramResult result) => string.Join("|", XDocument.Parse(result.Svg!).Descendants().Where(e => e.Name.LocalName is "path" or "circle" or "rect").Select(e => string.Join(";", e.Attributes().Where(a => a.Name.LocalName is "d" or "cx" or "cy" or "x" or "y" or "width" or "height"))));
}
