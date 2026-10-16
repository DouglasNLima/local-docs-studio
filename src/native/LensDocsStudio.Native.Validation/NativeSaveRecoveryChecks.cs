using System.Diagnostics;
using System.Text.Json;
using System.Windows.Threading;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSaveRecoveryChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var root = Path.Combine(output, "save-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspace);
        var state = Path.Combine(root, "state");
        var path = Path.Combine(workspace, "Café.md");
        const string original = "# Saved baseline\r\n\r\nOriginal Café.\r\n";
        const string draft = "# Latest draft\r\n\r\nUnsaved Café and こんにちは 🌍.\r\n";
        var originalBytes = DocumentSnapshot.Encode(original, true); File.WriteAllBytes(path, originalBytes);
        var session = new SessionStore(state); var documents = new DocumentStore(Path.Combine(state, "writes"));
        var checks = new List<object>();
        try
        {
            Guid documentId;
            using (var model = Create())
            {
                Complete(model.InitialiseAsync([workspace, path]));
                Complete(model.PersistSessionAsync());
                var tab = model.ActiveDocument!; documentId = tab.Id; tab.EditorDocument.Text = draft;
                // No MainWindow or autosave timer is running: the save itself must retain current intent.
                bool rejected;
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    rejected = Reject(model.SaveTabAsync(tab, false));
                var restored = ReadSession();
                var latest = restored.Documents.Single(document => document.Id == documentId);
                var preserved = File.ReadAllBytes(path).AsSpan().SequenceEqual(originalBytes);
                var pending = Directory.GetFiles(documents.RecoveryDirectory, "*.pending.json").Length;
                var passed = rejected && latest.Text == draft && latest.SavedText == original && latest.Utf8Bom
                    && tab.IsDirty && tab.SavedSnapshot?.Text == original && preserved && pending == 0;
                Record(new { name = "Blocked destination retains current draft before any write journal", passed,
                    rejected, latestDraftRetained = latest.Text == draft, baselineRetained = latest.SavedText == original, preserved, pending });
                if (!passed) throw new IOException("A failed save did not retain the latest draft before the source commit.");
            }
            using (var restored = Create())
            {
                Complete(restored.InitialiseAsync([]));
                var tab = restored.ActiveDocument!;
                var passed = tab.Id == documentId && tab.EditorDocument.Text == draft && tab.IsDirty
                    && tab.SavedSnapshot?.Text == original && tab.SavedSnapshot.Utf8Bom
                    && File.ReadAllBytes(path).AsSpan().SequenceEqual(originalBytes);
                Record(new { name = "A fresh workspace model restores exact unsaved text and its saved baseline", passed });
                if (!passed) throw new IOException("Failed-save recovery lost the draft, saved baseline, BOM or document identity.");
                var save = restored.SaveTabAsync(tab, false); Complete(save);
                passed = save.Result && !tab.IsDirty && File.ReadAllBytes(path).AsSpan().SequenceEqual(DocumentSnapshot.Encode(draft, true))
                    && ReadSession().Documents.Single(document => document.Id == documentId).SavedText == draft;
                Record(new { name = "Retry saves the recovered draft with original UTF-8 BOM and exact line endings", passed });
                if (!passed) throw new IOException("Retrying the recovered save changed its source encoding or state.");

                tab.EditorDocument.Text = "New text must remain in the editor when recovery is unavailable.";
                bool rejected;
                using (var locked = new FileStream(Path.Combine(state, "session.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
                    rejected = Reject(restored.SaveTabAsync(tab, false));
                passed = rejected && tab.IsDirty && tab.SavedSnapshot?.Text == draft
                    && File.ReadAllBytes(path).AsSpan().SequenceEqual(DocumentSnapshot.Encode(draft, true));
                Record(new { name = "Unavailable recovery blocks source commit while retaining the editor draft", passed, rejected });
                if (!passed) throw new IOException("The source changed despite failure to preserve recovery state.");
            }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }

        WorkspaceViewModel Create() => new(documents, session, new WorkspaceService(), new DesktopDialogs());
        SessionState ReadSession()
        {
            var read = session.ReadAsync<SessionState>("session.json"); Complete(read);
            return read.Result ?? throw new IOException("Save recovery did not retain a session.");
        }
        void Record(object value)
        {
            checks.Add(value);
            File.WriteAllText(Path.Combine(output, "native-save-recovery.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static bool Reject(Task task)
    {
        try { Complete(task); return false; }
        catch (IOException) { return true; }
    }
    private static void Complete(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Save recovery validation exceeded its deadline.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}
