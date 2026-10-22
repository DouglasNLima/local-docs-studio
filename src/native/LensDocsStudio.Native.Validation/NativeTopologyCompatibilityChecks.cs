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

/// <summary>Ordinary public-pipeline compatibility before topology editor authoring.</summary>
internal static class NativeTopologyCompatibilityChecks
{
    public static void Run(string output, string repository)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var observations = new List<object>(); var diagrams = new DiagramService();
        try
        {
            foreach (var fixture in new[] { "architecture", "architecture-icons", "c4-context", "c4-container", "c4-component", "c4-context-elements", "c4-container-elements", "c4-component-elements", "block" })
            foreach (var dark in new[] { false, true })
            {
                var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid/expansion-v", fixture + ".mmd"));
                var name = fixture + (dark ? "-dark" : "-light"); var result = Render(source, name, dark);
                var native = new NativeSvgService(); var png = NativeSvgService.ToPng(native.Render(result.Svg!));
                File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                var markdown = "# Pinned " + fixture + " compatibility\n\n```mermaid\n" + source + "\n```\n";
                var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single();
                var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { });
                Check(Pixels(png).SequenceEqual(Pixels(NativeSvgService.ToPng(preview.Media.Single().Image.Source))), name + ": complete ordinary preview equals trusted canonical SVG/PNG");
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
                    else { using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes); Check(pdf.GetPages().SelectMany(p => p.GetImages()).Any(), name + ": ordinary PDF contains a parsed image"); }
                }
            }
            foreach (var header in new[] { "architecture-beta", "C4Context", "C4Container", "C4Component", "block", "block-beta" })
            {
                Render(header + "\n", "empty-" + header);
                var body = header == "architecture-beta" ? "service a[A]\n" : header.StartsWith("C4", StringComparison.Ordinal) ? "Person(a, \"A\")\n" : "a[A]\n";
                Render(header + "\n" + body, "simple-" + header);
            }

            const string architecture = "architecture-beta\ngroup g[Group]\nservice a(server)[Same] in g\n  service b[Same]\njunction j in g\na:R --> L:b\na:B -- T:j\n";
            var topology = Render(architecture, "architecture-membership");
            Check(G(topology, "architecture-service").Select(e => A(e, "data-id")).SequenceEqual(new[] { "a", "b" }), "Architecture: duplicate labels retain distinct IDs and declaration order");
            Check(Inside(Box(G(topology, "architecture-service")[0]), Box(G(topology, "architecture-group").Single())) && !Inside(Box(G(topology, "architecture-service")[1]), Box(G(topology, "architecture-group").Single())), "Architecture: explicit in membership controls actual group geometry; indentation does not");
            Check(G(topology, "architecture-junction").Single().Name.LocalName == "circle", "Architecture: canonical junction is retained as its actual routing point");
            Check(G(Render(architecture.Replace(" in g", "", StringComparison.Ordinal) + "a:R -- L:unknown\n", "architecture-unknown"), "architecture-edge").Length == 2, "Architecture: missing endpoint is omitted without minting a service");
            Check(G(Render(architecture + "{g}:R -- L:b\n", "architecture-group-endpoint"), "architecture-edge").Length == 2, "Architecture limitation: brace group endpoint is stripped by the parser but absent from native edge box lookup");
            foreach (var side in new[] { "L", "R", "T", "B" })
            {
                var r = Render("architecture-beta\nservice a[A]\nservice b[B]\na:" + side + " -- " + side + ":b\n", "architecture-port-" + side);
                var box = Box(G(r, "architecture-service")[0]); var path = A(G(r, "architecture-edge").Single(), "d");
                var numbers = SourceText.Pattern(@"-?\d+(?:\.\d+)?").Matches(path).Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var expected = side switch { "L" => (box.X, box.Y + box.H / 2), "R" => (box.X + box.W, box.Y + box.H / 2), "T" => (box.X + box.W / 2, box.Y), _ => (box.X + box.W / 2, box.Y + box.H) };
                Check(Math.Abs(numbers[0] - expected.Item1) < .02 && Math.Abs(numbers[1] - expected.Item2) < .02, "Architecture: actual " + side + " source port geometry");
            }
            foreach (var arrow in new[] { "--", "<--", "-->", "<-->" })
            {
                var edge = G(Render("architecture-beta\nservice a[A]\nservice b[B]\na:R " + arrow + " L:b\n", "architecture-arrow-" + Array.IndexOf(new[] { "--", "<--", "-->", "<-->" }, arrow)), "architecture-edge").Single();
                Check((edge.Attribute("marker-start") is not null) == arrow.StartsWith('<') && (edge.Attribute("marker-end") is not null) == arrow.EndsWith('>'), "Architecture: actual arrowheads " + arrow);
            }
            foreach (var icon in new[] { "cloud", "database", "disk", "internet", "server", "generic" })
            {
                var r = Render("architecture-beta\nservice a(" + icon + ")[A]\n", "architecture-icon-" + icon);
                Check(Mermaider.Icons.IconRegistry.TryGet(icon, out _) && !r.Svg!.Contains("<image", StringComparison.Ordinal), "Architecture: registered bundled pictogram " + icon + " emits native vector geometry");
                Check(NativeSvgService.ToPng(new NativeSvgService().Render(r.Svg!)).Length > 0, "Architecture: bundled pictogram " + icon + " passes trusted conversion");
            }
            var unknownIcon = Render("architecture-beta\nservice a(unknown-provider:remote)[A]\n", "architecture-icon-fallback");
            Check(G(unknownIcon, "architecture-service").Single().Attribute("data-icon")!.Value == "unknown-provider:remote" && !unknownIcon.Svg!.Contains("<image", StringComparison.Ordinal), "Architecture: unknown icon source identity survives generic fallback; fallback is not requested-icon proof");
            var vendor = Render("architecture-beta\nservice a(aws:compute)[A]\n", "architecture-vendor-protected");
            var rejected = false; try { new NativeSvgService().Render(vendor.Svg!); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Architecture: provider artwork remains protected by existing active/external image policy");

            foreach (var variant in new[] { "C4Context", "C4Container", "C4Component" })
            {
                foreach (var kind in new[] { "Person", "System", "SystemDb", "SystemQueue", "Container", "ContainerDb", "ContainerQueue", "Component", "ComponentDb", "ComponentQueue" })
                foreach (var external in new[] { "", "_Ext" })
                {
                    var r = Render(variant + "\n" + kind + external + "(a, \"Label\", \"Third\", \"Fourth\")\n", variant + "-kind-" + kind + external);
                    var element = G(r, "c4-element").Single();
                    Check(A(element, "data-id") == "a" && A(element, "data-kind") == kind.ToLowerInvariant() && A(element, "data-external") == (external.Length > 0 ? "true" : "false"), variant + ": exact alias/type/external " + kind + external);
                    Check(NativeSvgService.ToPng(new NativeSvgService().Render(r.Svg!)).Length > 0, variant + ": trusted native shape conversion " + kind + external);
                }
                const string pair = "\nPerson(a, \"A\", \"Description\")\nContainer(b, \"B\", \"Technology\", \"Details\")\n";
                var forward = Render(variant + pair + "Rel(a,b,\"Label\",\"Protocol\")\n", variant + "-forward");
                Check(Geometry(forward) == Geometry(Render(variant + pair + "Rel_Back(b,a,\"Label\",\"Protocol\")\n", variant + "-back")), variant + ": Rel_Back reverses actual endpoints without rewriting argument order");
                Check(Geometry(forward) == Geometry(Render(variant + pair + "Rel_D(a,b,\"Label\",\"Protocol\")\n", variant + "-ignored-direction")), variant + ": direction hint is ignored by native layout");
                Check(G(Render(variant + pair + "BiRel(a,b)\n", variant + "-bidirectional"), "c4-relation").Single().Attribute("marker-start") is not null, variant + ": BiRel retains both arrowheads");
                var empty = Render(variant + "\nContainer(a,\"A\",\"\",\"Description\",$tags=\"kept\")\n", variant + "-explicit-empty");
                Check(G(empty, "c4-element").Single().Value.Contains("Description", StringComparison.Ordinal), variant + ": explicit empty technology retains following description and ignores named token");
                Check(Geometry(Render(variant + "\nContainer(a,\"A\",,\"Description\")\n", variant + "-unquoted-empty")) == Geometry(Render(variant + "\nContainer(a,\"A\",\"Description\")\n", variant + "-shifted-empty-control")), variant + ": empty unquoted argument is dropped, shifting the following positional value");
                foreach (var boundary in new[] { "Boundary", "Enterprise_Boundary", "System_Boundary", "Container_Boundary" })
                {
                    var complete = variant + "\n" + boundary + "(g,\"Group\") {\nPerson(a,\"A\")\n}\n";
                    var r = Render(complete, variant + "-boundary-" + boundary);
                    Check(Inside(Box(G(r, "c4-element").Single()), Box(G(r, "c4-boundary").Single())), variant + ": actual containment " + boundary);
                    Check(Geometry(r) == Geometry(Render(complete.Replace(") {", ")\n{", StringComparison.Ordinal), variant + "-boundary-next-line-" + boundary)), variant + ": next-line boundary opening retains geometry " + boundary);
                }
            }
            var duplicateC4 = Render("C4Context\nPerson(a,\"Hidden\")\nPerson(a,\"Effective\")\nRel(a,missing)\n", "c4-duplicate-missing");
            Check(G(duplicateC4, "c4-element").Single().Value.Contains("Effective", StringComparison.Ordinal) && G(duplicateC4, "c4-relation").Length == 0, "C4: duplicate alias coalesces to final element; missing relationship endpoint is hidden");
            Check(G(Render("C4Context\nBoundary(g,\"Open\") {\nPerson(a,\"A\")\n", "c4-unclosed"), "c4-boundary").Length == 1, "C4 limitation: unclosed boundary flushes at EOF and must remain structurally protected");

            var block = Render("block\ncolumns 3\na[First]:2 space b(Second) a[Hidden]\n! ignored c[Ignored]\na --> b\n", "block-first-wins");
            Check(G(block, "node").Select(e => A(e, "data-id")).SequenceEqual(new[] { "a", "b" }) && A(G(block, "node")[0], "data-label") == "First", "Block: several tokens per line, first ID wins, malformed suffix stops parsing, no implicit nodes");
            Check(A(G(block, "node")[0], "data-shape") == "rectangle" && A(G(block, "node")[1], "data-shape") == "rounded", "Block: actual explicit rectangle and rounded forms");
            Check(Box(G(block, "node")[0]).W > Box(G(block, "node")[1]).W && Box(G(block, "node")[1]).Y > Box(G(block, "node")[0]).Y, "Block: actual span consumes two cells, spacer completes row, next block wraps");
            var spaces = Render("block\ncolumns 4\na space space b\n", "block-repeat-spacers");
            Check(G(spaces, "node").Length == 2 && Box(G(spaces, "node")[1]).X > Box(G(spaces, "node")[0]).X + 3 * Box(G(spaces, "node")[0]).W, "Block: repeated invisible spacers retain distinct ordered cells");
            Check(Geometry(Render("block\ncolumns 2\na:99 b\n", "block-clamped-span")) == Geometry(Render("block\ncolumns 2\na:2 b\n", "block-span-control")), "Block: over-wide span is visually clamped without proof that its source is valid editor input");
            Check(G(Render("block\na b\na --> a\na --> missing\n", "block-hidden-links"), "block-edge").Length == 0, "Block: self and missing links are hidden rather than invented");
            var enormous = Render("block\ncolumns 2147483647\na[A]\n", "block-enormous-columns");
            var enormousDrawing = new NativeSvgService().Render(enormous.Svg!); var imageRejected = false;
            try { NativeSvgService.ToPng(enormousDrawing); } catch (IOException error) { imageRejected = error.Message.Contains("too large", StringComparison.Ordinal); }
            Check(imageRejected && enormous.Source.Contains("2147483647", StringComparison.Ordinal), "Block: enormous native grid dimensions fail before integer narrowing/image allocation and retain original source");
            Receipt("PASS", null);
        }
        catch (Exception error) { Receipt("FAIL", error.ToString()); throw; }

        DiagramResult Render(string source, string name, bool dark = false)
        {
            var r = diagrams.Render(source, dark); File.WriteAllText(Path.Combine(output, name + ".mmd"), source); File.WriteAllText(Path.Combine(output, name + ".svg"), r.Svg ?? r.Error);
            observations.Add(new { Name = name, Dark = dark, SourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(), r.Success, r.Error });
            Check(r.Success && r.Source == source, name + ": exact authoritative source renders"); return r;
        }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        void Receipt(string status, string? error) => File.WriteAllText(Path.Combine(output, "topology-compatibility.json"), JsonSerializer.Serialize(new { Status = status, Error = error, SourceRevision = typeof(DiagramService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, Renderer = "Mermaider 0.15.1", RendererSource = "71b64845570d633a94d29e85579c0a02786fece8", Checks = checks, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static byte[] Pixels(byte[] png) => NativeSourceEditorChecks.Pixels(png);
    private static XElement[] G(DiagramResult result, string cls) => SourceDeclarations.Groups(result.Svg!, cls);
    private static string A(XElement element, string name) => (string?)element.Attribute(name) ?? "";
    private static (double X, double Y, double W, double H) Box(XElement element)
    { var box = element.DescendantsAndSelf().First(e => e.Name.LocalName == "rect"); return (ChartSource.Attribute(box, "x"), ChartSource.Attribute(box, "y"), ChartSource.Attribute(box, "width"), ChartSource.Attribute(box, "height")); }
    private static bool Inside((double X, double Y, double W, double H) inner, (double X, double Y, double W, double H) outer) => inner.X >= outer.X && inner.Y >= outer.Y && inner.X + inner.W <= outer.X + outer.W && inner.Y + inner.H <= outer.Y + outer.H;
    private static string Geometry(DiagramResult result) => string.Join("|", XDocument.Parse(result.Svg!).Descendants().Where(e => e.Name.LocalName is "path" or "circle" or "rect").Select(e => string.Join(";", e.Attributes().Where(a => a.Name.LocalName is "d" or "cx" or "cy" or "x" or "y" or "width" or "height"))));
}
