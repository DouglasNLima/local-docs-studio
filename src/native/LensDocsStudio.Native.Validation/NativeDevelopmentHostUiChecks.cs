using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Validation;

// Deliberately separate from hosted suites: no clipboard, display, taskbar, theme, registry,
// installer, trust, account or default-application mutation. Only synthetic files and test HWNDs.
internal static partial class NativeUiChecks
{
    public static int RunDevelopmentHost(string sandbox, string revision, string tree, string executableSha256, bool shellOnly = false) => Task.Run(() =>
    {
        sandbox = Path.GetFullPath(sandbox);
        WorkspacePath.RejectReparsePoints(sandbox);
        var allowed = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../artifacts/native/development-host", revision)) + Path.DirectorySeparatorChar;
        if (!sandbox.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new IOException("Only this worktree's source-labelled ignored host sandbox is allowed.");
        using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(sandbox, "host-sandbox.json")));
        var identity = marker.RootElement;
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        if (principal.IsInRole(WindowsBuiltInRole.Administrator) || !Environment.UserInteractive ||
            Process.GetCurrentProcess().SessionId != HostActiveConsoleSession() ||
            identity.GetProperty("qualification").GetString() != "DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION" ||
            identity.GetProperty("cleanMachineCertified").GetBoolean() ||
            identity.GetProperty("sourceRevision").GetString() != revision || identity.GetProperty("sourceTree").GetString() != tree ||
            identity.GetProperty("executableSha256").GetString() != executableSha256 ||
            !sandbox.Contains(Path.DirectorySeparatorChar + "development-host" + Path.DirectorySeparatorChar + revision + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            DateTimeOffset.UtcNow - identity.GetProperty("preparedAtUtc").GetDateTimeOffset() > TimeSpan.FromHours(24))
            throw new IOException("Normal active console and fresh independently pinned development-host sandbox required.");
        var executable = Path.Combine(sandbox, "payload", "LensDocsStudio.Native.exe");
        if (Hash(executable) != executableSha256 || !FileVersionInfo.GetVersionInfo(executable).ProductVersion!.EndsWith("+" + revision, StringComparison.Ordinal))
            throw new IOException("Portable app bytes/source differ from the independent pins.");
        using var verification = JsonDocument.Parse(File.ReadAllText(Path.Combine(sandbox, "package-verification.json")));
        var payload = verification.RootElement.GetProperty("payload").EnumerateArray().ToArray();
        if (payload.Length != 528 || Directory.GetFiles(Path.Combine(sandbox, "payload"), "*", SearchOption.AllDirectories).Length != 528)
            throw new IOException("Complete 528-file portable inventory required.");
        foreach (var file in payload)
        {
            var path = Path.GetFullPath(Path.Combine(sandbox, "payload", file.GetProperty("path").GetString()!));
            if (!path.StartsWith(Path.Combine(sandbox, "payload") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Payload escape.");
            WorkspacePath.RejectReparsePoints(path);
            if (Hash(path) != file.GetProperty("sha256").GetString()) throw new IOException("Payload hash differs.");
        }
        if (Process.GetProcessesByName("LensDocsStudio.Native").Length != 0) throw new IOException("Existing native process; do not attach or replace it.");
        var output = Path.Combine(sandbox, "ui");
        if (Directory.Exists(output)) throw new IOException("UI evidence already exists; prepare a new sandbox.");
        Directory.CreateDirectory(output);
        var fixtures = Path.Combine(sandbox, "workspace", "families"); Directory.CreateDirectory(fixtures);
        var sources = HostSources().ToArray();
        foreach (var (family, _, source) in sources) File.WriteAllText(Path.Combine(fixtures, family + ".mmd"), source);
        WriteNew(Path.Combine(output, "fixtures-before.json"), sources.Select(row => new { row.Family, sha256 = Hash(Path.Combine(fixtures, row.Family + ".mmd")), source = row.Source }));
        var outcomes = new List<object>();
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        foreach (var (family, prefix, source) in shellOnly ? sources.Take(0) : sources)
        {
            Application? app = null; Window? main = null, visual = null;
            var checks = new List<string>(); var watch = Stopwatch.StartNew();
            var familyOutput = Path.Combine(output, family); Directory.CreateDirectory(familyOutput);
            try
            {
                var state = Path.Combine(sandbox, "state", "ui-" + family); Directory.CreateDirectory(state);
                WriteNew(Path.Combine(state, "preferences.json"), new { AutomaticUpdateChecks = false, Theme = "Light" });
                var fixture = Path.Combine(fixtures, family + ".mmd");
                WriteNew(Path.Combine(familyOutput, "before.json"), new { sourceSha256 = Hash(fixture), preferencesSha256 = Hash(Path.Combine(state, "preferences.json")), revision, tree });
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = fixtures };
                foreach (var arg in new[] { "--state-directory", state, Path.Combine(sandbox, "workspace"), fixture }) start.ArgumentList.Add(arg);
                using var actualProcess = Process.Start(start) ?? throw new IOException("Could not start the synthetic portable process.");
                actualProcess.EnableRaisingEvents = true;
                app = Application.Attach(actualProcess.Id);
                main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Actual owner HWND did not appear within the unchanged 25-second owner deadline.");
                var owner = Find(main, "MarkdownEditor");
                Wait(() => ReadEditorText(owner) == source, "Standalone source did not load exactly.");
                Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "Normal Visual Edit entry did not render.");
                CaptureHostRuntime(actualProcess, executable, familyOutput, revision, tree);
                checks.Add("Standalone source and normal Visual Edit entry correspond exactly");
                Open();
                var sourceId = prefix == "Mermaid" ? "MermaidDraftSource" : prefix + (family is "Sequence" or "State" or "Er" or "Class" ? "Source" : "SourceEditor");
                var validateId = prefix == "Mermaid" ? "MermaidValidateSource" : prefix + "Validate";
                var list = Find(visual!, family == "Sequence" ? "SequenceInteractions" : prefix + "Elements").AsListBox();
                FocusWindow(visual!); list.Focus(); Keyboard.Type(VirtualKeyShort.HOME);
                Wait(() => list.SelectedItems.Length > 0, "Keyboard Home did not select a source occurrence.");
                Keyboard.Type(VirtualKeyShort.END);
                Wait(() => list.SelectedItems.Length > 0 && !list.SelectedItems[^1].IsOffscreen, "Keyboard End did not reveal its selected source occurrence.");
                if (family == "Treemap")
                {
                    var zero = Array.FindIndex(list.Items, item => item.Name.Contains("Planned", StringComparison.Ordinal));
                    if (zero < 0) throw new IOException("Zero-area Treemap source occurrence is missing from the keyboard list.");
                    list.Focus(); Keyboard.Type(VirtualKeyShort.HOME);
                    for (var step = 0; step < zero; step++) Keyboard.Type(VirtualKeyShort.DOWN);
                    Wait(() => list.SelectedItems.Any(item => item.Name.Contains("Planned", StringComparison.Ordinal)), "Keyboard navigation did not reach the zero-area Treemap source occurrence.");
                    checks.Add("Zero-value Treemap occurrence remains an actual keyboard target");
                }
                var selected = list.SelectedItems.Select(item => item.Name).ToArray();
                Find(visual!, prefix + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand();
                var privateSource = Find(visual!, sourceId);
                Wait(() => ReadEditorText(privateSource) == source, "Private source does not exactly match standalone bytes.");
                Find(visual!, validateId).AsButton().Invoke();
                Wait(() => prefix == "Mermaid" ? Find(visual!, "MermaidStatus").Name.StartsWith("Ready", StringComparison.Ordinal) : Find(visual!, prefix + "Apply").IsEnabled,
                    "Private draft validation did not return its family-specific ready state.");
                if (prefix == "Mermaid" && Find(visual!, "MermaidApply").IsEnabled) throw new IOException("Unchanged Flowchart draft unexpectedly permits an owner mutation.");
                if (ReadEditorText(privateSource) != source) throw new IOException("Validation changed private source.");
                checks.Add("Actual keyboard occurrence selection and private validation retain exact source");
                foreach (var size in new[] { (880, 600), (1280, 800) })
                {
                    var scale = HostWindowDpi(visual!.Properties.NativeWindowHandle.Value) / 96.0;
                    visual.Patterns.Transform.Pattern.Resize(size.Item1 * scale, size.Item2 * scale);
                    Find(visual, prefix + "Fit").AsButton().Invoke();
                    Capture(visual, familyOutput, $"viewport-{size.Item1}x{size.Item2}", selected);
                }
                Find(visual!, prefix + "Cancel").AsButton().Invoke(); WaitClosed();
                if (ReadEditorText(owner) != source || File.ReadAllText(fixture) != source) throw new IOException("Cancel changed owner or saved source.");
                checks.Add("Cancel preserves owner and saved bytes");
                Open();
                Find(visual!, prefix + "SourcePanel").Patterns.ExpandCollapse.Pattern.Expand();
                if (ReadEditorText(Find(visual!, sourceId)) != source) throw new IOException("Reopened private source differs.");
                Find(visual!, prefix + "Cancel").AsButton().Invoke(); WaitClosed();
                checks.Add("Close/reopen returns the same private source");
                Capture(main, familyOutput, "owner-after-reopen", selected);
                FocusWindow(main); Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                Wait(() => app.HasExited, "Synthetic native app did not close normally.");
                if (actualProcess.ExitCode != 0) throw new IOException("Synthetic app exit failed.");
                var result = new { status = "PASS", family, prefix, checks, selected, milliseconds = watch.Elapsed.TotalMilliseconds, sourceSha256 = Hash(fixture) };
                WriteNew(Path.Combine(familyOutput, "completion.json"), result); outcomes.Add(result);
                Console.WriteLine("HOST LOCAL PASS: " + family + " — standalone/open/keyboard/validate/Cancel/reopen");
            }
            catch (Exception error)
            {
                try { if (visual is not null) Capture(visual, familyOutput, "failure", []); else if (main is not null) Capture(main, familyOutput, "failure", []); } catch (Exception captureError) { File.WriteAllText(Path.Combine(familyOutput, "capture-error.txt"), captureError.ToString()); }
                var result = new { status = "FAIL", family, checks, error = error.ToString(), milliseconds = watch.Elapsed.TotalMilliseconds, processId = app?.ProcessId };
                WriteNew(Path.Combine(familyOutput, "completion.json"), result); outcomes.Add(result);
                WriteNew(Path.Combine(output, "completion.json"), new { status = "FAIL", scope = "DEVELOPMENT_HOST_UI", revision, tree, executableSha256, outcomes, remaining = "NOT RUN after first failure; process left for observation, no uncertain retry or automatic kill" });
                Console.Error.WriteLine(error); return 1;
            }
            finally { app?.Dispose(); }

            void Open()
            {
                Find(main!, "DiagramEditVisually0").AsButton().Invoke();
                Wait(() => (visual = HostWindowById(automation, app!.ProcessId, prefix + "VisualEditor")) is not null, "Actual native family HWND did not open.");
                Wait(() => prefix == "Mermaid" ? Find(visual!, "MermaidStatus").Name.StartsWith("Ready", StringComparison.Ordinal) : Find(visual!, prefix + "Apply").IsEnabled,
                    "Initial native draft did not reach its family-specific ready state.");
            }
            void WaitClosed() { var handle = visual!.Properties.NativeWindowHandle.Value; Wait(() => !HostIsWindow(handle), "Native editor HWND did not close."); visual = null; }
            void Capture(Window window, string folder, string name, string[] selection)
            {
                using var process = Process.GetProcessById(app!.ProcessId);
                var handle = window.Properties.NativeWindowHandle.Value;
                if (handle == IntPtr.Zero || process.SessionId != Process.GetCurrentProcess().SessionId || !HostVisibleWindow(handle)) throw new IOException("A real visible HWND in this console session is required.");
                FocusWindow(window); _ = DwmFlush();
                window.CaptureToFile(Path.Combine(folder, name + ".png"));
                var controls = window.FindAllDescendants().Select(element => new { id = element.Properties.AutomationId.ValueOrDefault, name = element.Properties.Name.ValueOrDefault, focused = element.Properties.HasKeyboardFocus.ValueOrDefault, offscreen = element.Properties.IsOffscreen.ValueOrDefault, bounds = element.Properties.BoundingRectangle.ValueOrDefault.ToString() }).ToArray();
                WriteNew(Path.Combine(folder, name + ".json"), new { scope = "DEVELOPMENT_HOST_UI", revision, tree, executableSha256, processId = process.Id, processStartUtc = process.StartTime.ToUniversalTime(), hwnd = handle.ToInt64(), dpi = HostWindowDpi(handle), bounds = window.BoundingRectangle.ToString(), selection, controls });
            }
        }
        WriteNew(Path.Combine(output, "entry-matrix-completion.json"), new { status = shellOnly ? "NOT RUN" : "PASS", scope = "DEVELOPMENT_HOST_ENTRY_MATRIX", revision, tree, executableSha256, outcomes });
        try { RunDevelopmentHostShell(executable, sandbox, automation, revision, tree); RunDevelopmentHostExports(sandbox, revision); }
        catch (Exception error)
        {
            WriteNew(Path.Combine(output, "completion.json"), new { status = "FAIL", scope = shellOnly ? "DEVELOPMENT_HOST_SHELL_UI_ONLY" : "DEVELOPMENT_HOST_UI", revision, tree, executableSha256, outcomes, error = error.ToString() });
            throw;
        }
        WriteNew(Path.Combine(output, "completion.json"), new { status = "PASS", scope = shellOnly ? "DEVELOPMENT_HOST_SHELL_UI_ONLY" : "DEVELOPMENT_HOST_UI", entryMatrix = shellOnly ? "NOT RUN in this session" : "27 cases across 24 families", revision, tree, executableSha256, harness = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, outcomes,
            limitations = new[] { "Bounded entry/keyboard/validation/Cancel/reopen only. Full hosted mutation/protection suites remain separate.", "Existing OS scale only; no physical screen-reader, High Contrast, clean-machine or alternate-scale certification." } });
        return 0;
    }).GetAwaiter().GetResult();

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static void CaptureHostRuntime(Process process, string executable, string output, string revision, string tree)
    {
        var payload = Path.GetDirectoryName(executable)!;
        var modules = process.Modules.Cast<ProcessModule>().ToArray();
        var runtime = new[] { "coreclr.dll", "wpfgfx_cor3.dll" }.Select(name =>
        {
            var found = modules.Where(module => module.ModuleName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (found.Length != 1 || !Path.GetDirectoryName(found[0].FileName)!.Equals(payload, StringComparison.OrdinalIgnoreCase)) throw new IOException("Actual CoreCLR/WPF module is not loaded from the verified portable payload.");
            return new { name, path = "<sandbox>/payload/" + name, sha256 = Hash(found[0].FileName), version = FileVersionInfo.GetVersionInfo(found[0].FileName).ProductVersion };
        }).ToArray();
        WriteNew(Path.Combine(output, "runtime.json"), new { status = "PASS", scope = "DEVELOPMENT_HOST_LOADED_MODULES", processId = process.Id, revision, tree, runtime, cleanMachineCertified = false });
    }
    private static void WriteNew(string path, object value) { using var stream = new FileStream(path, FileMode.CreateNew); JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true }); stream.Flush(true); }
    [DllImport("kernel32.dll", EntryPoint = "WTSGetActiveConsoleSessionId")] private static extern uint HostActiveConsoleSession();
    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")] private static extern bool HostVisibleWindow(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")] private static extern uint HostWindowDpi(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "IsWindow")] private static extern bool HostIsWindow(IntPtr window);
    private delegate bool HostEnumWindow(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", EntryPoint = "EnumWindows")] private static extern bool HostEnumWindows(HostEnumWindow callback, IntPtr parameter);
    private static Window[] HostWindows(UIA3Automation automation, int processId)
    {
        var handles = new List<IntPtr>();
        HostEnumWindows((handle, _) => { GetWindowThreadProcessId(handle, out var owner); if (owner == processId && HostVisibleWindow(handle)) handles.Add(handle); return true; }, IntPtr.Zero);
        return handles.Select(handle => automation.FromHandle(handle).AsWindow()).ToArray();
    }
    private static Window? HostWindowById(UIA3Automation automation, int processId, string id) =>
        HostWindows(automation, processId).SingleOrDefault(window => window.Properties.AutomationId.ValueOrDefault == id);

    private static IEnumerable<(string Family, string Prefix, string Source)> HostSources()
    {
        yield return ("Flowchart", "Mermaid", "flowchart LR\nA[Write] --> B[Review]\n");
        yield return ("Sequence", "Sequence", "sequenceDiagram\nparticipant A as Author\nparticipant B as Reviewer\nA->>B: Review guide\n");
        yield return ("State", "State", "stateDiagram-v2\n[*] --> Draft\nDraft --> Review: Submit\nReview --> [*]\n");
        yield return ("Er", "Er", "erDiagram\nAUTHOR ||--o{ DOCUMENT : writes\nAUTHOR {\n int id PK\n string name\n}\nDOCUMENT {\n int id PK\n}\n");
        yield return ("Class", "Class", "classDiagram\nclass Document {\n+string title\n+Save()\n}\nclass Author\nAuthor --> Document : writes\n");
        yield return ("Gantt", "Gantt", LensDocsStudio.Core.GanttDiagrams.GanttVisualDraft.Starter);
        yield return ("Mindmap", "Mindmap", LensDocsStudio.Core.Mindmaps.MindmapVisualDraft.Starter);
        yield return ("Pie", "Pie", LensDocsStudio.Core.PieDiagrams.PieVisualDraft.Starter);
        yield return ("Timeline", "Timeline", LensDocsStudio.Core.TimelineDiagrams.TimelineVisualDraft.Starter);
        yield return ("Journey", "Journey", LensDocsStudio.Core.JourneyDiagrams.JourneyVisualDraft.Starter);
        yield return ("Quadrant", "Quadrant", LensDocsStudio.Core.QuadrantDiagrams.QuadrantVisualDraft.Starter);
        yield return ("Xy", "XYChart", LensDocsStudio.Core.XyCharts.XyVisualDraft.Starter);
        yield return ("Radar", "Radar", LensDocsStudio.Core.RadarCharts.RadarVisualDraft.Starter);
        yield return ("Sankey", "Sankey", LensDocsStudio.Core.SankeyDiagrams.SankeyVisualDraft.Starter);
        yield return ("Kanban", "Kanban", LensDocsStudio.Core.KanbanDiagrams.KanbanVisualDraft.Starter);
        yield return ("Requirement", "Requirement", LensDocsStudio.Core.RequirementDiagrams.RequirementVisualDraft.Starter);
        yield return ("GitGraph", "GitGraph", LensDocsStudio.Core.GitGraphDiagrams.GitGraphVisualDraft.Starter);
        yield return ("Architecture", "Architecture", LensDocsStudio.Core.ArchitectureDiagrams.ArchitectureVisualDraft.Starter);
        yield return ("C4", "C4", LensDocsStudio.Core.C4Diagrams.C4VisualDraft.Starter);
        yield return ("C4Container", "C4", "C4Container\nPerson(reader, \"Reader\")\nContainer(ui, \"Studio\", \"WPF\", \"Documentation\")\nRel(reader, ui, \"Uses\")\n");
        yield return ("C4Component", "C4", "C4Component\nComponent(editor, \"Editor\", \"WPF\", \"Markdown\")\nComponent(preview, \"Preview\", \"WPF\", \"Native\")\nRel(editor, preview, \"Renders\")\n");
        yield return ("Block", "Block", LensDocsStudio.Core.BlockDiagrams.BlockVisualDraft.Starter);
        yield return ("Packet", "Packet", LensDocsStudio.Core.PacketDiagrams.PacketVisualDraft.Starter);
        yield return ("TreeView", "TreeView", LensDocsStudio.Core.TreeViewDiagrams.TreeViewVisualDraft.Starter);
        yield return ("TreeViewBox", "TreeView", "treeView\n├── Guides\n└── Reference\n");
        yield return ("Treemap", "Treemap", LensDocsStudio.Core.TreemapDiagrams.TreemapVisualDraft.Starter);
        yield return ("Venn", "Venn", LensDocsStudio.Core.VennDiagrams.VennVisualDraft.Starter);
    }
}
