using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.State;

public sealed record EditorPreferences(string FontFamily = "Cascadia Code, Consolas", int FontSize = 14,
    bool WordWrap = true, bool ShowLineNumbers = true, int IndentationSize = 4, bool ConvertTabsToSpaces = false);
public sealed record PreviewPreferences(int Zoom = 100, string ReadingMode = "Automatic", string DiagramTheme = "Auto");

/// <summary>Editable appearance settings, separate from recovery, recent paths and update consent.</summary>
public sealed record WorkspaceSettings(string Theme, string Layout, EditorPreferences Editor, PreviewPreferences Preview, bool ShowOutline)
{
    public static IReadOnlyList<string> Themes { get; } = Array.AsReadOnly(new[] { "System", "Light", "Dark" });
    public static IReadOnlyList<string> Layouts { get; } = Array.AsReadOnly(new[] { "Editor", "Split", "Preview" });
    public static IReadOnlyList<string> Fonts { get; } = Array.AsReadOnly(new[] { "Cascadia Code, Consolas", "Consolas", "Courier New" });
    public static IReadOnlyList<string> ReadingModes { get; } = Array.AsReadOnly(new[] { "Automatic", "Continuous", "Pages" });
    public static WorkspaceSettings Default { get; } = new("System", "Split", new(), new(), true);

    public static WorkspaceSettings From(UserPreferences preferences) => new(preferences.Theme, preferences.Layout,
        preferences.Editor ?? new(), preferences.Preview ?? new(), preferences.ShowOutline);

    public bool IsValid => Themes.Contains(Theme) && Layouts.Contains(Layout) && Editor is not null && Preview is not null
        && Fonts.Contains(Editor.FontFamily) && Editor.FontSize is >= 10 and <= 36
        && Editor.IndentationSize is >= 2 and <= 8 && Preview.Zoom is >= 50 and <= 200 && ReadingModes.Contains(Preview.ReadingMode)
        && DiagramThemes.Choices.Contains(Preview.DiagramTheme);

    public static bool ArePreferencesValid(UserPreferences preferences) => From(preferences).IsValid
        && preferences.UpdateChannel is "Stable" or "Preview";

    public UserPreferences ApplyTo(UserPreferences current)
    {
        if (!IsValid) throw new ArgumentException("Choose supported appearance settings, a font size from 10 to 36, indentation from 2 to 8 and preview zoom from 50 to 200%.");
        // Merge into the latest state so an update check or recent-path change cannot be lost.
        return current with { Theme = Theme, Layout = Layout, Editor = Editor, Preview = Preview, ShowOutline = ShowOutline };
    }
}
