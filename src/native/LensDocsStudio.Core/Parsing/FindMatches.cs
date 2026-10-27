namespace LensDocsStudio.Core.Parsing;

/// <summary>Literal, non-overlapping, ordinal case-insensitive matches in one immutable pane snapshot.</summary>
public sealed record FindMatches(IReadOnlyList<int> Offsets, int Length, bool Complete)
{
    public const int MaximumCharacters = 8 * 1024 * 1024;
    public const int MaximumMatches = 100_000;
    public static FindMatches Collect(string text, string query, CancellationToken cancellationToken = default)
    {
        if (text.Length > MaximumCharacters || query.Length > 1024) return new([], query.Length, false);
        var matches = new List<int>();
        if (query.Length == 0 || query.Contains('\0')) return new(matches, query.Length, true);
        for (var offset = 0; offset <= text.Length - query.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = text.IndexOf(query, offset, StringComparison.OrdinalIgnoreCase);
            if (found < 0) break;
            if (matches.Count == MaximumMatches) return new([], query.Length, false);
            matches.Add(found); offset = found + query.Length;
        }
        return new(matches.AsReadOnly(), query.Length, true);
    }
    public int Move(int current, int direction) => Offsets.Count == 0 ? -1
        : current < 0 ? (direction < 0 ? Offsets.Count - 1 : 0)
        : (current + (direction < 0 ? -1 : 1) + Offsets.Count) % Offsets.Count;
    public int AtOrAfter(int offset)
    {
        for (var i = 0; i < Offsets.Count; i++) if (Offsets[i] >= offset) return i;
        return Offsets.Count == 0 ? -1 : 0;
    }
}
