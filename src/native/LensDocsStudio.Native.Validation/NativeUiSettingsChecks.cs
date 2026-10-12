using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static Window OpenSettingsWindow(Window owner, UIA3Automation automation, int process)
    {
        FocusWindow(owner); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA);
        Window? settings = null;
        Wait(() => (settings = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings")))?.AsWindow()) is not null,
            "Ctrl+, did not open native settings.");
        return settings!;
    }

    private static void CheckSettings(Window window, AutomationElement editor, UIA3Automation automation, int process, string state, string output, string source)
    {
        var originalText = ReadEditorText(editor); var savedText = File.ReadAllText(source);
        var preferencePath = Path.Combine(state, "preferences.json");
        var before = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(preferencePath))!;
        var settings = OpenSettingsWindow(window, automation, process);
        Select("SettingsFont", "Consolas"); Select("SettingsFontSize", "18"); Select("SettingsIndentation", "2");
        Find(settings, "SettingsWordWrap").AsCheckBox().IsChecked = false;
        Find(settings, "SettingsLineNumbers").AsCheckBox().IsChecked = false;
        Find(settings, "SettingsSpaces").AsCheckBox().IsChecked = true;
        Find(settings, "SettingsOutline").AsCheckBox().IsChecked = false;
        Find(settings, "SettingsZoom").Patterns.RangeValue.Pattern.SetValue(125);
        settings.CaptureToFile(Path.Combine(output, "native-settings.png"));
        Find(settings, "SettingsApply").AsButton().Invoke();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null,
            "Applied settings did not close.");
        var after = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(preferencePath))!;
        if (after.Editor != new EditorPreferences("Consolas", 18, false, false, 2, true) || after.Preview != new PreviewPreferences(125) || after.ShowOutline
            || after.AutomaticUpdateChecks != before.AutomaticUpdateChecks || after.UpdateChannel != before.UpdateChannel
            || !after.RecentDocuments!.SequenceEqual(before.RecentDocuments!) || after.IgnoredVersion != before.IgnoredVersion
            || editor.Patterns.Value.Pattern.Value.Value != originalText || File.ReadAllText(source) != savedText)
            throw new IOException("Applying reading settings lost source, recent paths or update consent, or failed to persist the chosen values.");
        settings = OpenSettingsWindow(window, automation, process);
        Find(settings, "SettingsReset").AsButton().Invoke();
        Wait(() => Find(settings, "SettingsFontSize").AsComboBox().SelectedItem?.Text == "14", "Restore defaults did not reset the editable settings.");
        Find(settings, "SettingsCancel").AsButton().Invoke();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null,
            "Cancelled settings did not close.");
        var cancelled = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(preferencePath))!;
        if (WorkspaceSettings.From(cancelled) != WorkspaceSettings.From(after) || editor.Patterns.Value.Pattern.Value.Value != originalText)
            throw new IOException("Cancelling default settings changed saved preferences or document text.");

        void Select(string id, string value) { var choice = Find(settings, id).AsComboBox(); choice.Select(value); choice.Collapse(); }
    }

    private static void CheckRestoredSettings(Window window, UIA3Automation automation, int process)
    {
        var settings = OpenSettingsWindow(window, automation, process);
        if (Find(settings, "SettingsFont").AsComboBox().SelectedItem?.Text != "Consolas"
            || Find(settings, "SettingsFontSize").AsComboBox().SelectedItem?.Text != "18"
            || Find(settings, "SettingsIndentation").AsComboBox().SelectedItem?.Text != "2"
            || Find(settings, "SettingsWordWrap").AsCheckBox().IsChecked != false || Find(settings, "SettingsLineNumbers").AsCheckBox().IsChecked != false
            || Find(settings, "SettingsSpaces").AsCheckBox().IsChecked != true || Find(settings, "SettingsOutline").AsCheckBox().IsChecked != false
            || Find(settings, "SettingsZoom").Patterns.RangeValue.Pattern.Value != 125)
            throw new IOException("Native settings did not restore their exact values after restarting the application.");
        Find(settings, "SettingsCancel").AsButton().Invoke();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null,
            "Restored settings did not close.");
    }
}
