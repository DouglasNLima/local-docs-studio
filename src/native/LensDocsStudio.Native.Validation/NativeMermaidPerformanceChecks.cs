using System.Diagnostics;
using System.Text.Json;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeMermaidPerformanceChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); var observations = new List<object>();
        foreach (var (name, source) in Fixtures())
        {
            var allocated = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew();
            var model = new MermaidSourceParser().Parse(source); var parse = watch.Elapsed.TotalMilliseconds;
            if (!model.CanEdit && name != "Dense connections") throw new IOException(name + ": " + model.Diagnostic);
            var service = new DiagramService(); watch.Restart();
            var render = service.Render(source); var rendering = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var presentation = render.Success ? NativeMermaidPresentation.Create(render, new NativeSvgService()) : null;
            var layout = watch.Elapsed.TotalMilliseconds;
            if (presentation is not null && model.CanEdit && !presentation.Maps(model)) throw new IOException("Canonical layout omitted an editable identity in " + name);
            var draft = new MermaidVisualDraft(source, service); var node = model.Nodes[0]; watch.Restart();
            var result = draft.ExecuteAsync(new UpdateMermaidNode(node.Id, "Updated label", node.Shape, node.Id)).GetAwaiter().GetResult();
            var edit = watch.Elapsed.TotalMilliseconds;
            if (!result.Success && draft.Source != source) throw new IOException("A rejected performance candidate changed source.");
            observations.Add(new { Fixture = name, Nodes = model.Nodes.Count, Connections = model.Edges.Count, Subgraphs = model.Subgraphs.Count,
                SourceCharacters = source.Length, ParseMilliseconds = parse, NativeLayoutMilliseconds = layout, RenderMilliseconds = rendering,
                EditAndRenderMilliseconds = edit, AllocatedProcessBytes = GC.GetTotalAllocatedBytes(true) - allocated,
                Rendered = render.Success, RenderError = render.Error, EditAccepted = result.Success, EditError = result.Error,
                RenderingSkippedForProtection = false,
                SourcePreservedAfterRejection = result.Success || draft.Source == source });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { Scenarios = observations, Environment.ProcessorCount,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Limits = "100,000 characters; 300 nodes; 500 total elements; eight nested subgraphs; unchanged three-second renderer deadline.",
            Limitations = "Single-process observations include cold/warm runtime effects; these are source/model/layout/render measurements, not physical input or monitor certification. Renderer rejection within editing bounds remains a protected source/preview case." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<(string Name, string Source)> Fixtures()
    {
        foreach (var count in new[] { 20, 100, 300 })
            yield return ($"{count} nodes", "flowchart LR\n" + string.Join('\n', Enumerable.Range(0, count).Select(index => $"Node{index}[Node {index}]"))
                + "\n" + string.Join('\n', Enumerable.Range(0, Math.Min(count - 1, 199)).Select(index => $"Node{index} --> Node{index + 1}")));
        yield return ("Dense connections", "flowchart TD\n" + string.Join('\n', Enumerable.Range(0, 20).Select(index => $"Node{index}[Node {index}]"))
            + "\n" + string.Join('\n', Enumerable.Range(0, 20).SelectMany(from => Enumerable.Range(from + 1, 19 - from).Select(to => $"Node{from} --> Node{to}"))));
        yield return ("Nested groups", "flowchart TD\n" + string.Join('\n', Enumerable.Range(0, 10).Select(group => $"subgraph Group{group}[Group {group}]\nsubgraph Inner{group}[Inner {group}]\n"
            + string.Join('\n', Enumerable.Range(group * 10, 10).Select(index => $"Node{index}[Node {index}]")) + "\nend\nend"))
            + "\n" + string.Join('\n', Enumerable.Range(0, 99).Select(index => $"Node{index} --> Node{index + 1}")));
        yield return ("Long labels", "flowchart BT\n" + string.Join('\n', Enumerable.Range(0, 20).Select(index => $"Node{index}[\"{string.Join(' ', Enumerable.Repeat("Long diagnostic context", 30))}\"]")));
    }
}
