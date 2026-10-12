using System.Text.Json;
using FlaUI.Core.AutomationElements;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckDiagramThemes(Window window, AutomationElement editor, string state, string output, string source)
    {
        var path = Path.Combine(state, "preferences.json");
        UserPreferences Read()
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<UserPreferences>(input)!;
        }
        var before = Read(); var text = ReadEditorText(editor); var saved = File.ReadAllText(source);
        var imageId = "DiagramPreview" + text.IndexOf("```mermaid", StringComparison.Ordinal);
        var originalHelp = Find(window, imageId).Properties.HelpText.Value;
        var checks = new List<object>();
        Appearance("Light");
        foreach (var choice in new[] { "Default", "Neutral", "Forest", "Lens", "Dark", "Auto" })
        {
            Select(choice, choice == "Auto" ? "Lens" : choice);
            if (choice == "Forest") window.CaptureToFile(Path.Combine(output, "native-diagram-forest-light.png"));
        }
        Appearance("Dark"); Select("Forest", "Forest");
        window.CaptureToFile(Path.Combine(output, "native-diagram-forest-dark.png"));
        Select("Auto", "Dark");
        Appearance(before.Theme);
        Select(before.Preview?.DiagramTheme ?? "Auto", originalHelp["Diagram theme: ".Length..]);
        var after = Read();
        if (ReadEditorText(editor) != text || File.ReadAllText(source) != saved || after.Theme != before.Theme
            || WorkspaceSettings.From(after).Preview != WorkspaceSettings.From(before).Preview
            || after.AutomaticUpdateChecks != before.AutomaticUpdateChecks || after.UpdateChannel != before.UpdateChannel)
            throw new IOException("Diagram theme selection changed source, unrelated preferences or update consent.");
        File.WriteAllText(Path.Combine(output, "native-ui-diagram-themes.json"), JsonSerializer.Serialize(new
        { Passed = true, Checks = checks, SourcePreserved = true, OriginalPreferencesRestored = true }, new JsonSerializerOptions { WriteIndented = true }));

        void Appearance(string theme)
        {
            FocusWindow(window); Find(window, "ViewMenu").Click();
            Find(window, theme == "System" ? "WindowsTheme" : theme + "Theme").Patterns.Invoke.Pattern.Invoke();
            Wait(() => Read().Theme == theme, "The application appearance did not persist.");
        }
        void Select(string choice, string resolved)
        {
            FocusWindow(window); Find(window, "ViewMenu").Click();
            Find(window, "DiagramThemeMenu").Patterns.ExpandCollapse.Pattern.Expand();
            Find(window, "DiagramTheme" + choice).Patterns.Invoke.Pattern.Invoke();
            Wait(() => Read().Preview?.DiagramTheme == choice, "The diagram choice did not persist.");
            Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(imageId))?.Properties.HelpText.Value == "Diagram theme: " + resolved,
                "The visible native diagram did not finish rendering the selected palette.");
            checks.Add(new { Choice = choice, ApplicationTheme = Read().Theme, RenderedTheme = resolved });
        }
    }
}
