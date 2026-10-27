using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckMinimumWindow(Window window, AutomationElement editor, UIA3Automation automation, int process, string output, string source)
    {
        var originalBounds = window.BoundingRectangle;
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        try
        {
            window.Patterns.Transform.Pattern.Resize(860, 540);
            Wait(() => window.BoundingRectangle.Width < originalBounds.Width && window.BoundingRectangle.Height < originalBounds.Height,
                "The native window did not accept its smaller size.");
            var commands = window.FindFirstDescendant(cf => cf.ByName("Application commands"))
                ?? throw new IOException("The application toolbar is unavailable at minimum size.");
            var more = commands.FindFirstDescendant(cf => cf.ByName("More commands"));
            AutomationElement? settingsCommand = null;
            var settingsInOverflow = more is { IsOffscreen: false };
            if (!settingsInOverflow)
            {
                // Compact branding lets all application commands fit at this width. Exercise
                // genuine formatting overflow, while requiring Settings to remain directly visible.
                settingsCommand = commands.FindFirstDescendant(cf => cf.ByAutomationId("ToolbarSettings"));
                if (settingsCommand is null || settingsCommand.IsOffscreen || !window.BoundingRectangle.Contains(settingsCommand.BoundingRectangle))
                    throw new IOException("The fitting application toolbar lost its directly visible Settings command.");
                more = Find(window, "FormattingToolbar").FindFirstDescendant(cf => cf.ByName("More commands"));
            }
            if (more is null) throw new IOException("The actual native overflow command is unavailable.");
            if (more.IsOffscreen || !window.BoundingRectangle.Contains(more.BoundingRectangle))
                throw new IOException("The actual overflow command is clipped at minimum size.");
            more.Click();
            window.CaptureToFile(Path.Combine(output, "native-minimum-overflow.png"));
            if (settingsInOverflow)
                Wait(() => (settingsCommand = automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(process).And(cf.ByAutomationId("ToolbarSettings")))
                    .FirstOrDefault(command => !command.IsOffscreen)) is not null, "Overflow did not expose its actual Settings command.");
            else
            {
                AutomationElement? rule = null;
                Wait(() => (rule = automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(process).And(cf.ByAutomationId("Formatrule")))
                    .FirstOrDefault(command => !command.IsOffscreen)) is not null, "Formatting overflow lost its actual horizontal-rule command.");
                rule!.AsButton().Invoke();
                Wait(() => ReadEditorText(editor) != original && ReadEditorText(editor).Contains("---", StringComparison.Ordinal), "The actual overflowing horizontal-rule command did not edit Markdown.");
                editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "The overflowing formatting action did not retain one exact Undo.");
            }
            settingsCommand!.AsButton().Invoke();
            Window? settings = null;
            Wait(() => (settings = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings")))?.AsWindow()) is not null,
                "The actual overflow Settings command did not execute.");
            Find(settings!, "SettingsCancel").AsButton().Invoke();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null,
                "The overflow Settings dialogue did not dismiss safely.");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_H);
            Wait(() => Find(window, "FindContext").Name == "Find in editor", "Replace did not open at the minimum window size.");
            Find(window, "FindText").AsTextBox().Text = "Searchable";
            Find(window, "ReplaceText").AsTextBox().Text = "Reviewed";
            var bounds = window.BoundingRectangle;
            if (window.FindFirstDescendant(cf => cf.ByAutomationId("CancelOperation"))?.IsOffscreen == false)
                throw new IOException("The idle status bar retained an inactive Cancel operation button.");
            var controls = new[] { "FindPrevious", "FindNext", "ReplaceOne", "CloseFind", "PreviewZoomLabel" }
                .Select(id => new { Id = id, Element = Find(window, id) })
                .Select(item => new { item.Id, item.Element.Name, Bounds = item.Element.BoundingRectangle, item.Element.IsOffscreen }).ToArray();
            foreach (var control in controls)
                if (control.IsOffscreen || control.Bounds.Width < 25 || control.Bounds.Height < 20 || !bounds.Contains(control.Bounds))
                    throw new IOException("The native " + control.Id + " control is clipped at the minimum window size.");
            Find(window, "FindNext").Click(); Find(window, "ReplaceOne").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == original.Replace("Searchable", "Reviewed", StringComparison.Ordinal),
                "The visible Replace control did not change the selected source.");
            window.CaptureToFile(Path.Combine(output, "native-minimum-window.png"));
            Find(window, "CloseFind").Click();
            Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("CloseFind"))?.IsOffscreen != false,
                "The visible Close control did not dismiss Find/Replace.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == original, "Minimum-window replacement did not retain Undo.");
            if (!File.ReadAllBytes(source).AsSpan().SequenceEqual(saved)) throw new IOException("Minimum-window Find/Replace changed the saved file.");
            File.WriteAllText(Path.Combine(output, "native-minimum-window.json"), JsonSerializer.Serialize(new {
                Passed = true, RequestedWidth = 860, RequestedHeight = 540, ActualBounds = bounds, Controls = controls,
                ActualOverflowCommandExecuted = true, SettingsInOverflow = settingsInOverflow, SettingsExecuted = true,
                SourceAndUndoPreserved = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.Patterns.Transform.Pattern.Resize(originalBounds.Width, originalBounds.Height); }
    }
}
