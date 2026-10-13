using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using ContinuousViewer.Model;

namespace ContinuousViewer;

/// <summary>Full logical TextPattern, including ranges whose visuals have never been created.</summary>
public sealed class VirtualDocumentPeer(VirtualDocumentView owner) : FrameworkElementAutomationPeer(owner), ITextProvider
{
    public VirtualDocumentView View { get; } = owner;
    protected override string GetClassNameCore() => "ExperimentalContinuousDocument";
    protected override string GetNameCore() => "Experimental native Continuous document";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
    public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.Text ? this : base.GetPattern(pattern);
    public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;
    public ITextRangeProvider DocumentRange => Range(0, View.Document?.Text.Length ?? 0);
    public ITextRangeProvider[] GetSelection() => [Range(View.Selection?.Start ?? 0, View.Selection?.End ?? 0)];
    public ITextRangeProvider[] GetVisibleRanges() => View.Realised.Values.Where(p => p.Piece is not null && p.ActualHeight > 0)
        .Where(p => { var y = p.TranslatePoint(new Point(), View).Y; return y < View.ActualHeight && y + p.ActualHeight > 0; })
        .OrderBy(p => p.Piece!.Start).Select(p => (ITextRangeProvider)Range(p.Piece!.Start, p.Piece.End)).ToArray();
    public ITextRangeProvider RangeFromPoint(Point point) => Range(View.Hit(View.PointFromScreen(point)), View.Hit(View.PointFromScreen(point)));
    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement)
    {
        if (PeerFromProvider(childElement) is LogicalHeadingPeer heading) return Range(heading.Piece.Start, heading.Piece.End);
        throw new ArgumentException("The element is not a logical child of this document.", nameof(childElement));
    }
    public LogicalTextRange Range(int start, int end) => new(this, View.Document, start, end);
    public IRawElementProviderSimple Provider => ProviderFromPeer(this);
    protected override List<AutomationPeer>? GetChildrenCore() => View.Document?.Pieces.Where(p => p.Kind == PieceKind.Heading)
        .Select(p => (AutomationPeer)new LogicalHeadingPeer(this, p)).ToList();
}

public sealed class LogicalHeadingPeer(VirtualDocumentPeer parent, LogicalPiece piece) : FrameworkElementAutomationPeer(new TextBlock())
{
    public LogicalPiece Piece { get; } = piece;
    protected override string GetNameCore() => parent.View.Document!.Text[Piece.Start..Piece.End].TrimEnd();
    protected override string GetClassNameCore() => "HeadingLevel" + Piece.Level;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Header;
    protected override bool IsOffscreenCore() => !parent.View.Realised.ContainsKey(Piece.Id);
    protected override bool IsContentElementCore() => true;
    protected override Rect GetBoundingRectangleCore()
    {
        if (!parent.View.Realised.TryGetValue(Piece.Id, out var presenter)) return Rect.Empty;
        return new Rect(presenter.PointToScreen(new Point()), presenter.RenderSize);
    }
}

