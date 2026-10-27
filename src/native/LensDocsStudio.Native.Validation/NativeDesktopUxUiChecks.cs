using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Reuses the existing UIA, clipboard and disposable-runner accessibility harness.
    // Program retains its hosted-runner guard; this never changes a development host's OS theme.
    public static int RunDesktopUxInteractions(string executable, string output, string root, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start();
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false, Theme: "Dark")));
        var er = File.ReadAllText(Path.Combine(root, "tests/fixtures/native-desktop-ux/inspection.mmd"));
        var source = "# Inspection\n\nContact **contact** and con**tact**. Another CONTACT.\n\n$x_{contact}$\n\n```mermaid\n" + er + "\n```\n";
        var path = Path.Combine(output, "fixture.md"); File.WriteAllText(path, source);
        var saved = File.ReadAllBytes(path); var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var arg in new[] { "--state-directory", state, path }) info.ArgumentList.Add(arg);
        using var app = Application.Launch(info); Window? main = null; Window? diagram = null;
        var previousClipboard = NativeUiClipboard.GetDataObject(); uint clipboardSequence = 0;
        try
        {
            main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The native UX window did not open.");
            FocusWindow(main); var editor = Find(main, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == source && Find(main, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "The exact source/preview did not load.");
            Check(!highContrast || System.Windows.SystemParameters.HighContrast, "Requested operating-system High Contrast is active");
            foreach (var theme in new[] { "Dark", "Light", "System", "Dark" })
            {
                Find(main, "ViewMenu").Patterns.ExpandCollapse.Pattern.Expand(); Find(main, (theme == "System" ? "Windows" : theme) + "Theme").AsMenuItem().Invoke();
                Wait(() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(Path.Combine(state, "preferences.json")))!.Theme == theme, "Theme preference did not change.");
                main.CaptureToFile(Path.Combine(output, "caption-" + theme.ToLowerInvariant() + ".png"));
            }
            Check(true, "Shown native caption and content captured for Light, Dark and System without recreating the window");
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            var query = Find(main, "FindText").AsTextBox(); query.Text = "contact";
            var count = LensDocsStudio.Core.Parsing.FindMatches.Collect(source, "contact").Offsets.Count;
            Counter("1 of " + count); Check(query.Properties.HasKeyboardFocus.Value, "Initial query result preserves keyboard focus");
            Keyboard.Type(VirtualKeyShort.RETURN); Counter("2 of " + count);
            Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.RETURN); Counter("1 of " + count);
            Find(main, "FindPrevious").AsButton().Invoke(); Counter(count + " of " + count);
            Keyboard.Type(VirtualKeyShort.F3); Counter("1 of " + count);
            main.CaptureToFile(Path.Combine(output, "find-editor.png"));
            query.Text = "missing-unique-query"; Counter("0 of 0"); query.Text = ""; Counter("0 of 0");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            var preview = Find(main, "DocumentPreview");
            var text = preview.Patterns.Text.Pattern.DocumentRange.FindText("Contact", false, true)!;
            var textBounds = text.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
            Mouse.Click(new System.Drawing.Point((int)(textBounds.Left + 3), (int)(textBounds.Top + 3)));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(main, "FindContext").Name == "Find in preview", "Preview context was not retained.");
            query = Find(main, "FindText").AsTextBox(); query.Text = "contact"; Counter("1 of 5");
            Keyboard.Type(VirtualKeyShort.RETURN); Counter("2 of 5"); Keyboard.Type(VirtualKeyShort.RETURN); Counter("3 of 5");
            Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).Equals("contact", StringComparison.OrdinalIgnoreCase)), "The formatted preview occurrence was counted but not selected.");
            main.CaptureToFile(Path.Combine(output, "find-preview-formatted.png"));
            Check(true, "Separate editor/preview counts, initial focus, forward/backward wrap, F3, empty/missing and cross-inline selection");
            Keyboard.Type(VirtualKeyShort.ESCAPE); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_H);
            Find(main, "FindText").AsTextBox().Text = "contact"; Counter("1 of " + count);
            Find(main, "ReplaceText").AsTextBox().Text = "Customer"; Find(main, "ReplaceOne").AsButton().Invoke();
            Wait(() => Find(main, "FindCounter").Name.EndsWith(" of " + (count - 1), StringComparison.Ordinal), "Replacement did not invalidate the match count.");
            Keyboard.Type(VirtualKeyShort.ESCAPE); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == source, "Replacement Undo did not restore exact source.");
            main.FindFirstDescendant(cf => cf.ByName("Zoom in").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))!.AsButton().Invoke();
            Wait(() => Find(main, "PreviewZoomLabel").Name != "Preview zoom 100 percent; restore 100 percent", "Preview zoom label did not update.");
            main.CaptureToFile(Path.Combine(output, "preview-zoom.png")); Find(main, "PreviewZoomLabel").AsButton().Invoke();
            Wait(() => Find(main, "PreviewZoomLabel").Name == "Preview zoom 100 percent; restore 100 percent", "Preview zoom reset did not synchronise.");
            Check(true, "Replacement invalidation, exact Undo and live/reset document zoom");

            Find(main, "DiagramActionZoom").AsButton().Invoke();
            Wait(() => (diagram = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId("DiagramWindow")))?.AsWindow()) is not null, "ER inspection window did not open.");
            FocusWindow(diagram!); Find(diagram!, "DiagramActualSize").AsButton().Invoke();
            var geometry = StaWorker.RunAsync(() => DiagramInspection.Create(new DiagramService().Render(er, true), new NativeSvgService())!, default).GetAwaiter().GetResult();
            var drawingWidth = geometry.Drawing.Width; var drawingHeight = geometry.Drawing.Height;
            var entity = geometry.Entities.Single(item => item.Description == "Contact").Shape.Bounds;
            var relationship = geometry.Connections.Single(item => item.Description.Contains("performs", StringComparison.Ordinal)).LabelBounds!.Value;
            Copy(); var canonical = ClipboardPixels();
            Move(relationship.Left + relationship.Width / 2, relationship.Top + relationship.Height / 2);
            Wait(() => Find(diagram!, "DiagramCopyStatus").Name.Contains("performs", StringComparison.Ordinal) && Find(diagram!, "DiagramCopyStatus").Name.Contains("Click to select", StringComparison.Ordinal), "Relationship hover did not expose verified identity.");
            diagram!.CaptureToFile(Path.Combine(output, "er-hover.png")); Mouse.Click();
            Wait(() => Find(diagram!, "DiagramCopyStatus").Name.StartsWith("Selected:", StringComparison.Ordinal), "Relationship click did not persist selection.");
            Move(entity.Left + entity.Width / 2, entity.Top + entity.Height / 2); Mouse.Click();
            Wait(() => Find(diagram!, "DiagramCopyStatus").Name.StartsWith("Selected: Contact", StringComparison.Ordinal), "Entity click did not select its direct connections.");
            diagram.CaptureToFile(Path.Combine(output, "er-selected.png")); Copy(); Check(ClipboardPixels() == canonical, "Actual Copy image pixels exclude temporary relationship/entity highlighting");
            Find(diagram, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(250);
            Wait(() => Find(diagram, "DiagramZoomLabel").Name == "250%", "Diagram zoom indicator is stale.");
            var viewport = Find(diagram, "DiagramViewport"); var rectangle = viewport.BoundingRectangle;
            var before = viewport.Patterns.Scroll.Pattern.VerticalScrollPercent.Value;
            Mouse.MoveTo(rectangle.Right - 35, rectangle.Bottom - 45); Mouse.Down(MouseButton.Left);
            try { Mouse.MoveTo(rectangle.Right - 35, rectangle.Top - 15); } finally { Mouse.Up(MouseButton.Left); }
            Wait(() => viewport.Patterns.Scroll.Pattern.VerticalScrollPercent.Value > before, "Direct left drag did not pan.");
            Check(Find(diagram, "DiagramCopyStatus").Name.StartsWith("Selected: Contact", StringComparison.Ordinal), "Pan beyond viewport and outside mouse release preserve entity selection");
            diagram.CaptureToFile(Path.Combine(output, "er-pan.png"));
            FocusWindow(diagram); Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait(() => Find(diagram, "DiagramCopyStatus").Name.StartsWith("Click an entity", StringComparison.Ordinal), "Escape did not clear inspection state.");
            Find(diagram, "DiagramReset").AsButton().Invoke(); Wait(() => Find(diagram, "DiagramZoomLabel").Name == "100%", "Reset zoom indicator is stale.");
            diagram.Close(); diagram = null;
            Check(ReadEditorText(editor) == source && File.ReadAllBytes(path).SequenceEqual(saved), "Inspection, pan, zoom and copy preserve source and saved bytes");
            File.WriteAllText(Path.Combine(output, "desktop-ux-ui.json"), JsonSerializer.Serialize(new { Passed = true, ActualHighContrast = System.Windows.SystemParameters.HighContrast, Checks = checks, CanonicalClipboardPixelSha256 = canonical }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;

            void Counter(string expected) => Wait(() => Find(main, "FindCounter").Name == expected, "Find counter did not become " + expected);
            void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
            void Move(double x, double y)
            {
                var image = Find(diagram!, "DiagramImage").BoundingRectangle;
                Mouse.MoveTo((int)(image.Left + x * image.Width / drawingWidth), (int)(image.Top + y * image.Height / drawingHeight));
            }
            void Copy() { Find(diagram!, "DiagramCopyImage").AsButton().Invoke(); Wait(() => Find(diagram!, "DiagramCopyStatus").Name.StartsWith("Diagram image copied", StringComparison.Ordinal), "Copy image failed."); clipboardSequence = GetClipboardSequenceNumber(); }
        }
        catch (Exception error)
        {
            main?.CaptureToFile(Path.Combine(output, "failure-main.png")); diagram?.CaptureToFile(Path.Combine(output, "failure-diagram.png"));
            File.WriteAllText(Path.Combine(output, "desktop-ux-ui.json"), JsonSerializer.Serialize(new { Passed = false, Checks = checks, Error = error.ToString() }));
            Console.Error.WriteLine(error); return 1;
        }
        finally { RestoreClipboard(previousClipboard, clipboardSequence); if (!app.HasExited) app.Kill(); }
    }).GetAwaiter().GetResult();

    private static string ClipboardPixels()
    {
        var bitmap = NativeUiClipboard.GetImage() ?? throw new IOException("No copied diagram pixels.");
        var stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8; var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0); return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
