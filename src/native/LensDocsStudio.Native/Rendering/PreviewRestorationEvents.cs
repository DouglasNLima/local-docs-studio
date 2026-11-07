using System.Diagnostics.Tracing;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Opt-in task boundaries; process-local identities, never authored text.</summary>
[EventSource(Name = "LensDocsStudio-Native-PreviewRestoration")]
internal sealed class PreviewRestorationEvents : EventSource
{
    internal static readonly PreviewRestorationEvents Log = new();

    [Event(1, Level = EventLevel.Verbose)]
    public void Restoration(string phase, int reader, int operation)
    {
        if (IsEnabled()) WriteEvent(1, phase, reader, operation);
    }
}
