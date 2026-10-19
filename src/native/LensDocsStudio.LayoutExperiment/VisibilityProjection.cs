using System.Collections.Immutable;
using System.Text;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Projects already complete geometry into the current expanded/collapsed document.</summary>
internal static class VisibilityProjection
{
    public static LayoutSnapshot Apply(LayoutSnapshot complete, CancellationToken cancellation)
    {
        if (!complete.Lines.Any(line => line.Hidden)) return complete;
        var model = complete.Model; var visible = new HashSet<int>(); Collect(model.Root);
        void Collect(SemanticNode node)
        {
            if (node.Paragraph >= 0) visible.Add(node.Paragraph);
            foreach (var child in node.Kind == "Details" && !node.Expanded ? node.Children.Take(1) : node.Children) Collect(child);
        }
        var text = new StringBuilder(); var starts = ImmutableArray.CreateBuilder<int>(); var ends = ImmutableArray.CreateBuilder<int>();
        var items = ImmutableArray.CreateBuilder<Item>(); var indices = new Dictionary<int, int>(); var offsets = new int[model.Text.Length + 1];
        for (var index = 0; index < model.Items.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested(); var item = model.Items[index];
            var end = index + 1 < model.Items.Length ? model.Items[index + 1].Start : model.Text.Length;
            var begin = text.Length;
            for (var at = item.Start; at < end; at++) offsets[at] = begin + (visible.Contains(index) ? at - item.Start : 0);
            if (!visible.Contains(index)) continue;
            indices[index] = items.Count;
            items.Add(item with { Start = begin, Runs = item.Runs.Select(run => run with { Start = begin + run.Start - item.Start }).ToImmutableArray() });
            text.Append(model.Text[item.Start..end]);
            for (var at = item.Start; at < end; at++) { starts.Add(model.SourceBoundaries[at]); ends.Add(model.SourceEnds[at]); }
        }
        offsets[^1] = text.Length; starts.Add(ends.Count > 0 ? ends[^1] : 0);
        SemanticNode Project(SemanticNode node) => node with
        {
            Start = offsets[node.Start], End = offsets[node.End], Paragraph = node.Paragraph >= 0 ? indices[node.Paragraph] : -1,
            Children = (node.Kind == "Details" && !node.Expanded ? node.Children.Take(1) : node.Children).Select(Project).ToImmutableArray()
        };
        var logical = text.ToString();
        var projected = model with { Text = logical, SourceBoundaries = starts.ToImmutable(), SourceEnds = ends.ToImmutable(), Items = items.ToImmutable(),
            Root = Project(model.Root), WordBoundaries = WordUnits.Create(logical, cancellation), OriginalListShape = false };
        var lines = complete.Lines.Where(line => !line.Hidden).Select(line => line with
        {
            Start = offsets[line.Start], End = offsets[line.End], Item = indices[line.Item],
            Carets = line.Carets.Select(caret => caret with { Offset = offsets[caret.Offset] }).ToImmutableArray(),
            Clusters = line.Clusters.Select(cluster => cluster with { Start = offsets[cluster.Start], End = offsets[cluster.End] }).ToImmutableArray()
        }).ToImmutableArray();
        return complete with { Model = projected, Lines = lines, CompleteModel = model, CompleteLines = complete.Lines };
    }
}
