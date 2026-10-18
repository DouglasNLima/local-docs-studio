using System.Diagnostics.Tracing;

namespace LensDocsStudio.Native.Validation;

[EventSource(Name = "LensDocsStudio-Native-ContinuousHarness")]
internal sealed class ContinuousHarnessEvents : EventSource
{
    public static readonly ContinuousHarnessEvents Log = new();
    [Event(1, Level = EventLevel.Verbose)]
    public void Stage(string name) { if (IsEnabled()) WriteEvent(1, name); }
}
