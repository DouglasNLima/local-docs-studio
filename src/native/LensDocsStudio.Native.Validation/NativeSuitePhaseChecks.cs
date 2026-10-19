using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Core.StateDiagrams;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSuitePhaseChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); using var recorder = new MermaidPhaseRecorder();
        try
        {
            foreach (var family in new[] { "Flowchart", "Sequence", "State", "ER", "Class" })
            {
                using var scenario = MermaidDiagnostics.Measure("scenario." + family);
                var source = NativeSuitePerformanceChecks.Source(family, false); var service = new DiagramService(); var svg = new NativeSvgService();
                var render = service.Render(source);
                if (!render.Success) throw new IOException(family + ": phase fixture failed rendering.");
                bool success;
                switch (family)
                {
                    case "Flowchart":
                        _ = NativeMermaidPresentation.Create(render, svg);
                        var flow = new MermaidVisualDraft(source, service); success = flow.ValidateAndCommitAsync(source).GetAwaiter().GetResult().Success;
                        if (flow.Source != source || flow.CanUndo) throw new IOException("Phase tracing changed Flowchart source/history."); break;
                    case "Sequence":
                        _ = NativeSequencePresentation.Create(new SequenceSourceParser().Parse(source), render, svg);
                        var sequence = new SequenceVisualDraft(source, service); success = sequence.ValidateSourceAsync(source).GetAwaiter().GetResult().Success;
                        if (sequence.Source != source || sequence.CanUndo) throw new IOException("Phase tracing changed Sequence source/history."); break;
                    case "State":
                        _ = NativeStatePresentation.Create(new StateDiagramSourceParser().Parse(source), render, svg);
                        var state = new StateVisualDraft(source, service); success = state.ValidateSourceAsync(source).GetAwaiter().GetResult().Success;
                        if (state.Source != source || state.CanUndo) throw new IOException("Phase tracing changed State source/history."); break;
                    case "ER":
                        _ = NativeErPresentation.Create(new ErDiagramSourceParser().Parse(source), render, svg);
                        var er = new ErVisualDraft(source, service); success = er.ValidateSourceAsync(source).GetAwaiter().GetResult().Success;
                        if (er.Source != source || er.CanUndo) throw new IOException("Phase tracing changed ER source/history."); break;
                    default:
                        _ = NativeClassPresentation.Create(new ClassDiagramSourceParser().Parse(source), render, svg);
                        var cls = new ClassDiagramVisualDraft(source, service); success = cls.ValidateSourceAsync(source).GetAwaiter().GetResult().Success;
                        if (cls.Source != source || cls.CanUndo) throw new IOException("Phase tracing changed Class source/history."); break;
                }
                if (!success) throw new IOException(family + ": phase tracing failed private validation.");
            }
        }
        finally { recorder.Write(Path.Combine(output, "suite-phases.json")); }
    }
}
