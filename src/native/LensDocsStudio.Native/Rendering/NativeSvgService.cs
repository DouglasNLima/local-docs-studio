using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Materialises Mermaider CSS colour tokens for a native SVG 1.1 renderer. Never accepts arbitrary remote SVG.</summary>
public sealed partial class NativeSvgService
{
    private readonly Dictionary<string, DrawingImage> drawings = new(StringComparer.Ordinal);
    private readonly Queue<string> drawingOrder = new();
    private int cachedCharacters;
    public string Prepare(string generatedSvg)
    {
        using var reader = XmlReader.Create(new StringReader(generatedSvg), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var xml = XDocument.Load(reader);
        var root = xml.Root ?? throw new InvalidDataException("The generated diagram is empty.");
        if (root.Name != XName.Get("svg", "http://www.w3.org/2000/svg")) throw new InvalidDataException("The generated drawing is not an SVG document.");
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in root.Descendants().Where(e => e.Name.LocalName == "style"))
            foreach (Match match in Variable().Matches(style.Value)) variables[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        foreach (Match match in Variable().Matches(root.Attribute("style")?.Value ?? ""))
            variables[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.Namespace != root.Name.Namespace || element.Name.LocalName is "script" or "foreignObject" or "image" or "a" or "animate" or "set" or "animateTransform")
                throw new InvalidDataException("The diagram contains unsupported active or external content.");
            foreach (var attribute in element.Attributes().ToArray())
            {
                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) || attribute.Name.LocalName == "href" || attribute.Name == XNamespace.Xml + "base")
                    throw new InvalidDataException("The diagram contains an active reference.");
                if (attribute.Name.LocalName is "style" or "fill" or "stroke" or "filter" or "clip-path" or "mask" or "marker" or "marker-start" or "marker-mid" or "marker-end" or "cursor" or "font" or "font-family" or "color-profile")
                    RejectExternalCss(attribute.Value);
                attribute.Value = Resolve(attribute.Value, variables, 0);
            }
            if (element.Name.LocalName == "style")
            {
                RejectExternalCss(element.Value);
                element.Value = Variable().Replace(Resolve(element.Value, variables, 0), "");
            }
        }
        root.SetAttributeValue("font-family", "Segoe UI");
        return xml.ToString(SaveOptions.DisableFormatting);
    }
    private static void RejectExternalCss(string value)
    {
        if (value.Contains('\\') || Regex.IsMatch(value, @"@(?:import|font-face)|expression\s*\(|(?:https?|ftp|file|data|javascript|vbscript):", RegexOptions.IgnoreCase))
            throw new InvalidDataException("External or active diagram styles are not supported.");
        foreach (Match match in Regex.Matches(value, @"url\s*\(([^)]*)\)", RegexOptions.IgnoreCase))
        {
            var reference = match.Groups[1].Value.Trim().Trim('\'', '"');
            if (!Regex.IsMatch(reference, @"^#[A-Za-z_][A-Za-z0-9_.:-]*$"))
                throw new InvalidDataException("Diagram styles may only reference local SVG definitions.");
        }
    }

    public DrawingImage Render(string generatedSvg)
    {
        lock (drawings) if (drawings.TryGetValue(generatedSvg, out var cached)) return cached;
        var svg = Prepare(generatedSvg);
        using var text = new StringReader(svg);
        using var reader = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true });
        var drawing = reader.Read(text) ?? throw new InvalidDataException("The native SVG renderer produced no drawing.");
        if (drawing.Bounds.IsEmpty || drawing.Bounds.Width < 1 || drawing.Bounds.Height < 1) throw new InvalidDataException("The diagram has no visible geometry.");
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        if (generatedSvg.Length <= 2_000_000)
        {
            lock (drawings)
            {
                if (drawings.TryGetValue(generatedSvg, out var cached)) return cached;
                while (drawingOrder.Count > 0 && (drawings.Count >= 32 || cachedCharacters + generatedSvg.Length > 2_000_000))
                {
                    var oldest = drawingOrder.Dequeue(); drawings.Remove(oldest); cachedCharacters -= oldest.Length;
                }
                drawings.Add(generatedSvg, image); drawingOrder.Enqueue(generatedSvg); cachedCharacters += generatedSvg.Length;
            }
        }
        return image;
    }

    public static byte[] ToPng(ImageSource source, double scale = 2)
    {
        if (!double.IsFinite(source.Width) || !double.IsFinite(source.Height) || source.Width <= 0 || source.Height <= 0)
            throw new InvalidDataException("The diagram dimensions are invalid.");
        var width = (int)Math.Ceiling(source.Width * scale);
        var height = (int)Math.Ceiling(source.Height * scale);
        if (width > 16000 || height > 16000 || (long)width * height > 64_000_000) throw new IOException("This diagram is too large to export as one image.");
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen()) context.DrawImage(source, new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }

    private static string Resolve(string value, Dictionary<string, string> variables, int depth)
    {
        if (depth > 32) throw new InvalidDataException("Diagram styles contain a circular reference.");
        for (var iteration = 0; iteration < 2000; iteration++)
        {
            var match = Function().Match(value);
            if (!match.Success) break;
            var start = match.Index; var position = start + match.Length; var nesting = 1;
            while (position < value.Length && nesting != 0)
            {
                if (value[position] == '(') nesting++;
                if (value[position] == ')') nesting--;
                position++;
            }
            if (nesting != 0) throw new InvalidDataException("Invalid generated diagram style.");
            var arguments = Split(value[(start + match.Length)..(position - 1)]);
            string replacement;
            if (match.Value == "var(")
            {
                var key = arguments[0].Trim();
                replacement = variables.TryGetValue(key, out var token) ? Resolve(token, variables, depth + 1)
                    : arguments.Count > 1 ? Resolve(arguments[1], variables, depth + 1)
                    : throw new InvalidDataException("Unresolved diagram style: " + key);
            }
            else
            {
                if (arguments.Count != 3 || arguments[0].Trim() != "in srgb") throw new InvalidDataException("Unsupported diagram colour interpolation.");
                var first = Colour(Resolve(arguments[1], variables, depth + 1));
                var second = Colour(Resolve(arguments[2], variables, depth + 1));
                var weight = first.Weight ?? (second.Weight.HasValue ? 1 - second.Weight.Value : .5);
                byte Mix(byte a, byte b) => (byte)Math.Clamp(Math.Round(a * weight + b * (1 - weight)), 0, 255);
                var mixed = Color.FromArgb(Mix(first.Colour.A, second.Colour.A), Mix(first.Colour.R, second.Colour.R), Mix(first.Colour.G, second.Colour.G), Mix(first.Colour.B, second.Colour.B));
                replacement = mixed.A == 255 ? $"#{mixed.R:X2}{mixed.G:X2}{mixed.B:X2}" : $"rgba({mixed.R},{mixed.G},{mixed.B},{(mixed.A / 255d).ToString(CultureInfo.InvariantCulture)})";
            }
            value = value[..start] + replacement + value[position..];
        }
        return Rem().Replace(value, m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 16).ToString(CultureInfo.InvariantCulture));
    }
    private static (Color Colour, double? Weight) Colour(string value)
    {
        var match = Weight().Match(value.Trim());
        double? weight = match.Success ? double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) / 100 : null;
        var colour = match.Success ? match.Groups[1].Value.Trim() : value.Trim();
        return ((Color)ColorConverter.ConvertFromString(colour), weight);
    }
    private static List<string> Split(string text)
    {
        var result = new List<string>(); var depth = 0; var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
            else if (text[i] == ',' && depth == 0) { result.Add(text[start..i]); start = i + 1; }
        }
        result.Add(text[start..]); return result;
    }
    [GeneratedRegex(@"(--[\w-]+)\s*:\s*([^;{}]+);?")] private static partial Regex Variable();
    [GeneratedRegex(@"var\(|color-mix\(")] private static partial Regex Function();
    [GeneratedRegex(@"([\d.]+)rem")] private static partial Regex Rem();
    [GeneratedRegex(@"^(.*?)\s+([\d.]+)%$")] private static partial Regex Weight();
}
