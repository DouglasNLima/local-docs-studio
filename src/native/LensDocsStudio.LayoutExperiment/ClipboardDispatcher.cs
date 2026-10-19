using System.Diagnostics;
using System.Windows.Threading;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record ClipboardCommit(long StartTimestamp, long EndTimestamp, int Thread, bool CancellationDuringAtomicCommit, uint Sequence);

/// <summary>One window-owned OLE STA. Only immutable payload strings cross this boundary.</summary>
internal sealed class ClipboardDispatcher : IDisposable
{
    private readonly Thread thread;
    private readonly Task<Dispatcher> ready;
    private readonly object gate = new();
    private DispatcherOperation? queued;
    private TaskCompletionSource<ClipboardCommit>? queuedCompletion;
    private bool closed;
    public ClipboardCommit? LastCommit { get; private set; }
    public ClipboardDispatcher()
    {
        var completion = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously); ready = completion.Task;
        thread = new Thread(() => { completion.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); })
            { IsBackground = true, Name = "Experimental clipboard OLE STA" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    public Task<ClipboardCommit> Selected(SelectionClipboard.Payload payload, uint sequence, CancellationToken cancellation)
        => Commit(() =>
        {
            if (NativeClipboardService.Sequence != sequence) throw new IOException("The clipboard changed before the accepted copy could commit.");
            System.Windows.Clipboard.SetDataObject(SelectionClipboard.Create(payload), true);
        }, cancellation);
    public Task<ClipboardCommit> Rendered(ExportedClipboard payload, uint sequence, CancellationToken cancellation)
        => Commit(() => NativeClipboardService.WriteRendered(payload, sequence), cancellation);
    public Task<ClipboardCommit> Plain(string text, uint sequence, CancellationToken cancellation) => Commit(() =>
    {
        if (NativeClipboardService.Sequence != sequence) throw new IOException("The clipboard changed before copying the diagram source.");
        System.Windows.Clipboard.SetText(text);
    }, cancellation);
    private async Task<ClipboardCommit> Commit(Action write, CancellationToken cancellation)
    {
        var dispatcher = await ready.ConfigureAwait(false);
        var completion = new TaskCompletionSource<ClipboardCommit>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (closed) throw new OperationCanceledException();
            if (queued?.Status == DispatcherOperationStatus.Pending) { queued.Abort(); queuedCompletion?.TrySetCanceled(); }
            queuedCompletion = completion;
            queued = dispatcher.InvokeAsync(() =>
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested(); var start = Stopwatch.GetTimestamp();
                    write();
                    var committed = new ClipboardCommit(start, Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, cancellation.IsCancellationRequested, NativeClipboardService.Sequence);
                    LastCommit = committed; completion.TrySetResult(committed);
                }
                catch (OperationCanceledException) { completion.TrySetCanceled(); }
                catch (Exception error) { completion.TrySetException(error); }
                finally { lock (gate) if (ReferenceEquals(queuedCompletion, completion)) { queued = null; queuedCompletion = null; } }
            });
        }
        return await completion.Task.ConfigureAwait(false);
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (closed) return; closed = true;
            if (queued?.Status == DispatcherOperationStatus.Pending) { queued.Abort(); queuedCompletion?.TrySetCanceled(); }
            queued = null; queuedCompletion = null;
        }
        // An accepted active OS clipboard call is atomic. Never wait for it on the UI thread.
        _ = ready.ContinueWith(task => task.Result.BeginInvokeShutdown(DispatcherPriority.Background), TaskScheduler.Default);
    }
}
