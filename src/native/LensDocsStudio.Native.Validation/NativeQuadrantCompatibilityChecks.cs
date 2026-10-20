using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeQuadrantCompatibilityChecks
{
    public static void Run(string output, string repository)
    {
        Directory.CreateDirectory(output);
        var source = File.ReadAllText(Path.Combine(repository, "tests/fixtures/native-mermaid/quadrant.mmd"));
        var checks = new List<string>();
        foreach (var dark in new[] { false, true })
        {
            var name = dark ? "dark" : "light";
            var result = new DiagramService().Render(source, dark);
            Check(result.Success && result.Source == source, name + ": ordinary adapter retains authoritative source");
            var root = XDocument.Parse(result.Svg!).Root!;
            var quadrants = root.Descendants().Where(e => (string?)e.Attribute("class") == "quadrant").ToDictionary(e => (string)e.Attribute("data-quadrant")!);
            var topLeft = quadrants["2"].Elements().First();
            var bottomRight = quadrants["4"].Elements().First();
            double Number(XElement e, string attribute) => double.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);
            var left = Number(topLeft, "x"); var top = Number(topLeft, "y");
            var width = Number(bottomRight, "x") + Number(bottomRight, "width") - left;
            var height = Number(bottomRight, "y") + Number(bottomRight, "height") - top;
            Check(Number(quadrants["1"].Elements().First(), "x") > left && Number(quadrants["3"].Elements().First(), "y") > top,
                name + ": engine numbering is 1 top right, 2 top left, 3 bottom left, 4 bottom right");
            var points = root.Descendants().Where(e => (string?)e.Attribute("class") == "quadrant-point").ToArray();
            var values = new[] { (.25, .75), (.75, .25), (.5, .5), (.5, .5), (0d, 0d), (1d, 1d) };
            Check(points.Length == values.Length, name + ": all duplicate, coincident, boundary and limit occurrences render");
            for (var i = 0; i < points.Length; i++)
            {
                var circle = points[i].Elements().First();
                Check(Math.Abs(Number(circle, "cx") - (left + values[i].Item1 * width)) < .001 &&
                    Math.Abs(Number(circle, "cy") - (top + (1 - values[i].Item2) * height)) < .001,
                    name + ": canonical point " + i + " follows actual panel extent and inverted Y");
            }
            var native = new NativeSvgService(); var prepared = native.Prepare(result.Svg!);
            var drawing = native.Render(result.Svg!); var png = NativeSvgService.ToPng(drawing);
            Check(png.Length > 1000 && drawing.Width > 400 && drawing.Height > 400, name + ": trusted native conversion produces a meaningful PNG");
            File.WriteAllText(Path.Combine(output, name + ".svg"), prepared); File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
            var document = "# Quadrant compatibility\n\n```mermaid\n" + source + "\n```\n";
            foreach (var format in new[] { "html", "docx", "pdf" })
            {
                var bytes = new DocumentExportService().ExportAsync(format.ToUpperInvariant(), document, null, null, dark, null).GetAwaiter().GetResult();
                Check(bytes.Length > 1000, name + ": ordinary " + format + " export includes the native diagram");
                File.WriteAllBytes(Path.Combine(output, name + "." + format), bytes);
            }
        }
        File.WriteAllText(Path.Combine(output, "quadrant-compatibility.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
    }
}
