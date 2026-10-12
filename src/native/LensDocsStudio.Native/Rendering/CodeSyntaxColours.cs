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
        => AddAsync(target, text, syntax, dark, highContrast).GetAwaiter().GetResult();

    internal static async ValueTask AddAsync(InlineCollection target, string text, CodeSyntaxBlock? syntax, bool dark, bool highContrast, Func<ValueTask>? cooperate = null)
    {
        var palette = dark ? Dark : Light;
        foreach (var token in CodeSyntaxRuns.Read(text, highContrast ? null : syntax))
        {
            if (cooperate is not null) await cooperate();
            var run = new Run(token.Text);
            if (token.Role != CodeTokenRole.Plain) run.Foreground = palette[(int)token.Role];
            target.Add(run);
        }
    }

    private static Brush[] Palette(bool dark) => Enum.GetValues<CodeTokenRole>().Select(role =>
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + CodeSyntaxPalette.Hex(role, dark))); brush.Freeze(); return (Brush)brush;
    }).ToArray();
}
