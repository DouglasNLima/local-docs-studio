using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Diagnostics;

namespace LensDocsStudio.Native.Validation;

internal static class SyntheticSaveRace
{
    public static int Run(string executable, string output)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = new NativeTestHighContrast();
        using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(AutomaticUpdateChecks: false)));
        var path = Path.Combine(output, "synthetic.md"); File.WriteAllText(path, "# Before Save\n");
        const string saved = "# Synthetic Architecture save\n\n```mermaid\narchitecture-beta\nservice api(server)[API]\n```\n";
        var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
        info.Environment["LENS_DIAGNOSTIC_MODE"] = "barrier";
        foreach (var argument in new[] { "--state-directory", state, output, path }) info.ArgumentList.Add(argument);
        using var app = Application.Launch(info);
        object? heldError = null;
        try
        {
            var main = NativeExpansionLaunch.Prepare(app, automation, Stopwatch.StartNew(), path, output, 1, true);
            var editor = main.FindFirstDescendant(cf => cf.ByAutomationId("MarkdownEditor")) ?? throw new InvalidOperationException("The synthetic editor was not found.");
            editor.Patterns.Value.Pattern.SetValue(saved); editor.Focus();
            CreationSaveProbe.BeforeSave(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => SaveTimeline.BarrierState == 1, "The post-disk preference barrier was not entered.");
            var diskMatches = File.ReadAllText(path) == saved;
            var readOnly = editor.Patterns.Value.Pattern.IsReadOnly.Value;
            var nativeJson = editor.Properties.ItemStatus.Value;
            using var native = JsonDocument.Parse(nativeJson);
            var nativeSaving = native.RootElement.GetProperty("Main").GetProperty("IsSaving").GetBoolean();
            var pendingSave = native.RootElement.GetProperty("Main").GetProperty("Pending").EnumerateArray().Any(item => item.GetString()!.EndsWith(":save", StringComparison.Ordinal));
            var held = native.RootElement.GetProperty("Barrier").GetInt32() == 1 && SaveTimeline.BarrierState == 1;
            var snapshot = CreationSaveProbe.Snapshot(editor, main, automation);
            SaveTimeline.Event("synthetic.held", new { DiskMatches = diskMatches, ReadOnly = readOnly, State = snapshot });
            if (!diskMatches || !readOnly || !nativeSaving || !pendingSave || !held) throw new InvalidOperationException("The synthetic intermediate state was not demonstrated.");
            try { CreationSaveProbe.SetValue(editor, "# Attempt while saving\n", main, automation, output); }
            catch (Exception error) { heldError = SaveTimeline.Error(error); }
            finally { SaveTimeline.ReleaseBarrier(); }
            Wait(() => !editor.Patterns.Value.Pattern.IsReadOnly.Value, "The real save did not restore the editor after releasing persistence.");
            CreationSaveProbe.SetValue(editor, "# Writable after Save\n", main, automation, output);
            var result = new { Classification = "RACE_DETERMINISTICALLY_DEMONSTRATED", OriginalAttribution = "NOT ESTABLISHED",
                DiskMatches = diskMatches, HeldReadOnly = readOnly, HeldSetValueException = heldError,
                HeldNativeIsSaving = nativeSaving, HeldPendingSave = pendingSave, HeldBarrier = held,
                HeldSetValueSucceeded = heldError is null, RestoredWritableSetValue = "PASS", HeldState = snapshot,
                SufficiencyLimit = "Inspect raw provider entry/exit state and barrier flag; an external pre-call snapshot alone cannot establish the provider state." };
            File.WriteAllText(Path.Combine(output, "synthetic-result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            display.Complete(); return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "synthetic-error.txt"), error.ToString()); Console.Error.WriteLine(error); return 1; }
        finally { SaveTimeline.ReleaseBarrier(); SaveTimeline.Flush(); if (!app.HasExited) app.Kill(); }
    }
    private static void Wait(Func<bool> condition, string message)
    {
        var timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException(message); Thread.Sleep(10); }
    }
}
