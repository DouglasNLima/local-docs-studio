using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Materialises Mermaider CSS colour tokens for a native SVG 1.1 renderer. Never accepts arbitrary remote SVG.</summary>
public sealed partial class NativeSvgService
{
    private readonly Dictionary<string, DrawingImage> drawings = new(StringComparer.Ordinal);
    private readonly Queue<string> drawingOrder = new();
    private int cachedCharacters;
    public string Prepare(string generatedSvg) => PrepareDocument(generatedSvg).ToString(SaveOptions.DisableFormatting);
    internal XDocument PrepareDocument(string generatedSvg)
    {
        using var reader = XmlReader.Create(new StringReader(generatedSvg), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var xml = XDocument.Load(reader);
        var root = xml.Root ?? throw new InvalidDataException("The generated diagram is empty.");
        if (root.Name != XName.Get("svg", "http://www.w3.org/2000/svg")) throw new InvalidDataException("The generated drawing is not an SVG document.");
        // Mermaider marks inherited node text with this token; literal authored colours must retain precedence.
        var inheritedNodeLabels = root.Descendants().Where(e => e.Name.LocalName == "text"
            && (string?)e.Attribute("fill") == "var(--_text)" && (string?)e.Parent?.Attribute("class") == "node").ToArray();
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
                // Inert source metadata is literal text, even when a label mentions CSS functions or rem units.
                if (!attribute.Name.LocalName.StartsWith("data-", StringComparison.Ordinal)) attribute.Value = Resolve(attribute.Value, variables, 0);
            }
            if (element.Name.LocalName == "style")
            {
                RejectExternalCss(element.Value);
                element.Value = Variable().Replace(Resolve(element.Value, variables, 0), "");
            }
        }
        foreach (var label in inheritedNodeLabels) EnsureNodeLabelContrast(label);
        AddDeclaredBackground(root, variables);
        root.SetAttributeValue("font-family", "Segoe UI");
        return xml;
    }
    private static void AddDeclaredBackground(XElement root, Dictionary<string, string> variables)
    {
        // CSS theme variables do not paint a WPF drawing. Keep exported labels readable on any destination surface.
        if (root.Elements().Any(e => e.Name.LocalName == "rect" && (string?)e.Attribute("class") == "native-background")) return;
        if (!variables.TryGetValue("--bg", out var value) || !TrySolidColour(Resolve(value, variables, 0), out var colour)) return;
        var parts = ((string?)root.Attribute("viewBox") ?? "").Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return;
        var bounds = new double[4];
        for (var index = 0; index < bounds.Length; index++)
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out bounds[index]) || !double.IsFinite(bounds[index])) return;
        if (bounds[2] <= 0 || bounds[3] <= 0) return;
        root.AddFirst(new XElement(root.Name.Namespace + "rect", new XAttribute("class", "native-background"),
            new XAttribute("x", parts[0]), new XAttribute("y", parts[1]), new XAttribute("width", parts[2]), new XAttribute("height", parts[3]),
            new XAttribute("fill", $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}")));
    }
    private static void EnsureNodeLabelContrast(XElement label)
    {
        // Only a single opaque solid shape has an unambiguous background. Gradients and translucent shapes retain the renderer's colours.
        var shapes = label.Parent!.Elements().Where(e => e.Name.LocalName is "rect" or "circle" or "ellipse" or "polygon" or "path").ToArray();
        if (shapes.Length != 1 || shapes[0].Attribute("opacity") is not null || shapes[0].Attribute("fill-opacity") is not null
            || !TrySolidColour((string?)shapes[0].Attribute("fill"), out var background)
            || !TrySolidColour((string?)label.Attribute("fill"), out var foreground)) return;
        if (Contrast(background, foreground) >= 4.5) return;
        var dark = Color.FromRgb(0x20, 0x21, 0x24);
        var light = Color.FromRgb(0xF3, 0xF4, 0xF6);
        var darkContrast = Contrast(background, dark); var lightContrast = Contrast(background, light);
        var replacement = darkContrast >= lightContrast ? "#202124" : "#F3F4F6";
        if (Math.Max(darkContrast, lightContrast) < 4.5)
            replacement = Contrast(background, Colors.Black) >= Contrast(background, Colors.White) ? "#000000" : "#FFFFFF";
        label.SetAttributeValue("fill", replacement);
    }
    internal static bool TrySolidColour(string? value, out Color colour)
    {
        colour = default;
        if (string.IsNullOrWhiteSpace(value) || value.Contains('(') || value is "none" or "currentColor") return false;
        try { colour = (Color)ColorConverter.ConvertFromString(value); return colour.A == 255; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }
    private static double Contrast(Color first, Color second)
    {
        static double Luminance(Color colour)
        {
            static double Channel(byte value) { var scaled = value / 255d; return scaled <= .04045 ? scaled / 12.92 : Math.Pow((scaled + .055) / 1.055, 2.4); }
            return .2126 * Channel(colour.R) + .7152 * Channel(colour.G) + .0722 * Channel(colour.B);
        }
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
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
        var image = RenderPrepared(svg);
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

    // Frozen drawings have no Dispatcher ownership. Share conversion only, never semantic
    // models, occurrence maps, mutable session groups or editor windows. The process pins
    // Mermaider 0.15.1 and SharpVectors; an upgrade starts a new process/cache.
    private sealed record CanonicalKey(string Source, string? Theme, string PreparedSvg, bool Identities);
    private static readonly Dictionary<CanonicalKey, DrawingImage> canonicalDrawings = new();
    private static readonly Queue<CanonicalKey> canonicalOrder = new();
    private static readonly SemaphoreSlim canonicalConversion = new(1, 1);
    private static int canonicalCharacters;
    private const int CanonicalCharacterLimit = 2_000_000, CanonicalEntryLimit = 8;
    internal DrawingImage RenderCanonical(DiagramResult render, string preparedSvg, bool identities = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = new CanonicalKey(render.Source, render.Theme, preparedSvg, identities);
        lock (canonicalDrawings) if (canonicalDrawings.TryGetValue(key, out var existing)) return existing;
        // Conversion itself is not interruptible. Queued obsolete callers can cancel
        // before entering SharpVectors, and no cancelled result enters the cache.
        canonicalConversion.Wait(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (canonicalDrawings) if (canonicalDrawings.TryGetValue(key, out var existing)) return existing;
            var image = RenderPrepared(preparedSvg, identities);
            cancellationToken.ThrowIfCancellationRequested();
            var cost = render.Source.Length + preparedSvg.Length;
            if (cost <= CanonicalCharacterLimit)
            {
                lock (canonicalDrawings)
                {
                    while (canonicalOrder.Count > 0 && (canonicalDrawings.Count >= CanonicalEntryLimit || canonicalCharacters + cost > CanonicalCharacterLimit))
                    {
                        var oldest = canonicalOrder.Dequeue(); canonicalDrawings.Remove(oldest);
                        canonicalCharacters -= oldest.Source.Length + oldest.PreparedSvg.Length;
                    }
                    canonicalDrawings.Add(key, image); canonicalOrder.Enqueue(key); canonicalCharacters += cost;
                }
            }
            return image;
        }
        finally { canonicalConversion.Release(); }
    }

    // Only callers which have passed Prepare may use this conversion. Runtime metadata is inert;
    // TextAsGeometry and the active-content rejection remain identical to the document preview.
    internal static DrawingImage RenderPrepared(string safeSvg, bool identities = false)
    {
        using var text = new StringReader(safeSvg);
        using var reader = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = identities, TextAsGeometry = true });
        var drawing = reader.Read(text) ?? throw new InvalidDataException("The native SVG renderer produced no drawing.");
        if (drawing.Bounds.IsEmpty || drawing.Bounds.Width < 1 || drawing.Bounds.Height < 1) throw new InvalidDataException("The diagram has no visible geometry.");
        drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return image;
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
        var functional = RgbColour().Match(colour);
        if (functional.Success)
        {
            if (functional.Groups["rgba"].Success != functional.Groups["alpha"].Success)
                throw new InvalidDataException("The generated RGB colour has an inconsistent alpha channel.");
            byte Component(string text, bool alpha = false)
            {
                var percentage = text.EndsWith('%'); var scalar = double.Parse(percentage ? text[..^1] : text, CultureInfo.InvariantCulture);
                var maximum = percentage ? 100 : alpha ? 1 : 255;
                if (!double.IsFinite(scalar) || scalar < 0 || scalar > maximum) throw new InvalidDataException("The generated RGB colour exceeds its channel limits.");
                return (byte)Math.Round(scalar / maximum * 255);
            }
            return (Color.FromArgb(functional.Groups["alpha"].Success ? Component(functional.Groups["alpha"].Value, true) : (byte)255,
                Component(functional.Groups["red"].Value), Component(functional.Groups["green"].Value), Component(functional.Groups["blue"].Value)), weight);
        }
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
    [GeneratedRegex(@"^rgb(?<rgba>a)?\(\s*(?<red>(?:\d+(?:\.\d+)?|\.\d+)%?)\s*,\s*(?<green>(?:\d+(?:\.\d+)?|\.\d+)%?)\s*,\s*(?<blue>(?:\d+(?:\.\d+)?|\.\d+)%?)(?:\s*,\s*(?<alpha>(?:\d+(?:\.\d+)?|\.\d+)%?))?\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex RgbColour();
    [GeneratedRegex(@"([\d.]+)rem")] private static partial Regex Rem();
    [GeneratedRegex(@"^(.*?)\s+([\d.]+)%$")] private static partial Regex Weight();
}
