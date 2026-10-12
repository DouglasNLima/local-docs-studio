using System.Windows.Documents;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Rendering;

public static class CodeSyntaxColours
{
    private static readonly Brush[] Light = Palette(false);
    private static readonly Brush[] Dark = Palette(true);

    public static Brush For(CodeTokenRole role, bool dark) => (dark ? Dark : Light)[(int)role];

    public static void Add(InlineCollection target, string text, CodeSyntaxBlock? syntax, bool dark, bool highContrast)
    {
        if (syntax is null || syntax.Text != text || highContrast) { target.Add(new Run(text)); return; }
        var palette = dark ? Dark : Light;
        var position = 0;
        foreach (var span in syntax.Spans)
        {
            if (span.Start > position) target.Add(new Run(text[position..span.Start]));
            target.Add(new Run(text.Substring(span.Start, span.Length)) { Foreground = palette[(int)span.Role] });
            position = span.Start + span.Length;
        }
        if (position < text.Length) target.Add(new Run(text[position..]));
    }

    private static Brush[] Palette(bool dark) => Enum.GetValues<CodeTokenRole>().Select(role =>
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + CodeSyntaxPalette.Hex(role, dark))); brush.Freeze(); return (Brush)brush;
    }).ToArray();
}
