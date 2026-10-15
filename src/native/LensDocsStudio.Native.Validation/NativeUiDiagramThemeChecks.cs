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
        var before = Read(); var text = ReadEditorText(editor); var saved = ReadObservedText(source);
        var imageId = "DiagramPreview" + text.IndexOf("```mermaid", StringComparison.Ordinal);
        var originalHelp = Find(window, imageId).Properties.HelpText.Value;
        var checks = new List<object>();
        var paint = new List<object>();
        Appearance("Light");
        foreach (var choice in new[] { "Default", "Neutral", "Forest", "Lens", "Dark", "Auto" })
        {
            Select(choice, choice == "Auto" ? "Lens" : choice);
            if (choice == "Forest") CaptureForest("Light");
        }
        Appearance("Dark"); Select("Forest", "Forest");
        CaptureForest("Dark");
        Select("Auto", "Dark");
        Appearance(before.Theme);
        Select(before.Preview?.DiagramTheme ?? "Auto", originalHelp["Diagram theme: ".Length..]);
        var after = Read();
        if (ReadEditorText(editor) != text || ReadObservedText(source) != saved || after.Theme != before.Theme
            || WorkspaceSettings.From(after).Preview != WorkspaceSettings.From(before).Preview
            || after.AutomaticUpdateChecks != before.AutomaticUpdateChecks || after.UpdateChannel != before.UpdateChannel)
            throw new IOException("Diagram theme selection changed source, unrelated preferences or update consent.");
        File.WriteAllText(Path.Combine(output, "native-ui-diagram-themes.json"), JsonSerializer.Serialize(new
        { Passed = true, Checks = checks, Paint = paint, SourcePreserved = true, OriginalPreferencesRestored = true }, new JsonSerializerOptions { WriteIndented = true }));

        void CaptureForest(string applicationTheme)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); var attempts = 0;
            Wait(() =>
            {
                var diagram = Find(window, imageId); var bounds = diagram.BoundingRectangle; var windowBounds = window.BoundingRectangle;
                var region = new System.Drawing.Rectangle(bounds.Left - windowBounds.Left, bounds.Top - windowBounds.Top, bounds.Width, bounds.Height);
                using var screenshot = window.Capture(); attempts++;
                var observation = NativeDiagramPaintChecks.ObserveForest(screenshot, region);
                if (!observation.Passed) return false;
                // Keep the exact accepted frame; a second capture could observe a later update.
                screenshot.Save(Path.Combine(output, "native-diagram-forest-" + applicationTheme.ToLowerInvariant() + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                paint.Add(new { ApplicationTheme = applicationTheme, Attempts = attempts, Milliseconds = watch.Elapsed.TotalMilliseconds, Observation = observation });
                return true;
            }, "The Forest diagram did not paint both labelled nodes within the desktop observation deadline.");
        }

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
