using System.Windows;
using System.Windows.Media;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;

namespace LensDocsStudio.Native.Rendering;

public sealed record PreviewFormula(DrawingImage? Drawing, string? Error);

/// <summary>Prepares frozen native formula drawings on the existing serial STA worker.</summary>
public sealed class NativeMathService
{
    public const int MaximumFormulaCharacters = 4096;
    private readonly Dictionary<(string Source, bool Display, bool Dark, bool HighContrast, Color Colour), PreviewFormula> cache = [];
    private readonly Queue<(string, bool, bool, bool, Color)> order = new();

    public IReadOnlyDictionary<int, PreviewFormula> Prepare(ParsedDocument document, bool dark, CancellationToken cancellationToken = default, ContainerBlock? scope = null)
    {
        var result = new Dictionary<int, PreviewFormula>(); var characters = 0; var count = 0;
        foreach (var formula in DocumentMathematics.Read(scope ?? document.Syntax))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var error = !formula.Complete ? "Complete the closing formula delimiter to render this expression." : Validate(formula.Text);
            if (++count > 256 || (characters += Math.Min(MaximumFormulaCharacters, formula.Text.Length)) > 32768)
                error = "Additional formulae are shown as source because the document's maths preview limit was reached.";
            if (error is not null) { result[formula.SourceStart] = new(null, error); continue; }
            var highContrast = SystemParameters.HighContrast;
            var colour = highContrast ? SystemColors.WindowTextColor : dark ? Color.FromRgb(232, 234, 237) : Color.FromRgb(32, 33, 36);
            var key = (formula.Text, formula.Display, dark, highContrast, colour);
            if (!cache.TryGetValue(key, out var prepared))
            {
                prepared = Draw(formula.Text, formula.Display, colour, highContrast);
                while (cache.Count >= 128 && order.TryDequeue(out var oldest)) cache.Remove(oldest);
                cache.Add(key, prepared); order.Enqueue(key);
            }
            result[formula.SourceStart] = prepared;
        }
        cancellationToken.ThrowIfCancellationRequested(); return result;
    }

    public static string? Validate(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaximumFormulaCharacters)
            return "Formulae are rendered up to 4,096 characters; the original source is retained.";
        var depth = 0; var commands = 0; var scripts = 0; var cells = 0;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (char.IsControl(character) && character is not ('\r' or '\n' or '\t')) return "The formula contains an unsupported control character.";
            if (character == '\\')
            {
                if (++commands > 128) return "The formula exceeds the command limit; its source is retained.";
                if (index + 1 < source.Length && !char.IsLetter(source[index + 1])) index++;
            }
            else if (character == '{' && ++depth > 24) return "The formula exceeds the nesting limit; its source is retained.";
            else if (character == '}') depth--;
            else if (character is '^' or '_' && ++scripts > 128) return "The formula exceeds the script limit; its source is retained.";
            else if (character == '&' && ++cells > 64) return "The formula exceeds the matrix cell limit; its source is retained.";
            if (depth < 0) return "The formula has an unmatched closing brace.";
        }
        return depth == 0 ? null : "The formula has an unclosed brace.";
    }

    private static PreviewFormula Draw(string source, bool display, Color colour, bool highContrast)
    {
        try
        {
            var size = display ? 20d : 16d;
            var nativeSource = MathFormulaSource.Prepare(source);
            if (highContrast && (nativeSource.Contains(@"\color", StringComparison.Ordinal) || nativeSource.Contains(@"\textcolor", StringComparison.Ordinal)))
                return new(null, "Explicitly coloured formulae are shown as readable source in Windows high contrast.");
            var formula = WpfTeXFormulaParser.Instance.Parse(nativeSource);
            var environment = WpfTeXEnvironment.Create(display ? TexStyle.Display : TexStyle.Text, size, "Segoe UI", new SolidColorBrush(colour));
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) formula.RenderTo(context, environment, size);
            var bounds = visual.ContentBounds;
            if (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0
                || bounds.Width > 4096 || bounds.Height > 2048 || bounds.Width * bounds.Height > 2_000_000)
                return new(null, "The rendered formula exceeds the drawing size limit; its source is retained.");
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, bounds.Width + 4, bounds.Height + 4));
                context.PushTransform(new TranslateTransform(2 - bounds.Left, 2 - bounds.Top));
                context.DrawDrawing(visual.Drawing); context.Pop();
            }
            drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return new(image, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        { return new(null, "This formula cannot be rendered by the native maths engine; its original source is retained."); }
    }
}
