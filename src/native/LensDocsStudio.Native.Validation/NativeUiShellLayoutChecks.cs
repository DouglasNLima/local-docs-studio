using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckMinimumWindow(Window window, AutomationElement editor, string output, string source)
    {
        var originalBounds = window.BoundingRectangle;
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        try
        {
            window.Patterns.Transform.Pattern.Resize(860, 540);
            Wait(() => window.BoundingRectangle.Width < originalBounds.Width && window.BoundingRectangle.Height < originalBounds.Height,
                "The native window did not accept its smaller size.");
            FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_H);
            Wait(() => Find(window, "FindContext").Name == "Find in editor", "Replace did not open at the minimum window size.");
            Find(window, "FindText").AsTextBox().Text = "Searchable";
            Find(window, "ReplaceText").AsTextBox().Text = "Reviewed";
            var bounds = window.BoundingRectangle;
            if (window.FindFirstDescendant(cf => cf.ByAutomationId("CancelOperation"))?.IsOffscreen == false)
                throw new IOException("The idle status bar retained an inactive Cancel operation button.");
            var controls = new[] { "FindNext", "ReplaceOne", "CloseFind" }
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
                SourceAndUndoPreserved = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.Patterns.Transform.Pattern.Resize(originalBounds.Width, originalBounds.Height); }
    }
}
