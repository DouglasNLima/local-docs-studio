using System.Text.Json;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using Mermaider;
using Mermaider.Models;

namespace LensDocsStudio.Core.Tests;

[CollectionDefinition("Complex ER", DisableParallelization = true)]
public sealed class ComplexErCollection { }

[Collection("Complex ER")]
public sealed class ComplexErRenderingTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-complex-er", "manifest.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.EnumerateArray().Select(x => new object[] { x.GetProperty("name").GetString()!, x.GetProperty("entities").GetInt32(), x.GetProperty("relationships").GetInt32(), x.GetProperty("attributes").GetInt32() }).ToArray();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Complex_fixture_preserves_every_semantic_occurrence_and_exact_source(string name, int entities, int relationships, int attributes)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-complex-er", name + ".mmd"));
        var service = new DiagramService(); var render = service.Render(source);
        Assert.True(render.Success, render.Error); Assert.Equal(source, render.Source);
        Assert.Equal(entities, render.ErSemantics!.Entities.Count); Assert.Equal(relationships, render.ErSemantics.Relationships.Count);
        Assert.Equal(attributes, render.ErSemantics.Entities.Sum(x => x.Attributes.Count));
        Assert.Equal(entities, render.ErLayout!.Entities.Count); Assert.Equal(relationships, render.ErLayout.Relationships.Count);
        Assert.Equal(attributes, render.ErLayout.Entities.Sum(x => x.Attributes.Count));
        static string Identity(ErRelationship r) => JsonSerializer.Serialize(r);
        Assert.Equal(render.ErSemantics.Relationships.Select(Identity).Order(), render.ErLayout.Relationships.Select(r => Identity(new(r.Entity1, r.Entity2, r.Cardinality1, r.Cardinality2, r.Label, r.Identifying))).Order());
        foreach (var entity in render.ErSemantics.Entities)
            Assert.Equal(JsonSerializer.Serialize(entity.Attributes), JsonSerializer.Serialize(Assert.Single(render.ErLayout.Entities, x => x.Id == entity.Id).Attributes));
        Assert.All(render.ErLayout.Relationships, r => Assert.True(r.Points.Count >= 2 && r.Points.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y))));
        if (name.StartsWith("complete-", StringComparison.Ordinal))
            foreach (var edge in render.ErLayout.Relationships)
            foreach (var node in render.ErLayout.Entities.Where(node => node.Id != edge.Entity1 && node.Id != edge.Entity2))
            for (var index = 1; index < edge.Points.Count; index++)
            {
                var a = edge.Points[index - 1]; var b = edge.Points[index];
                var crosses = Math.Abs(a.X - b.X) < .01
                    ? a.X > node.X + 1 && a.X < node.X + node.Width - 1 && Math.Max(a.Y, b.Y) > node.Y + 1 && Math.Min(a.Y, b.Y) < node.Y + node.Height - 1
                    : Math.Abs(a.Y - b.Y) < .01 && a.Y > node.Y + 1 && a.Y < node.Y + node.Height - 1 && Math.Max(a.X, b.X) > node.X + 1 && Math.Min(a.X, b.X) < node.X + node.Width - 1;
                Assert.False(crosses, "A primary regression route crosses an unrelated entity body.");
            }
        var xml = XDocument.Parse(render.Svg!);
        Assert.Equal(entities, xml.Descendants().Count(x => (string?)x.Attribute("class") == "entity"));
        Assert.Equal(relationships, xml.Descendants().Count(x => (string?)x.Attribute("class") == "er-relationship"));
        Assert.Same(render, service.Render(source));
        // An independent render owns fresh models but produces identical canonical bytes.
        Assert.Equal(render.Svg, new DiagramService().Render(source).Svg);
    }

    [Fact]
    public void Small_pinned_layout_retains_exact_upstream_geometry()
    {
        var model = new ErDiagram {
            Entities = [new() { Id = "A", Label = "Alpha", Attributes = [] }, new() { Id = "B", Label = "Beta", Attributes = [new("int", "id", [ErKeyType.PK])] }],
            Relationships = [new("A", "B", ErCardinality.One, ErCardinality.ZeroMany, "label", true), new("A", "A", ErCardinality.ZeroOne, ErCardinality.Many, "self", false)]
        };
        var original = MermaidRenderer.LayoutProvider.LayoutEr(model);
        var candidate = BoundedErLayout.Layout(model, MermaidRenderer.LayoutProvider, default, 160);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(candidate));
    }

    [Theory]
    [InlineData("TD")]
    [InlineData("BT")]
    [InlineData("LR")]
    [InlineData("RL")]
    public void Reordered_dense_declarations_and_all_directions_retain_parallel_and_self_occurrences(string direction)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-complex-er", "complete-relationships.mmd"));
        var source = "erDiagram\n    direction " + direction + "\n" + string.Join('\n', lines.Skip(1).Reverse()) + "\n";
        var result = new DiagramService().Render(source);
        Assert.True(result.Success, result.Error); Assert.Equal(source, result.Source);
        Assert.Equal(39, result.ErLayout!.Entities.Count); Assert.Equal(115, result.ErLayout.Relationships.Count);
        var edges = result.ErSemantics!.Relationships;
        Assert.Contains(edges, r => r.Entity1 == r.Entity2);
        Assert.Contains(edges.GroupBy(r => (r.Entity1, r.Entity2)), group => group.Count() > 1);
        Assert.Contains(edges, r => r.Label.Length == 0); Assert.Contains(edges, r => r.Label.Length > 35);
        Assert.Contains(edges, r => r.Identifying); Assert.Contains(edges, r => !r.Identifying);
        Assert.All(result.ErLayout.Relationships, r => Assert.True(r.Points.Count >= 2));
    }

    [Fact]
    public void Unlabelled_self_relationship_remains_inside_the_complete_export_canvas()
    {
        var result = new DiagramService().Render("erDiagram\nA ||--o{ A : \"\"\n");
        Assert.True(result.Success, result.Error);
        var layout = result.ErLayout!;
        Assert.All(Assert.Single(layout.Relationships).Points, point => {
            Assert.InRange(point.X, 0, layout.Width); Assert.InRange(point.Y, 0, layout.Height);
        });
    }

    [Fact]
    public async Task Cancelling_dense_layout_stops_supported_search_and_does_not_cache_partial_output()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-complex-er", "complete-detailed.mmd"));
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using var listener = new System.Diagnostics.ActivityListener {
            ShouldListenTo = s => s.Name == "LensDocsStudio.Mermaid",
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
            ActivityStarted = a => { if (a.OperationName == "renderer.ER.sugiyama-layout-routing") { entered.Set(); cancellation.Cancel(); } }
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);
        var service = new DiagramService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() => service.Render(source, cancellationToken: cancellation.Token)));
        Assert.True(entered.IsSet);
        listener.Dispose();
        Assert.True(service.Render(source).Success);
    }
}
