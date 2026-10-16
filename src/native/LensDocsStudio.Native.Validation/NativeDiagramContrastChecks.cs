using System.Text.Json;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeDiagramContrastChecks
{
    public static void Run(string output)
    {
        var renderer = new NativeSvgService();
        var diagrams = new DiagramService();
        var cases = new[]
        {
            (Name: "dark-pale-node", Dark: true, Style: "fill:#edf1f7,stroke:#52647a", Expected: "#202124"),
            (Name: "light-dark-node", Dark: false, Style: "fill:#202124,stroke:#8995A7", Expected: "#F3F4F6"),
            (Name: "dark-authored-colour", Dark: true, Style: "fill:#edf1f7,color:#F3F4F6", Expected: "#F3F4F6"),
            (Name: "light-readable-node", Dark: false, Style: "fill:#edf1f7,stroke:#52647a", Expected: "#202124"),
            (Name: "dark-middle-grey", Dark: true, Style: "fill:#777777", Expected: "#000000")
        };
        var results = new List<object>();
        foreach (var item in cases)
        {
            var source = "flowchart LR\nA[\"Portable<br/>files\"] --> B[Export]\nstyle A " + item.Style;
            var diagram = diagrams.Render(source, item.Dark);
            if (!diagram.Success) throw new IOException(diagram.Error);
            var svg = renderer.Prepare(diagram.Svg!);
            var xml = XDocument.Parse(svg);
            if (XDocument.Parse(renderer.Prepare(svg)).Root!.Elements().Count(e => (string?)e.Attribute("class") == "native-background") != 1)
                throw new IOException("Repeated SVG preparation duplicated the diagram background.");
            var node = xml.Descendants().Single(e => (string?)e.Attribute("data-id") == "A");
            var label = node.Elements().Single(e => e.Name.LocalName == "text");
            if ((string?)label.Attribute("fill") != item.Expected || label.Value != "Portablefiles" || diagram.Source != source)
                throw new IOException("Diagram label contrast or source preservation failed: " + item.Name);
            File.WriteAllText(Path.Combine(output, "native-contrast-" + item.Name + ".svg"), svg);
            var png = NativeSvgService.ToPng(renderer.Render(diagram.Svg!));
            File.WriteAllBytes(Path.Combine(output, "native-contrast-" + item.Name + ".png"), png);
            using var stream = new MemoryStream(png);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var pixels = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var corner = new byte[4]; pixels.CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
            var expectedCorner = item.Dark ? new byte[] { 0x24, 0x21, 0x20, 255 } : new byte[] { 250, 250, 250, 255 };
            if (!corner.SequenceEqual(expectedCorner)) throw new IOException("Diagram export lost its declared opaque background: " + item.Name);
            results.Add(new { item.Name, item.Expected, SourcePreserved = true, OpaqueThemeBackground = true });
        }
        const string classSource = "flowchart LR\nA[Class colour]\nclassDef pale fill:#edf1f7\nclass A pale";
        var classXml = XDocument.Parse(renderer.Prepare(diagrams.Render(classSource, true).Svg!));
        if ((string?)classXml.Descendants().Single(e => e.Name.LocalName == "text" && e.Value == "Class colour").Attribute("fill") != "#202124")
            throw new IOException("Class-based node fill lost inherited label contrast.");
        results.Add(new { Name = "class-fill", Expected = "#202124", SourcePreserved = true });
        File.WriteAllText(Path.Combine(output, "native-diagram-contrast.json"), JsonSerializer.Serialize(new { Passed = results.Count, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
