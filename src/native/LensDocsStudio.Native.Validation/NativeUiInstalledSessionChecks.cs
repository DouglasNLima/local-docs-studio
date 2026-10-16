using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunInstalledSession(string executable, string state, string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
        { Console.Error.WriteLine("Installed session validation requires the disposable Windows GitHub-hosted runner."); return 1; }
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(CheckInstalledSession(executable, state, output)); }
            catch (Exception error) { Console.Error.WriteLine(error); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Installed session automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }

    private static int CheckInstalledSession(string executable, string state, string output)
    {
        executable = Path.GetFullPath(executable); state = Path.GetFullPath(state); output = Path.GetFullPath(output);
        var fixture = Directory.GetParent(state)!.FullName;
        using var priorRun = JsonDocument.Parse(ReadObservedText(Path.Combine(fixture, "native-ui.json")));
        if (!priorRun.RootElement.GetProperty("Passed").GetBoolean()
            || !string.Equals(priorRun.RootElement.GetProperty("FixtureRoot").GetString(), fixture, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Restore validation requires a completed, isolated native UI fixture.");
        var before = ReadSession();
        var preferences = ReadPreferences();
        if (before.SchemaVersion != 1 || before.Documents.Count == 0 || preferences.AutomaticUpdateChecks)
            throw new IOException("The preserved fixture must contain documents with automatic network checks disabled.");
        var unsavedSavedDocuments = before.Documents.Count(document => document.Path is not null && HasUnsavedText(document));
        var untitledDrafts = before.Documents.Count(document => document.Path is null && HasUnsavedText(document));
        if (unsavedSavedDocuments == 0 || untitledDrafts == 0)
            throw new IOException("Migration acceptance requires both a dirty saved document and an untitled draft.");
        var activeIndex = before.Documents.ToList().FindIndex(document => document.Id == before.ActiveDocument);
        if (activeIndex < 0) throw new IOException("The preserved fixture has no active document.");
        var stateFiles = Snapshot(state);
        var sources = before.Documents.Where(document => document.Path is not null).Select(document => document.Path!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        Process? started = null; Application? app = null; Window? window = null;
        using var automation = new UIA3Automation();
        try
        {
            var reusableBefore = InspectReusableState();
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            start.ArgumentList.Add("--state-directory"); start.ArgumentList.Add(state);
            started = Process.Start(start) ?? throw new IOException("The upgraded native application could not start.");
            app = Application.Attach(started.Id);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The preserved session window did not appear.");
            var editor = Find(window, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == before.Documents[activeIndex].Text, "The installed application did not restore the previous active document.");
            var tabs = Find(window, "DocumentTabs").FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
            if (tabs.Length != before.Documents.Count) throw new IOException("The installed application lost preserved document tabs.");
            for (var index = 0; index < tabs.Length; index++)
            {
                tabs[index].Patterns.SelectionItem.Pattern.Select();
                var expected = before.Documents[index];
                Wait(() => ReadEditorText(editor) == expected.Text, "The installed application changed recovered text for: " + expected.Id);
            }
            tabs[activeIndex].Patterns.SelectionItem.Pattern.Select();
            Wait(() => ReadEditorText(editor) == before.Documents[activeIndex].Text, "The original active tab was not restored after inspection.");
            CheckRestoredSettings(window, automation, app.ProcessId);
            window.CaptureToFile(Path.Combine(output, "restored-session.png"));
            CloseRetainingDrafts(window, automation, started, before.Documents.Count(HasUnsavedText));
            if (started.ExitCode != 0) throw new IOException("The restored session failed during shutdown: " + started.ExitCode);
            var after = ReadSession(); var afterPreferences = ReadPreferences();
            if (Canonical(before) != Canonical(after)
                || JsonSerializer.Serialize(preferences with { Window = null }) != JsonSerializer.Serialize(afterPreferences with { Window = null }))
                throw new IOException("Opening the preserved session changed document identity/text, workspace, settings or update consent.");
            // Normal shutdown can update caret/scroll/window positions and rotate these backups.
            // Every other retained state file and all workspace source bytes must remain exact.
            var mutable = new HashSet<string>(["session.json", "session.json.bak", "preferences.json", "preferences.json.bak"], StringComparer.OrdinalIgnoreCase);
            var remainingBefore = stateFiles.Where(entry => !mutable.Contains(entry.Key)).OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            var remainingAfter = Snapshot(state).Where(entry => !mutable.Contains(entry.Key)).OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            if (!remainingBefore.SequenceEqual(remainingAfter) || sources.Any(entry => Hash(entry.Key) != entry.Value))
                throw new IOException("Restoring the installed session changed a workspace source, library, snapshot or retained asset.");
            var reusableAfter = InspectReusableState();
            if (reusableBefore != reusableAfter) throw new IOException("Restoration changed usable templates, snippets, profiles or recovery snapshots.");
            Write(new { passed = true, restoredDocuments = tabs.Length, retainedStateFiles = remainingBefore.Length,
                workspaceFiles = sources.Count, unsavedSavedDocuments, untitledDrafts, sameSession = true, priorFixture = fixture, historicalSchemaCertification = false,
                reusableState = JsonSerializer.Deserialize<JsonElement>(reusableAfter),
                checks = new[] { "Prior active document and every preserved tab display exact text", "Saved appearance settings restore in native controls",
                    "Document identities, workspace, content, settings and update consent survive shutdown", "Library/snapshot/asset bytes and saved workspace sources remain unchanged",
                    "Retained library content parses, Word templates produce usable DOCX and recovery snapshots remain readable" } });
            Console.WriteLine("Installed session restoration: " + tabs.Length + " preserved tabs passed."); return 0;
        }
        catch (Exception error)
        {
            NativeUiFailureDiagnostics.Capture(started, output);
            try { window?.CaptureToFile(Path.Combine(output, "native-ui-failure.png")); } catch { }
            Write(new { passed = false, error = error.ToString(), priorFixture = fixture }); throw;
        }
        finally
        {
            if (started is { HasExited: false }) started.Kill(entireProcessTree: true);
            app?.Dispose(); started?.Dispose();
        }

        SessionState ReadSession() => JsonSerializer.Deserialize<SessionState>(ReadObservedText(Path.Combine(state, "session.json")))
            ?? throw new IOException("The preserved session is empty.");
        UserPreferences ReadPreferences() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(Path.Combine(state, "preferences.json")))
            ?? throw new IOException("The preserved preferences are empty.");
        void Write(object value) => File.WriteAllText(Path.Combine(output, "restored-session.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        static string Canonical(SessionState session) => JsonSerializer.Serialize(session with
            { Documents = session.Documents.Select(document => document with { CaretOffset = 0, VerticalOffset = 0 }).ToArray() });
        static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
        static Dictionary<string, string> Snapshot(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), Hash, StringComparer.OrdinalIgnoreCase);

        string InspectReusableState()
        {
            var retainedImages = StaWorker.RunAsync(() =>
            {
                var images = new List<object>();
                foreach (var document in before.Documents)
                {
                    var root = document.Path is null ? document.AssetWorkspace : document.WorkspaceRoot ?? before.Workspace;
                    var path = document.Path ?? (root is null ? null : Path.Combine(root, "draft.md"));
                    var parsed = new MarkdownService().Parse(document.Text, Path.GetExtension(path) is ".mmd" or ".mermaid");
                    foreach (var image in new PreviewImageService().Prepare(parsed, path, root))
                    {
                        if (image.Value.Pixels is not { } pixels || path is null || root is null)
                            throw new IOException("A retained workspace image cannot be displayed: " + image.Key + ". " + image.Value.Error);
                        var file = new WorkspacePath(root).ResolveLink(path, image.Key);
                        images.Add(new { document.Id, reference = image.Key, pixels.PixelWidth, pixels.PixelHeight, sha256 = Hash(file) });
                    }
                }
                if (images.Count == 0) throw new IOException("The original UI fixture must retain relative workspace images before migration acceptance.");
                return images;
            }).GetAwaiter().GetResult();
            var libraryDirectory = Path.Combine(state, "content-library");
            if (!File.Exists(Path.Combine(libraryDirectory, "library.json")))
                throw new IOException("The original UI fixture did not retain its actual content library.");
            var library = new LocalLibraryStore(libraryDirectory).ReadAsync().GetAwaiter().GetResult().State;
            var items = library.Active.Templates.Concat(library.Archived.Templates)
                .Concat(library.Active.Snippets).Concat(library.Archived.Snippets).ToArray();
            if (library.Active.Templates.Length + library.Archived.Templates.Length == 0
                || library.Active.Snippets.Length + library.Archived.Snippets.Length == 0)
                throw new IOException("The original UI fixture must retain usable templates and snippets before migration acceptance.");
            foreach (var item in items)
            {
                var imported = new MarkdownImageImporter().Import(item.Content);
                if (new MarkdownService().Parse(imported.Markdown).Source != imported.Markdown)
                    throw new IOException("A retained reusable entry lost editable source: " + item.Label);
            }
            var wordLibrary = new WordTemplateLibrary(Path.Combine(state, "word-templates"));
            var active = wordLibrary.ListAsync().GetAwaiter().GetResult();
            var archived = wordLibrary.ListAsync(archived: true).GetAwaiter().GetResult();
            if (active.Warnings.Count != 0 || archived.Warnings.Count != 0)
                throw new IOException("A retained Word template is unavailable after upgrade.");
            var templates = active.Entries.Concat(archived.Entries).ToArray();
            foreach (var template in templates)
            {
                var prepared = wordLibrary.ReadAsync(template).GetAwaiter().GetResult();
                const string body = "Historical template remains usable — Café.";
                var content = new ExportContent(new MarkdownService().Parse(body), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
                using var stream = new MemoryStream(new DocxExporter().Export(content, prepared.Bytes));
                using var package = WordprocessingDocument.Open(stream, false);
                if (package.MainDocumentPart?.Document?.InnerText.Contains(body, StringComparison.Ordinal) != true)
                    throw new IOException("A retained Word template could not export current document content.");
            }
            var snapshots = new DocumentSnapshotStore(state);
            var listing = snapshots.ListAsync().GetAwaiter().GetResult();
            if (listing.Warnings.Count != 0) throw new IOException("A retained recovery snapshot is unavailable after upgrade.");
            var recovered = listing.Entries.Select(entry => snapshots.ReadAsync(entry).GetAwaiter().GetResult()).ToArray();
            return JsonSerializer.Serialize(new { library, wordTemplates = templates.Select(entry => entry.Record).ToArray(),
                snapshots = recovered, templateCount = library.Active.Templates.Length + library.Archived.Templates.Length,
                snippetCount = library.Active.Snippets.Length + library.Archived.Snippets.Length, wordTemplateCount = templates.Length,
                snapshotCount = recovered.Length, retainedImages });
        }
    }
}
