namespace LensDocsStudio.Core.State;

public sealed record NavigationLocation(Guid DocumentId, string? Path, int CaretOffset, double VerticalOffset);

/// <summary>Bounded session navigation. A failed destination never advances the history.</summary>
public sealed class NavigationHistory(int capacity = 100)
{
    private readonly List<NavigationLocation> entries = [];
    private int index = -1;
    public bool CanGoBack => index > 0;
    public bool CanGoForward => index >= 0 && index < entries.Count - 1;
    public NavigationLocation? Back => CanGoBack ? entries[index - 1] : null;
    public NavigationLocation? Forward => CanGoForward ? entries[index + 1] : null;

    public void UpdateCurrent(NavigationLocation location)
    {
        if (index >= 0 && entries[index].DocumentId == location.DocumentId) entries[index] = location;
    }
    public void Visit(NavigationLocation location)
    {
        if (index >= 0 && entries[index] == location) return;
        if (CanGoForward) entries.RemoveRange(index + 1, entries.Count - index - 1);
        entries.Add(location);
        if (entries.Count > Math.Max(2, capacity)) entries.RemoveAt(0);
        index = entries.Count - 1;
    }
    public void CompleteMove(bool forward, NavigationLocation location)
    {
        if (forward ? !CanGoForward : !CanGoBack) throw new InvalidOperationException("The navigation destination is unavailable.");
        index += forward ? 1 : -1;
        entries[index] = location;
    }
}