public sealed class LogicalTextRange(VirtualDocumentPeer peer, LogicalDocument? document, int start, int end) : ITextRangeProvider
{
    private int first = document?.Snap(start) ?? 0, last = document?.Snap(end, true) ?? 0;
    public int Start => first;
    public int End => last;
    private void Current()
    {
        if (document is null || peer.View.Document != document) throw new InvalidOperationException("The logical document has been replaced.");
    }
    private LogicalTextRange Other(ITextRangeProvider other)
    {
        Current(); if (other is not LogicalTextRange range || range.document != document) throw new ArgumentException("The range belongs to another document.");
        return range;
    }
    // Keep a named identity for comparisons; primary constructor variables cannot be accessed on another range.
    private readonly LogicalDocument? document = document;
    public ITextRangeProvider Clone() { Current(); return new LogicalTextRange(peer, document, first, last); }
    public bool Compare(ITextRangeProvider range) { var other = Other(range); return first == other.first && last == other.last; }
    public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
    {
        var other = Other(targetRange); return (endpoint == TextPatternRangeEndpoint.Start ? first : last)
            - (targetEndpoint == TextPatternRangeEndpoint.Start ? other.first : other.last);
    }
    private int[] Boundaries(TextUnit unit)
    {
        Current(); var text = document!.Text;
        if (unit == TextUnit.Character) return document.Graphemes;
        if (unit is TextUnit.Document or TextUnit.Page) return [0, text.Length];
        // Unsupported visual Line/Format units expand to the next supported logical Paragraph unit.
        return [0, .. Enumerable.Range(1, text.Length - 1).Where(i => unit == TextUnit.Word
            ? char.IsWhiteSpace(text[i - 1]) && !char.IsWhiteSpace(text[i]) : text[i - 1] == '\n'), text.Length];
    }
    public void ExpandToEnclosingUnit(TextUnit unit)
    {
        var boundaries = Boundaries(unit); var index = Array.BinarySearch(boundaries, first);
        index = index >= 0 ? Math.Min(index, boundaries.Length - 2) : Math.Max(0, ~index - 1);
        first = boundaries[index]; last = boundaries[index + 1];
    }
    public ITextRangeProvider? FindAttribute(int attributeId, object value, bool backward)
    { Current(); return null; }
    public ITextRangeProvider? FindText(string text, bool backward, bool ignoreCase)
    {
        Current(); if (string.IsNullOrEmpty(text)) throw new ArgumentException("Search text must not be empty.", nameof(text));
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var content = document!.Text[first..last]; var index = backward ? content.LastIndexOf(text, comparison) : content.IndexOf(text, comparison);
        return index < 0 ? null : new LogicalTextRange(peer, document, document.Snap(first + index), document.Snap(first + index + text.Length, true));
    }
    public object GetAttributeValue(int attributeId)
    {
        Current(); if (attributeId == TextPatternIdentifiers.IsReadOnlyAttribute.Id) return true;
        return AutomationElementIdentifiers.NotSupported;
    }
    public double[] GetBoundingRectangles()
    {
        Current(); var rectangles = new List<double>();
        foreach (var presenter in peer.View.Realised.Values.Where(p => p.Piece is not null && p.Piece.Start < last && p.Piece.End > first).OrderBy(p => p.Piece!.Start))
        {
            foreach (var original in presenter.RangeRectangles(Math.Max(first, presenter.Piece!.Start), Math.Min(last, presenter.Piece.End)))
            {
                var rect = original; rect.Intersect(new Rect(0, 0, peer.View.ActualWidth, peer.View.ActualHeight));
                if (rect.IsEmpty) continue; var screen = peer.View.PointToScreen(rect.TopLeft);
                rectangles.AddRange([screen.X, screen.Y, rect.Width, rect.Height]);
            }
        }
        return [.. rectangles];
    }
    public IRawElementProviderSimple GetEnclosingElement() { Current(); return peer.Provider; }
    public string GetText(int maxLength)
    {
        Current(); if (maxLength < -1) throw new ArgumentOutOfRangeException(nameof(maxLength));
        return document!.Text.Substring(first, maxLength == -1 ? last - first : Math.Min(last - first, maxLength));
    }
    public int Move(TextUnit unit, int count)
    {
        var boundaries = Boundaries(unit); var index = Array.BinarySearch(boundaries, first); index = index >= 0 ? index : Math.Max(0, ~index - 1);
        var target = Math.Clamp(index + count, 0, boundaries.Length - 2); var moved = target - index;
        var degenerate = first == last; first = boundaries[target]; last = degenerate ? first : boundaries[target + 1]; return moved;
    }
    public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
    {
        var boundaries = Boundaries(unit); var position = endpoint == TextPatternRangeEndpoint.Start ? first : last;
        var index = Array.BinarySearch(boundaries, position); index = index >= 0 ? index : Math.Clamp(~index - (count < 0 ? 0 : 1), 0, boundaries.Length - 1);
        var target = Math.Clamp(index + count, 0, boundaries.Length - 1); var moved = target - index;
        if (endpoint == TextPatternRangeEndpoint.Start) { first = boundaries[target]; last = Math.Max(first, last); }
        else { last = boundaries[target]; first = Math.Min(first, last); } return moved;
    }
    public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
    {
        var other = Other(targetRange); var value = targetEndpoint == TextPatternRangeEndpoint.Start ? other.first : other.last;
        if (endpoint == TextPatternRangeEndpoint.Start) { first = value; last = Math.Max(first, last); }
        else { last = value; first = Math.Min(first, last); }
    }
    public void Select() { Current(); peer.View.Selection!.Set(first, last); }
    public void AddToSelection() => throw new InvalidOperationException("This document supports one contiguous logical selection.");
    public void RemoveFromSelection() { Current(); peer.View.Selection!.Set(first, first); }
    public void ScrollIntoView(bool alignToTop)
    {
        Current(); if (document!.Pieces.Length != 0) _ = peer.View.NavigateAsync(document.At(alignToTop ? first : Math.Max(first, last - 1)).SourceStart);
    }
    public IRawElementProviderSimple[] GetChildren() { Current(); return []; }
}
