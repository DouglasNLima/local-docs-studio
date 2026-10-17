using System.Diagnostics;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeMermaidPartialPerformanceChecks
{
    public static IReadOnlyList<object> Run()
    {
        var results = new List<object>();
        foreach (var (name, source, target, expectedPartial) in Fixtures())
        {
            var watch = Stopwatch.StartNew(); var model = new MermaidSourceParser().Parse(source);
            var analysis = model.Capabilities; var initial = watch.Elapsed.TotalMilliseconds;
            if (expectedPartial && model.Compatibility != MermaidCompatibility.PartiallyEditable) throw new IOException(name + ": " + model.Diagnostic);
            watch.Restart();
            for (var repeat = 0; repeat < 1000; repeat++)
            {
                _ = analysis.Query(MermaidOperationKind.NodeLabel, target);
                _ = analysis.Query(MermaidOperationKind.NodeId, "N0");
                if (!ReferenceEquals(analysis, model.Capabilities)) throw new IOException("Capability analysis was not cached by immutable source revision.");
            }
            var queries = watch.Elapsed.TotalMilliseconds;
            var service = new DiagramService(); var draft = new MermaidVisualDraft(source, service);
            watch.Restart(); var edit = draft.ExecuteAsync(new UpdateMermaidNode(target, "Updated synthetic label", FlowNodeShape.Process, target)).GetAwaiter().GetResult();
            var acceptedMs = watch.Elapsed.TotalMilliseconds;
            if (expectedPartial && !edit.Success) throw new IOException(name + ": " + edit.Error);
            if (!expectedPartial && (edit.Success || draft.Source != source || draft.CanUndo)) throw new IOException("Unknown dependencies changed protected source.");
            var beforeRejected = draft.Source; var history = draft.HistoryIdentities.ToArray(); watch.Restart();
            var rejected = draft.ExecuteAsync(new DeleteMermaidNodes(["N0"])).GetAwaiter().GetResult(); var rejectedMs = watch.Elapsed.TotalMilliseconds;
            if (rejected.Success || draft.Source != beforeRejected || !history.SequenceEqual(draft.HistoryIdentities)) throw new IOException("Protected rejection changed source or history.");
            if (edit.Success) { draft.Undo(); if (draft.Source != source) throw new IOException("Performance edit Undo was not exact."); }
            results.Add(new { Fixture = name, SourceCharacters = source.Length, Nodes = model.Nodes.Count, Connections = model.Edges.Count,
                State = model.Compatibility.ToString(), InitialAnalysisMilliseconds = initial, CapabilityQueries = 2000,
                TotalQueryMilliseconds = queries, MeanQueryMicroseconds = queries * 1000 / 2000,
                AllowedEditIncludingRenderMilliseconds = acceptedMs, EditAccepted = edit.Success, EditError = edit.Error,
                ProtectedRejectionMilliseconds = rejectedMs, SourceAndHistoryPreserved = true });
        }
        return results;
    }
    private static IEnumerable<(string Name, string Source, string Target, bool Partial)> Fixtures()
    {
        foreach (var count in new[] { 20, 100, 300 })
            yield return ($"Partial {count} nodes", "flowchart LR\nN0[Start] --> N1[Review] --> N2[Done]\n"
                + string.Join('\n', Enumerable.Range(3, count - 3).Select(index => $"N{index}[Node {index}]")) + "\n", "N" + (count - 1), true);
        yield return ("Eight nested groups and long label", "flowchart LR\nN0 --> N1 --> N2\n"
            + string.Join('\n', Enumerable.Range(0, 8).Select(index => $"subgraph G{index}[Group {index}]"))
            + "\nD[\"" + string.Join(' ', Enumerable.Repeat("Synthetic long label", 35)) + "\"]\n" + string.Concat(Enumerable.Repeat("end\n", 8)), "D", true);
        yield return ("Many repeated shared references", "flowchart LR\nN0 --> N1 --> N2\nD[Independent]\n"
            + string.Join('\n', Enumerable.Range(0, 90).Select(index => "N0 --> N2")), "D", true);
        yield return ("Many unknown protected statements", "flowchart LR\nN0 --> N1 --> N2\nD[Independent]\n"
            + string.Join('\n', Enumerable.Range(0, 450).Select(index => "click N0 \"https://example.invalid/" + index + "\"")), "D", false);
    }
}
