using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Exports;

internal static class CodeSyntaxHtml
{
    private static readonly CodeTokenRole[] Roles = Enum.GetValues<CodeTokenRole>().Where(role => role != CodeTokenRole.Plain).ToArray();
    private static readonly HashSet<string> SafeStyles = Roles.Select(Style).ToHashSet(StringComparer.Ordinal);

    public static string Class(CodeTokenRole role) => "code-syntax-" + role.ToString().ToLowerInvariant();
    public static string Style(CodeTokenRole role) => "color: #" + CodeSyntaxPalette.Hex(role) + ";";
    public static bool IsSafeStyle(string value) => SafeStyles.Contains(value);

    // Clipboard fragments carry light inline colours. Exported sites override only these
    // fixed token classes for dark mode, print and Windows high-contrast preferences.
    public static string ThemeCss => string.Concat(Roles.Select(role =>
        ":root[data-theme=dark] ." + Class(role) + "{color:#" + CodeSyntaxPalette.Hex(role, true) + "!important}"))
        + "\n@media print{:root[data-theme] pre,:root[data-theme] pre code{background:#F3F4F6;color:#202124}"
        + string.Concat(Roles.Select(role => ":root[data-theme] ." + Class(role) + "{color:#" + CodeSyntaxPalette.Hex(role) + "!important}")) + "}"
        + "\n@media(forced-colors:active){:root[data-theme] pre code [class^=code-syntax-]{color:CanvasText!important}}\n";
}
