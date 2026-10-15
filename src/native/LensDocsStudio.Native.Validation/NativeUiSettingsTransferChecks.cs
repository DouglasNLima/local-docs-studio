using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckSettingsTransfer(Window owner, AutomationElement editor, UIA3Automation automation, int process,
        string state, string output, string source, Action<string, string> open, Action<string, string> save)
    {
        var sourceText = ReadEditorText(editor); var savedSource = ReadObservedText(source);
        var preferencesPath = Path.Combine(state, "preferences.json");
        UserPreferences Read() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(preferencesPath))!;
        var original = Read(); var originalSettings = WorkspaceSettings.From(original);
        var backup = Path.Combine(output, "settings-" + Guid.NewGuid().ToString("N") + ".json");
        var browser = Path.Combine(output, "browser-settings-" + Guid.NewGuid().ToString("N") + ".json");
        var selectedTheme = original.Theme == "Dark" ? "Light" : "Dark";
        File.WriteAllText(browser, JsonSerializer.Serialize(new { formatVersion = SettingsTransferFormat.LegacyFormatVersion, theme = selectedTheme, layout = "Editor", showOutline = true }));
        var expected = originalSettings with { Theme = selectedTheme, Layout = "Editor", ShowOutline = true };
        var settings = OpenSettingsWindow(owner, automation, process);
        Find(settings, "SettingsExport").Patterns.Invoke.Pattern.Invoke(); save(backup, "Export appearance settings");
        Wait(() => File.Exists(backup) && Find(settings, "SettingsStatus").Name.StartsWith("Selected appearance choices exported", StringComparison.Ordinal),
            "Settings export did not create the selected portable appearance file.");
        if (SettingsTransferFormat.Read(File.ReadAllBytes(backup), WorkspaceSettings.Default) != originalSettings)
            throw new IOException("Native settings export lost the actual selected choices.");
        Import(browser);
        if (WorkspaceSettings.From(Read()) != originalSettings || Find(settings, "SettingsTheme").AsComboBox().SelectedItem?.Text != selectedTheme
            || Find(settings, "SettingsLayout").AsComboBox().SelectedItem?.Text != "Editor"
            || Find(settings, "SettingsFontSize").AsComboBox().SelectedItem?.Text != "18")
            throw new IOException("Imported settings saved before Apply, lost absent editor choices or failed to populate the native controls.");
        settings.CaptureToFile(Path.Combine(output, "native-settings-import-review.png"));
        Close("SettingsCancel");
        if (WorkspaceSettings.From(Read()) != originalSettings) throw new IOException("Cancelling an import changed saved appearance choices.");
        settings = OpenSettingsWindow(owner, automation, process); Import(browser); Close("SettingsApply");
        var applied = Read();
        if (WorkspaceSettings.From(applied) != expected || applied.AutomaticUpdateChecks != original.AutomaticUpdateChecks
            || applied.UpdateChannel != original.UpdateChannel || applied.IgnoredVersion != original.IgnoredVersion
            || !applied.RecentDocuments!.SequenceEqual(original.RecentDocuments!))
            throw new IOException("Applying imported appearance choices lost unrelated preferences or did not persist the reviewed settings.");
        settings = OpenSettingsWindow(owner, automation, process); Import(backup); Close("SettingsApply");
        if (WorkspaceSettings.From(Read()) != originalSettings || ReadEditorText(editor) != sourceText || ReadObservedText(source) != savedSource)
            throw new IOException("Restoring exported settings did not recover the original appearance or changed source text.");
        void Import(string path)
        {
            Find(settings, "SettingsImport").Patterns.Invoke.Pattern.Invoke(); open(path, "Import appearance settings");
            Wait(() => Find(settings, "SettingsStatus").Name.StartsWith("Imported appearance choices", StringComparison.Ordinal) && Find(settings, "SettingsApply").IsEnabled,
                "Settings import did not finish with reviewable, unapplied choices.");
        }
        void Close(string command)
        {
            Find(settings, command).Patterns.Invoke.Pattern.Invoke();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null,
                "The settings transfer dialogue did not close.");
        }
    }
}
