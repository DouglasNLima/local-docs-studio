using System.Collections.Immutable;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record SnapshotIndices(ImmutableArray<int> Characters, ImmutableArray<int> Words)
{
    public static SnapshotIndices Create(LayoutSnapshot snapshot, CancellationToken cancellation)
    {
        var characters = new SortedSet<int> { 0, snapshot.Model.Text.Length };
        var words = new SortedSet<int>(snapshot.Model.WordBoundaries);
        foreach (var item in snapshot.Model.Items) words.Add(item.Start);
        foreach (var line in snapshot.Lines)
        { cancellation.ThrowIfCancellationRequested(); foreach (var caret in line.Carets) characters.Add(caret.Offset); }
        return new(characters.ToImmutableArray(), words.ToImmutableArray());
    }
    public static int Adjacent(ImmutableArray<int> values, int at, bool forward)
    {
        var index = values.BinarySearch(at);
        index = index < 0 ? forward ? ~index : ~index - 1 : forward ? index + 1 : index - 1;
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }
}
