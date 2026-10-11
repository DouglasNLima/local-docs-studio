using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static class NativeUiChecks
{
    public static int Run(string executable, string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var runRoot = Path.Combine(output, "ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        var source = Path.Combine(runRoot, "fixture.md");
        File.WriteAllText(source, "# Native UI fixture\n\nSearchable text.\n\n```mermaid\nflowchart LR\nA[Write] --> B[Review]\n```\n");
        var state = Path.Combine(runRoot, "state");
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var checks = new List<string>();
        using var automation = new UIA3Automation();
        Application? app = null;
        Process? launchedProcess = null;
        try
        {
            app = Launch(source);
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("Native window did not appear.");
            var editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Searchable", StringComparison.Ordinal), "Fixture text did not load.");
            Wait(() => Find(window, "Status").Name.StartsWith("Preview updated", StringComparison.Ordinal), "The initial native preview did not finish.");
            checks.Add("Startup file opened through native controls");
            editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            var find = Find(window, "FindText").AsTextBox();
            Wait(() => Find(window, "FindContext").Name == "Find in editor", "Ctrl+F did not choose editor context.");
            find.Text = "Searchable";
            Keyboard.Type(VirtualKeyShort.RETURN);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.Type("\nNative UI validation — Café");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Typing was captured by an inactive search control.");
            checks.Add("Editor search closes and keyboard input reaches the editor");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => File.ReadAllText(source).Contains("Native UI validation — Café", StringComparison.Ordinal), "Save did not persist Unicode text.");
            checks.Add("Ctrl+S persisted actual UTF-8 source");
            var preview = Find(window, "DocumentPreview");
            preview.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(window, "FindContext").Name == "Find in preview", "Ctrl+F did not choose preview context.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            checks.Add("Ctrl+F targets the focused preview");
            Find(window, "DiagramActionZoom").Click();
            Window? diagramWindow = null;
            Wait(() => (diagramWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Mermaid diagram")))?.AsWindow()) is not null, "The native diagram zoom window did not open.");
            Find(diagramWindow!, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(175);
            Wait(() => Find(diagramWindow!, "DiagramZoomLabel").Name == "175%", "The native zoom did not change scale.");
            diagramWindow!.CaptureToFile(Path.Combine(output, "native-diagram.png"));
            diagramWindow.Close();
            checks.Add("Mermaid opens in a native drawing window and zoom changes its scale");
            window.Close();
            Wait(() => app.HasExited, "Native application did not close safely.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("Native application failed during shutdown: " + launchedProcess.ExitCode);
            app.Dispose(); launchedProcess.Dispose(); app = Launch(null);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("Restored window did not appear.");
            editor = Find(window, "MarkdownEditor");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value.Contains("Native UI validation — Café", StringComparison.Ordinal), "Session did not restore saved text.");
            checks.Add("Session restoration reopened the document");
            Find(window, "HelpMenu").Click();
            Find(window, "UpdatesMenu").Click();
            Window? updateWindow = null;
            Wait(() => (updateWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Lens Docs Studio updates")))?.AsWindow()) is not null, "The native updates window did not open.");
            var channel = Find(updateWindow!, "UpdateChannel").AsComboBox();
            channel.Select("Preview");
            channel.Collapse();
            Wait(() => File.ReadAllText(Path.Combine(state, "preferences.json")).Contains("\"Preview\"", StringComparison.Ordinal), "Update channel preference was not persisted.");
            if (Find(updateWindow!, "DownloadUpdate").IsEnabled || Find(updateWindow!, "InstallUpdate").IsEnabled) throw new InvalidOperationException("Untrusted update actions were enabled.");
            checks.Add("Native update preferences persist and untrusted installation actions stay disabled");
            updateWindow!.CaptureToFile(Path.Combine(output, "native-updates.png"));
            updateWindow!.Close();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Lens Docs Studio updates"))) is null, "The updates window did not close.");
            window.CaptureToFile(Path.Combine(output, "native-workspace.png"));
            window.Close();
            Wait(() => app.HasExited, "Restored application did not close.");
            if (launchedProcess!.ExitCode != 0) throw new InvalidOperationException("Restored application failed during shutdown: " + launchedProcess.ExitCode);
            File.WriteAllText(Path.Combine(output, "native-ui.json"), JsonSerializer.Serialize(new { Passed = true, Checks = checks, FixtureRoot = runRoot }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Native UI Automation: " + checks.Count + " checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            try { app?.GetMainWindow(automation, TimeSpan.FromSeconds(1))?.CaptureToFile(Path.Combine(output, "native-ui-failure.png")); } catch { }
            File.WriteAllText(Path.Combine(output, "native-ui.json"), JsonSerializer.Serialize(new { Passed = false, Checks = checks, Error = ex.ToString(), FixtureRoot = runRoot }, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally { if (app is not null) { if (!app.HasExited) app.Kill(); app.Dispose(); } launchedProcess?.Dispose(); }

        Application Launch(string? path)
        {
            var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state);
            if (path is not null) info.ArgumentList.Add(path);
            launchedProcess = Process.Start(info) ?? throw new IOException("Could not launch the native validation fixture.");
            launchedProcess.EnableRaisingEvents = true;
            // FlaUI refreshes and disposes its Process instance while locating a window.
            // Keep our separately owned started process for the real shutdown exit code.
            return Application.Attach(launchedProcess.Id);
        }
    }

    private static AutomationElement Find(Window window, string id)
    {
        AutomationElement? element = null;
        Wait(() => (element = window.FindFirstDescendant(cf => cf.ByAutomationId(id))) is not null, "Missing native control: " + id);
        return element!;
    }
    private static void Wait(Func<bool> condition, string failure)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(20))
        {
            try { if (condition()) return; } catch (Exception ex) when (ex is IOException or FlaUI.Core.Exceptions.ElementNotAvailableException) { }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException(failure);
    }
}
