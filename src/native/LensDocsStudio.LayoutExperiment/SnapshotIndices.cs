using System.Collections.Immutable;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record SnapshotIndices(ImmutableArray<int> Characters, ImmutableArray<int> Words)
{
    public static SnapshotIndices Create(LayoutSnapshot snapshot, CancellationToken cancellation)
    {
        // Offsets are bounded by the already-complete logical text. Avoid a tree
        // allocation/comparison for every caret (57 ms in the hosted list profile).
        var characters = new bool[snapshot.Model.Text.Length + 1];
        var words = new bool[characters.Length];
        characters[0] = characters[^1] = true;
        foreach (var offset in snapshot.Model.WordBoundaries) words[offset] = true;
        foreach (var item in snapshot.Model.Items) words[item.Start] = true;
        foreach (var line in snapshot.Lines)
        { cancellation.ThrowIfCancellationRequested(); foreach (var caret in line.Carets) characters[caret.Offset] = true; }
        var characterOffsets = ImmutableArray.CreateBuilder<int>(); var wordOffsets = ImmutableArray.CreateBuilder<int>();
        for (var offset = 0; offset < characters.Length; offset++)
        {
            if ((offset & 255) == 0) cancellation.ThrowIfCancellationRequested();
            if (characters[offset]) characterOffsets.Add(offset);
            if (words[offset]) wordOffsets.Add(offset);
        }
        return new(characterOffsets.ToImmutable(), wordOffsets.ToImmutable());
    }
    public static int Adjacent(ImmutableArray<int> values, int at, bool forward)
    {
        var index = values.BinarySearch(at);
        index = index < 0 ? forward ? ~index : ~index - 1 : forward ? index + 1 : index - 1;
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }
}
