using System.Text.Json;
using System.Windows.Automation;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly bool previewDiagnostics = Environment.GetEnvironmentVariable("LENS_NATIVE_PREVIEW_DIAGNOSTICS") == "1";
    private long diagnosticSourceGeneration;
    private long diagnosticAcceptedGeneration = -1;
    private long diagnosticOutlineGeneration = -1;
    private int diagnosticRequest;
    private string diagnosticPhase = "idle";
    private string? diagnosticError;

    private void PublishPreviewDiagnostic(string phase)
    {
        diagnosticPhase = phase;
        PreviewConstructionEvents.Log.Stage("shell/" + phase, diagnosticRequest);
        if (!previewDiagnostics) return;
        AutomationProperties.SetHelpText(PreviewStatusLabel, JsonSerializer.Serialize(new {
            SourceGeneration = diagnosticSourceGeneration, AcceptedGeneration = diagnosticAcceptedGeneration,
            OutlineGeneration = diagnosticOutlineGeneration, Request = diagnosticRequest, Phase = diagnosticPhase,
            Document = model.ActiveDocument?.Id, RenderedDocument = renderedDocumentId,
            CurrentSource = renderedVersion is not null && model.ActiveDocument is { } active
                && renderedDocumentId == active.Id && renderedVersion.CompareAge(active.EditorDocument.Version) == 0,
            Queued = previewTimer.IsEnabled, WorkerCapacity = previewGate.CurrentCount, Closing = closing, Error = diagnosticError
        }));
    }
}
