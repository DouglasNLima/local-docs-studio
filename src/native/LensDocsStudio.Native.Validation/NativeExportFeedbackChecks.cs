using System.Diagnostics;
using System.Text.Json;
using System.Windows.Threading;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeExportFeedbackChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            var root = Path.Combine(output, "feedback-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var state = Path.Combine(root, "state");
            using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
            var path = Path.Combine(root, "Café & evidence (1).html"); byte[] bytes = [1, 2, 3]; File.WriteAllBytes(path, bytes);
            Complete(model.CompleteExportAsync(path, bytes));
            var saved = model.CompletedExport ?? throw new IOException("The successful saved output has no completion action.");
            if (saved.Path != path || model.ExportHistory.Entries.Count != 1 || !model.Status.StartsWith("Export completed — Café", StringComparison.Ordinal))
                throw new IOException("The native export model lost its actual saved destination or completion status.");
            var open = ExportShellService.OpenFile(saved);
            if (!open.UseShellExecute || open.FileName != path || open.Arguments.Length != 0 || open.ArgumentList.Count != 0)
                throw new IOException("Native export actions stopped delegating the exact file to its Windows association.");
            model.Status = "Newer operation";
            if (model.CompletedExport is not null || model.ExportHistory.Entries.Single() != saved) throw new IOException("A newer status did not dismiss only its current export actions.");
            Complete(model.RunAsync(async () =>
            {
                model.CancelOperationCommand.Execute(null);
                await model.CompleteExportAsync(path, bytes);
            }));
            if (model.CompletedExport is not null || model.ExportHistory.Entries.Count != 1) throw new IOException("A cancelled operation exposed a success receipt.");
            File.Delete(path);
            var rejected = false;
            try { Complete(new ExportShellService().InvokeAsync(model.ExportHistory, saved, false)); }
            catch (IOException) { rejected = true; }
            if (!rejected) throw new IOException("The native shell action did not reject its missing destination before process launch.");
            rejected = false;
            try { Complete(new ExportShellService().InvokeAsync(model.ExportHistory, saved, true)); }
            catch (IOException) { rejected = true; }
            if (!rejected) throw new IOException("The native folder-selection action did not reject its missing destination before invoking the shell.");
            var unsupported = saved with { Id = Guid.NewGuid(), Path = Path.Combine(root, "unsafe.cmd") };
            try { Complete(new ExportShellService().InvokeAsync(model.ExportHistory, unsupported, false)); throw new IOException("An unrecognised export reached the Windows shell."); }
            catch (IOException error) when (error.Message.StartsWith("This export is no longer", StringComparison.Ordinal)) { }
            File.WriteAllText(Path.Combine(output, "native-export-feedback.json"), JsonSerializer.Serialize(new {
                Passed = true, ExactSavedDestination = true, WindowsFileAssociationDescriptor = true,
                NewStatusRetainsSessionHistory = true, CancellationCannotPublishSuccess = true,
                MissingAndUnrecognisedOutputsRejectedBeforeShell = true,
                Limitation = "The Open descriptor and rejecting paths are exercised here without launching desktop apps; successful native Shell folder selection is exercised by hosted UIA." }));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static void Complete(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new IOException("The export feedback fixture exceeded its deadline.");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false); Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}
