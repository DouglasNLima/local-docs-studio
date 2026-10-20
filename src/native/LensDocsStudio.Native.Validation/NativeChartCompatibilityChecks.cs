using System.Globalization;
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

/// <summary>Compatibility gate through production adapters; no private engine API or substitute layout.</summary>
internal static class NativeChartCompatibilityChecks
{
    public static void Run(string output, string repository)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var captures = new List<object>(); var diagrams = new DiagramService();
        foreach (var family in new[] { "xychart", "radar", "sankey" })
        foreach (var dark in new[] { false, true })
        {
            var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid", family + ".mmd"));
            var result = diagrams.Render(source, dark);
            Check(result.Success && result.Source == source, family + ": ordinary native adapter retains exact source");
            var root = XDocument.Parse(result.Svg!).Root!;
            if (family == "xychart") Xy(root, source, dark);
            else if (family == "radar") Radar(root, source, dark);
            else Sankey(root, source, dark);
            var native = new NativeSvgService(); var trusted = native.Prepare(result.Svg!);
            var drawing = native.Render(result.Svg!); var png = NativeSvgService.ToPng(drawing);
            Check(png.Length > 1000 && drawing.Width > 400 && drawing.Height > 300, family + ": trusted SVG converts to meaningful native PNG");
            var markdown = "# Pinned " + family + " compatibility\n\n```mermaid\n" + source + "\n```\n";
            var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.OfType<FencedCodeBlock>().Single();
            var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { });
            var ordinaryPng = NativeSvgService.ToPng(preview.Media.Single().Image.Source);
            Check(NativeSourceEditorChecks.Pixels(png).SequenceEqual(NativeSourceEditorChecks.Pixels(ordinaryPng)), family + ": complete ordinary document preview matches canonical native pixels");
            var name = family + (dark ? "-dark" : "-light");
            File.WriteAllText(Path.Combine(output, name + ".svg"), trusted); File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
            foreach (var format in new[] { "HTML", "DOCX", "PDF" })
            {
                var bytes = new DocumentExportService().ExportAsync(format, markdown, null, null, dark, null).GetAwaiter().GetResult();
                if (format == "HTML")
                {
                    var html = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(Encoding.UTF8.GetString(bytes));
                    var image = html.QuerySelector("figure img")?.GetAttribute("src") ?? "";
                    Check(image.StartsWith("data:image/png;base64,", StringComparison.Ordinal) &&
                        NativeSourceEditorChecks.Pixels(Convert.FromBase64String(image[22..])).SequenceEqual(NativeSourceEditorChecks.Pixels(png)), family + ": ordinary HTML embeds the exact canonical PNG pixels");
                }
                else if (format == "DOCX")
                {
                    using var docx = WordprocessingDocument.Open(new MemoryStream(bytes), false);
                    Check(!new OpenXmlValidator().Validate(docx).Any(), family + ": ordinary DOCX passes Open XML validation");
                    var image = docx.MainDocumentPart!.ImageParts.Single(p => p.ContentType == "image/png");
                    using var imageBytes = new MemoryStream(); image.GetStream().CopyTo(imageBytes);
                    Check(NativeSourceEditorChecks.Pixels(imageBytes.ToArray()).SequenceEqual(NativeSourceEditorChecks.Pixels(png)), family + ": ordinary DOCX embeds exact canonical diagram pixels");
                }
                else
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
                    Check(pdf.GetPages().SelectMany(p => p.GetImages()).Any(), family + ": ordinary PDF contains an actual parsed diagram image");
                }
                File.WriteAllBytes(Path.Combine(output, name + "." + format.ToLowerInvariant()), bytes);
            }
            captures.Add(new { Family = family, Dark = dark, SourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(), NativeWidth = drawing.Width, NativeHeight = drawing.Height });
        }
        File.WriteAllText(Path.Combine(output, "chart-compatibility.json"), JsonSerializer.Serialize(new
        {
            Status = "PASS", SourceRevision = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(XyVisualEditorWindow).Assembly)!.InformationalVersion,
            Renderer = "Mermaider 0.15.1", RendererSource = "71b64845570d633a94d29e85579c0a02786fece8", Checks = checks, Captures = captures
        }, new JsonSerializerOptions { WriteIndented = true }));

        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        XElement[] Groups(XElement root, string cls) => root.Descendants().Where(e => (string?)e.Attribute("class") == cls).ToArray();
        string Geometry(XElement element) => string.Join("|", element.Elements().Select(e => e.Name.LocalName + ":" + string.Join(";", e.Attributes().Where(a => new[] { "d", "points", "cx", "cy", "x", "y", "width", "height" }.Contains(a.Name.LocalName)))));
        double Number(XElement e, string name) => double.Parse((string)e.Attribute(name)!, CultureInfo.InvariantCulture);
        void Xy(XElement root, string source, bool dark)
        {
            var bars = Groups(root, "xy-bars"); var lines = Groups(root, "xy-line");
            Check(bars.Length == 1 && (string?)bars[0].Attribute("data-series") == "1" && lines.Select(e => (string?)e.Attribute("data-series")).SequenceEqual(new[] { "0", "2" }), "XY: real renderer paints bars under lines while retaining declaration indices");
            Check(bars[0].Elements().Count() == 4 && lines.All(e => e.Elements().Count(p => p.Name.LocalName == "circle") == 4), "XY: negative, zero and positive values retain every occurrence");
            Check(Geometry(lines[0]) == Geometry(lines[1]), "XY: duplicate named coincident series retain identical canonical geometry and distinct occurrences");
            Check(root.Value.Contains("Guides, reference", StringComparison.Ordinal) && root.Value.Contains("say \"hello\"", StringComparison.Ordinal) && SourceDeclarations.Groups(root.ToString(), "diagram-title").Single().Value.Contains("Documentation", StringComparison.Ordinal), "XY: actual doubled-quote category parsing and body title precedence");
            var horizontal = diagrams.Render(source.Replace("vertical", "horizontal", StringComparison.Ordinal), dark);
            Check(horizontal.Success, "XY: horizontal variant renders through ordinary adapter");
            var h = XDocument.Parse(horizontal.Svg!).Root!; var hp = Groups(h, "xy-line")[0].Elements().Where(e => e.Name.LocalName == "circle").ToArray();
            var vp = lines[0].Elements().Where(e => e.Name.LocalName == "circle").ToArray();
            Check(Number(hp[0], "cx") < Number(hp[3], "cx") && Number(hp[0], "cy") < Number(hp[3], "cy") && Number(vp[0], "cy") > Number(vp[3], "cy"), "XY: canonical horizontal values grow right; category order grows down, vertical values grow up");
            File.WriteAllText(Path.Combine(output, "xychart-horizontal" + (dark ? "-dark" : "-light") + ".svg"), horizontal.Svg);
            var invalid = diagrams.Render("xychart\nx-axis [a,b,c]\nbar [1,invalid,3]\n", dark);
            var zero = diagrams.Render("xychart\nx-axis [a,b,c]\nbar [1,0,3]\n", dark);
            Check(Geometry(Groups(XDocument.Parse(invalid.Svg!).Root!, "xy-bars")[0]) == Geometry(Groups(XDocument.Parse(zero.Svg!).Root!, "xy-bars")[0]), "XY limitation: invalid token coerces to zero; rendering alone cannot validate author input");
            var ranged = diagrams.Render("xychart\nx-axis 100 --> 200\nline [1,2,3]\n", dark);
            var automatic = diagrams.Render("xychart\nline [1,2,3]\n", dark);
            Check(Geometry(Groups(XDocument.Parse(ranged.Svg!).Root!, "xy-line")[0]) == Geometry(Groups(XDocument.Parse(automatic.Svg!).Root!, "xy-line")[0]), "XY limitation: numeric X range does not establish continuous-X geometry");
        }
        void Radar(XElement root, string source, bool dark)
        {
            foreach (var axes in new[] { "A", "A,B" })
            {
                var minimal = diagrams.Render("radar-beta\naxis " + axes + "\ncurve c{" + (axes == "A" ? "1" : "1,2") + "}\n", dark);
                var png = NativeSvgService.ToPng(new NativeSvgService().Render(minimal.Svg ?? throw new IOException(minimal.Error)));
                Check(png.Length > 100 && Groups(XDocument.Parse(minimal.Svg!).Root!, "radar-curve")[0].Elements().Count(e => e.Name.LocalName == "circle") == axes.Split(',').Length,
                    "Radar: actual one/two-axis canonical marks convert through the ordinary native SVG/PNG path without an invented minimum");
            }
            var curves = Groups(root, "radar-curve");
            Check(curves.Select(e => (string?)e.Attribute("data-curve")).SequenceEqual(new[] { "first", "second", "third" }) && curves.All(e => e.Elements().Count(p => p.Name.LocalName == "circle") == 4), "Radar: curve declaration order and every positional mark survive canonical rendering");
            Check(Geometry(curves[0]) == Geometry(curves[1]), "Radar: overlapping curves have identical real geometry and remain separate occurrences");
            var prefixed = diagrams.Render(source.Replace("{2.000, +4e0, 6, 8.000}", "{review:2.000, guides:+4e0, examples:6, reference:8.000}", StringComparison.Ordinal), dark);
            Check(Geometry(Groups(XDocument.Parse(prefixed.Svg!).Root!, "radar-curve")[0]) == Geometry(curves[0]), "Radar limitation: apparent keyed prefixes still map positionally");
            var dropped = diagrams.Render("radar-beta\naxis A,B,C\ncurve c{1,invalid,3}\n", dark);
            var ragged = diagrams.Render("radar-beta\naxis A,B,C\ncurve c{1,3}\n", dark);
            Check(Geometry(Groups(XDocument.Parse(dropped.Svg!).Root!, "radar-curve")[0]) == Geometry(Groups(XDocument.Parse(ragged.Svg!).Root!, "radar-curve")[0]), "Radar limitation: invalid value drops and renderer fills missing final positions");
            var unsupportedRange = diagrams.Render("radar-beta\naxis A,B,C\nmax 1e2\ncurve c{1,2,3}\n", dark);
            var auto = diagrams.Render("radar-beta\naxis A,B,C\ncurve c{1,2,3}\n", dark);
            Check(Geometry(Groups(XDocument.Parse(unsupportedRange.Svg!).Root!, "radar-curve")[0]) == Geometry(Groups(XDocument.Parse(auto.Svg!).Root!, "radar-curve")[0]), "Radar limitation: exponent max directive is ignored, unlike general value syntax");
        }
        void Sankey(XElement root, string source, bool dark)
        {
            var nodes = Groups(root, "sankey-node"); var links = Groups(root, "sankey-link");
            Check(nodes.Select(e => (string?)e.Attribute("data-node")).SequenceEqual(new[] { "Review", "Published", "Draft, Ω", "say \"hello\"" }), "Sankey: actual node identities use trimmed CSV text, embedded commas and doubled quotes in first-appearance order");
            Check(links.Length == 4 && (string?)links[0].Attribute("d") != (string?)links[1].Attribute("d"), "Sankey: parallel equal links remain separate stacked canonical ribbons");
            var invalid = diagrams.Render(source + "\nA,B,0\nA,B,-1\nA,B,NaN\n,B,2\ninvalid row\n", dark);
            Check(Groups(XDocument.Parse(invalid.Svg!).Root!, "sankey-link").Select(e => (string?)e.Attribute("d")).SequenceEqual(links.Select(e => (string?)e.Attribute("d"))), "Sankey limitation: invalid rows and blank endpoints are ignored without becoming valid links");
            var cyclic = diagrams.Render("sankey-beta\nA,A,5\nA,B,3\nB,C,3\nC,A,3\n", dark);
            Check(cyclic.Success && Groups(XDocument.Parse(cyclic.Svg!).Root!, "sankey-link").Length < 4, "Sankey limitation: self-links and feedback ribbons can be omitted; topology operations require protection");
            var overflow = diagrams.Render("sankey-beta\nA,B,1e20\n", dark);
            var label = Groups(XDocument.Parse(overflow.Svg!).Root!, "sankey-node")[0].Elements().Last(e => e.Name.LocalName == "text").Value;
            Check(label != "100000000000000000000", "Sankey limitation: integral node total 1e20 has incorrect pinned label " + label + "; authoring protects totals at or above 2^63");
        }
    }
}
