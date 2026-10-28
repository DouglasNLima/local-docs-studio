namespace LensDocsStudio.Core.Rendering;

public enum PreviewLoadingPhase { Idle, Queued, Parsing, Diagrams, Media, Building, Attaching, Completed, Failed, Cancelled }

public sealed record PreviewLoadingContext(Guid Document, long Revision, string Appearance, string Layout);

/// <summary>Request-owned presentation state. The host supplies monotonic time; no work or timers run here.</summary>
public sealed class PreviewLoadingState
{
    public static TimeSpan VisibilityDelay { get; } = TimeSpan.FromMilliseconds(200);
    public long Generation { get; private set; }
    public PreviewLoadingContext? Context { get; private set; }
    public PreviewLoadingPhase Phase { get; private set; }
    public bool Refresh { get; private set; }
    public TimeSpan PendingSince { get; private set; }
    public bool Busy => Phase is >= PreviewLoadingPhase.Queued and <= PreviewLoadingPhase.Attaching;
    public bool MasksContent => Context is not null && !Refresh && (Busy || Phase is PreviewLoadingPhase.Failed or PreviewLoadingPhase.Cancelled);
    public bool IsVisible(TimeSpan now) => Busy && now - PendingSince >= VisibilityDelay;

    public long Queue(PreviewLoadingContext context, bool acceptedPreview, TimeSpan now)
    {
        // Source edits transfer an existing busy presentation without flashing or restarting its delay.
        if (!Busy || Context?.Document != context.Document) PendingSince = now;
        Context = context; Refresh = acceptedPreview; Phase = PreviewLoadingPhase.Queued;
        return ++Generation;
    }

    public long Start(PreviewLoadingContext context, bool acceptedPreview, TimeSpan now)
    {
        if (!Busy || Context != context || Phase != PreviewLoadingPhase.Queued) Queue(context, acceptedPreview, now);
        if (PendingSince > now) PendingSince = now;
        Phase = PreviewLoadingPhase.Parsing;
        return Generation;
    }

    public bool Advance(long generation, PreviewLoadingPhase phase)
    {
        if (generation != Generation || !Busy) return false;
        Phase = phase;
        return true;
    }

    public void Reset()
    {
        Generation++; Context = null; Phase = PreviewLoadingPhase.Idle; Refresh = false;
    }
}
