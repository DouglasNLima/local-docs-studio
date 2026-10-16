namespace LensDocsStudio.Native.Platform;

public static class StaWorker
{
    public static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { cancellationToken.ThrowIfCancellationRequested(); var result = work(); cancellationToken.ThrowIfCancellationRequested(); completion.SetResult(result); }
            catch (OperationCanceledException) { completion.SetCanceled(cancellationToken); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Lens document operation" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
