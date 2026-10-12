using System.Diagnostics.Tracing;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Opt-in phase markers for investigating native preview construction delays.</summary>
[EventSource(Name = "LensDocsStudio-Native-PreviewConstruction")]
internal sealed class PreviewConstructionEvents : EventSource
{
    public static readonly PreviewConstructionEvents Log = new();

    [Event(1, Level = EventLevel.Verbose)]
    public void Stage(string name, int sourceStart = -1)
    {
        if (IsEnabled()) WriteEvent(1, name, sourceStart);
    }
}
