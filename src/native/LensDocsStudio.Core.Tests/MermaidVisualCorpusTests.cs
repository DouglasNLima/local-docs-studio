using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidVisualCorpusTests
{
    [Theory]
    [InlineData("simple")] [InlineData("branching")] [InlineData("repeated-labels")] [InlineData("nested")]
    [InlineData("styled")] [InlineData("complex")] [InlineData("unicode")]
    public async Task Supported_corpus_maps_exact_spans_and_keeps_every_unrelated_character(string fixture)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-mermaid-visual", fixture + ".mmd"));
        var draft = new MermaidVisualDraft(source); Assert.True(draft.Model.CanEdit, draft.Model.Diagnostic);
        Assert.Equal(source, draft.Source); Assert.False(draft.IsDirty);
        foreach (var occurrence in draft.Model.Nodes.SelectMany(node => node.Occurrences)) Assert.Equal(occurrence.Id, occurrence.IdSpan.Read(source));
        foreach (var edge in draft.Model.Edges)
        { Assert.Equal(edge.Source.Id, edge.Source.IdSpan.Read(source)); Assert.Equal(edge.Target.Id, edge.Target.IdSpan.Read(source)); }
        var selected = draft.Model.Nodes.First(node => node.Declaration is not null);
        var unchanged = await draft.ExecuteAsync(new UpdateMermaidNode(selected.Id, selected.Label, selected.Shape, selected.Id));
        Assert.True(unchanged.Success, unchanged.Error); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        var label = selected.Declaration!.LabelSpan!.Value;
        var changed = await draft.ExecuteAsync(new UpdateMermaidNode(selected.Id, "Changed label", selected.Shape, selected.Id));
        Assert.True(changed.Success, changed.Error);
        Assert.Equal(source[..label.Start] + "\"Changed label\"" + source[label.End..], draft.Source);
        Assert.True(new DiagramService().Render(draft.Source).Success);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }

    [Theory]
    [InlineData("protected", MermaidCompatibility.Protected)]
    [InlineData("duplicate-declarations", MermaidCompatibility.Protected)]
    [InlineData("malformed", MermaidCompatibility.Invalid)]
    public async Task Protected_and_invalid_corpus_stays_byte_for_byte_available(string fixture, MermaidCompatibility compatibility)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-mermaid-visual", fixture + ".mmd"));
        var draft = new MermaidVisualDraft(source); Assert.Equal(compatibility, draft.Model.Compatibility);
        var result = await draft.ExecuteAsync(new AddMermaidNode("New", "New", FlowNodeShape.Process));
        Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }

    [Theory] [InlineData("TD")] [InlineData("TB")] [InlineData("BT")] [InlineData("LR")] [InlineData("RL")]
    public void Compound_layout_is_deterministic_finite_and_respects_direction(string direction)
    {
        var source = "flowchart " + direction + "\nsubgraph Parent[Parent]\nsubgraph Child[Child]\nA[One]\nB[Two]\nend\nend\nC[Three]\nA --> B\nB --> C\n";
        var model = new MermaidSourceParser().Parse(source); Assert.True(model.CanEdit, model.Diagnostic);
        var first = MermaidGraphLayout.Arrange(model); var second = MermaidGraphLayout.Arrange(model);
        Assert.Equal(first.OrderBy(pair => pair.Key), second.OrderBy(pair => pair.Key));
        Assert.All(first.Values, box => Assert.True(double.IsFinite(box.X + box.Y + box.Width + box.Height) && box.Width > 0 && box.Height > 0));
        var a = first["node:A"]; var b = first["node:B"];
        if (direction == "LR") Assert.True(a.X < b.X);
        if (direction == "RL") Assert.True(a.X > b.X);
        if (direction is "TD" or "TB") Assert.True(a.Y < b.Y);
        if (direction == "BT") Assert.True(a.Y > b.Y);
        var group = first["group:Child"];
        Assert.True(a.X >= group.X && a.Y >= group.Y && a.X + a.Width <= group.X + group.Width && a.Y + a.Height <= group.Y + group.Height);
    }

    [Theory] [InlineData(20)] [InlineData(100)] [InlineData(300)]
    public void Representative_node_counts_keep_stable_identities_and_complete_native_layout(int count)
    {
        var source = "flowchart LR\n" + string.Join('\n', Enumerable.Range(0, count).Select(index => $"Node{index}[Node {index}]"));
        var model = new MermaidSourceParser().Parse(source); Assert.True(model.CanEdit, model.Diagnostic);
        Assert.Equal(count, model.Nodes.Count); Assert.Equal(count, MermaidGraphLayout.Arrange(model).Count);
        var excessive = new MermaidSourceParser().Parse(source + "\nExtra[Over limit]\n");
        Assert.Equal(count < 300, excessive.CanEdit);
    }

    [Fact]
    public async Task Dense_graphs_are_protected_before_repeated_renderer_validation()
    {
        var source = "flowchart TD\n" + string.Join('\n', Enumerable.Range(0, 20).Select(index => $"Node{index}[Node {index}]")) + "\n"
            + string.Join('\n', Enumerable.Range(0, 20).SelectMany(from => Enumerable.Range(from + 1, 19 - from).Select(to => $"Node{from} --> Node{to}")));
        var draft = new MermaidVisualDraft(source); Assert.Equal(MermaidCompatibility.Protected, draft.Model.Compatibility);
        Assert.Contains("dense graph", draft.Model.Diagnostic); Assert.Equal(20, draft.Model.Nodes.Count); Assert.Equal(190, draft.Model.Edges.Count);
        var result = await draft.ExecuteAsync(new UpdateMermaidNode("Node0", "Changed", FlowNodeShape.Process, "Node0"));
        Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
}
