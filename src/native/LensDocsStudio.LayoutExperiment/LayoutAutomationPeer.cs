using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;

namespace LensDocsStudio.LayoutExperiment;

internal sealed class LayoutAutomationPeer(LayoutSurface surface) : FrameworkElementAutomationPeer(surface), ITextProvider
{
    private ListPeer? list;
    public LayoutSurface Surface => surface;
    public IRawElementProviderSimple Provider(AutomationPeer peer) => ProviderFromPeer(peer);
    protected override string GetClassNameCore() => "CompleteLayoutSurface";
    protected override string GetNameCore() => "Experimental Continuous document";
    protected override string GetAutomationIdCore() => "ExperimentalDocument";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
    protected override bool IsKeyboardFocusableCore() => true;
    protected override Rect GetBoundingRectangleCore() => surface.Scroller is { } scroll
        ? new Rect(scroll.PointToScreen(new Point()), scroll.RenderSize) : base.GetBoundingRectangleCore();
    public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.Text ? this : base.GetPattern(pattern);
    protected override List<AutomationPeer>? GetChildrenCore() => surface.Snapshot is { } snapshot ? [list ??= new(this, snapshot)] : null;
    public void Changed() { list = null; RaiseAutomationEvent(AutomationEvents.TextPatternOnTextChanged); }
    public void SelectionChanged() => RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
    public ITextRangeProvider DocumentRange => Range(0, surface.Snapshot?.Model.Text.Length ?? 0);
    public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;
    public ITextRangeProvider[] GetSelection() => [Range(surface.Start, surface.End)];
    public ITextRangeProvider[] GetVisibleRanges()
    {
        if (surface.Snapshot is not { } snapshot) return [];
        var top = surface.Scroller?.VerticalOffset ?? 0; var bottom = top + (surface.Scroller?.ViewportHeight ?? 300);
        return snapshot.Lines.Where(line => line.Top + line.Height >= top && line.Top <= bottom).Select(line => Range(line.Start, line.End)).ToArray();
    }
    public ITextRangeProvider RangeFromPoint(Point point) { var at = surface.Hit(surface.PointFromScreen(point)); return Range(at, at); }
    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple child)
    {
        var id = child.GetPropertyValue(AutomationElementIdentifiers.AutomationIdProperty.Id) as string;
        var snapshot = surface.Snapshot ?? throw new ElementNotAvailableException();
        if (id == $"List-{snapshot.Version}") return DocumentRange;
        if (id?.StartsWith($"Item-{snapshot.Version}-", StringComparison.Ordinal) == true && int.TryParse(id.Split('-')[^1], out var index) && index >= 0 && index < snapshot.Model.Items.Length)
        { var item = snapshot.Model.Items[index]; return Range(item.Start, item.Start + item.Length); }
        throw new ArgumentException("The element is not a child of the current document.");
    }
    internal TextRange Range(int start, int end) => new(this, surface.Snapshot, start, end);
    internal IRawElementProviderSimple[] Children(int start, int end) => GetChildrenCore() is { Count: > 0 } peers
        ? ((ListPeer)peers[0]).Items.Where(peer => peer.Item.Start >= start && peer.Item.Start + peer.Item.Length <= end).Select(Provider).ToArray() : [];

    private abstract class SemanticPeer : AutomationPeer
    {
        protected override List<AutomationPeer>? GetChildrenCore() => null;
        protected override bool IsPasswordCore() => false;
        protected override Point GetClickablePointCore() => new(double.NaN, double.NaN);
        protected override AutomationPeer? GetLabeledByCore() => null;
        protected override bool IsKeyboardFocusableCore() => false;
        protected override string GetItemTypeCore() => "";
        protected override string GetHelpTextCore() => "";
        protected override string GetAcceleratorKeyCore() => "";
        protected override bool IsEnabledCore() => true;
        protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
        protected override string GetItemStatusCore() => "";
        protected override bool IsRequiredForFormCore() => false;
        protected override void SetFocusCore() { }
        protected override string GetAccessKeyCore() => "";
        protected override bool HasKeyboardFocusCore() => false;
    }
    private sealed class ListPeer : SemanticPeer
    {
        private readonly LayoutAutomationPeer root; private readonly LayoutSnapshot snapshot;
        public List<ItemPeer> Items { get; }
        public ListPeer(LayoutAutomationPeer root, LayoutSnapshot snapshot)
        { this.root = root; this.snapshot = snapshot; Items = snapshot.Model.Items.Select((item, index) => new ItemPeer(root, snapshot, item, index)).ToList(); }
        public override object? GetPattern(PatternInterface patternInterface) => null;
        protected override string GetAutomationIdCore() => $"List-{snapshot.Version}";
        protected override string GetClassNameCore() => "SemanticList";
        protected override string GetNameCore() => snapshot.Model.Ordered ? "Ordered list" : "Unordered list";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
        protected override List<AutomationPeer> GetChildrenCore() => Items.Cast<AutomationPeer>().ToList();
        protected override Rect GetBoundingRectangleCore() => root.GetBoundingRectangle();
        protected override bool IsOffscreenCore() => !ReferenceEquals(root.Surface.Snapshot, snapshot);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }
    private sealed class ItemPeer(LayoutAutomationPeer root, LayoutSnapshot snapshot, Item item, int index) : SemanticPeer
    {
        public Item Item => item;
        public override object? GetPattern(PatternInterface patternInterface) => null;
        protected override string GetAutomationIdCore() => $"Item-{snapshot.Version}-{index}";
        protected override string GetClassNameCore() => "SemanticListItem";
        protected override string GetNameCore() => snapshot.Model.Text.Substring(item.Start, item.Length);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override Rect GetBoundingRectangleCore()
        {
            if (!ReferenceEquals(root.Surface.Snapshot, snapshot)) return Rect.Empty;
            var rectangles = root.Surface.Bounds(item.Start, item.Start + item.Length, true).ToArray();
            if (rectangles.Length == 0) return Rect.Empty;
            var bounds = rectangles.Aggregate(Rect.Empty, Rect.Union);
            return new(root.Surface.PointToScreen(bounds.TopLeft), bounds.Size);
        }
        protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    internal sealed class TextRange(LayoutAutomationPeer peer, LayoutSnapshot? snapshot, int start, int end) : ITextRangeProvider
    {
        private int start = start, end = end;
        private readonly WeakReference<LayoutSnapshot>? identity = snapshot is null ? null : new(snapshot);
        private LayoutSnapshot? Identity => identity is not null && identity.TryGetTarget(out var value) ? value : null;
        private LayoutSnapshot Current => Identity is { } current && ReferenceEquals(peer.Surface.Snapshot, current) ? current : throw new ElementNotAvailableException();
        public ITextRangeProvider Clone() => new TextRange(peer, Current, start, end);
        private TextRange Other(ITextRangeProvider other)
        { _ = Current; return other is TextRange range && ReferenceEquals(range.Identity, Identity) ? range : throw new ArgumentException("Ranges belong to different documents."); }
        public bool Compare(ITextRangeProvider range) { var other = Other(range); return start == other.start && end == other.end; }
        public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
        { var other = Other(targetRange); return (endpoint == TextPatternRangeEndpoint.Start ? start : end).CompareTo(targetEndpoint == TextPatternRangeEndpoint.Start ? other.start : other.end); }
        private int[] Units(TextUnit unit)
        {
            var model = Current.Model;
            IEnumerable<int> units = unit switch
            {
                TextUnit.Character => Current.Lines.SelectMany(line => line.Carets.Select(caret => caret.Offset)).Concat(model.Items.SelectMany(item => new[] { item.Start + item.Length, item.Start + item.Length + 2 })),
                TextUnit.Word => Enumerable.Range(0, model.Text.Length).Where(i => i == 0 || char.IsWhiteSpace(model.Text[i - 1]) != char.IsWhiteSpace(model.Text[i])),
                TextUnit.Line => Current.Lines.Select(line => line.Start),
                TextUnit.Paragraph => model.Items.Select(item => item.Start),
                TextUnit.Format => model.Items.SelectMany(item => item.Runs.Select(run => run.Start)),
                _ => [0, model.Text.Length]
            };
            return units.Append(0).Append(model.Text.Length).Distinct().Order().ToArray();
        }
        public void ExpandToEnclosingUnit(TextUnit unit)
        {
            var units = Units(unit); var at = Array.FindLastIndex(units, value => value <= start);
            start = units[Math.Clamp(at, 0, units.Length - 2)]; end = units[Math.Clamp(at + 1, 1, units.Length - 1)];
        }
        public ITextRangeProvider? FindText(string text, bool backward, bool ignoreCase)
        {
            var value = Current.Model.Text[start..end]; var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var at = backward ? value.LastIndexOf(text, comparison) : value.IndexOf(text, comparison);
            return at < 0 ? null : new TextRange(peer, Current, start + at, start + at + text.Length);
        }
        public ITextRangeProvider? FindAttribute(int attributeId, object value, bool backward)
        {
            _ = Current;
            if (attributeId != TextPatternIdentifiers.FontWeightAttribute.Id) return null;
            var runs = Current.Model.Items.SelectMany(item => item.Runs).Where(run => run.Start + run.Text.Length > start && run.Start < end);
            if (backward) runs = runs.Reverse();
            var found = runs.FirstOrDefault(run => Equals(value, run.Bold ? 700 : 400));
            return found is null ? null : new TextRange(peer, Current, Math.Max(start, found.Start), Math.Min(end, found.Start + found.Text.Length));
        }
        public object GetAttributeValue(int attributeId)
        {
            _ = Current;
            if (attributeId == TextPatternIdentifiers.IsReadOnlyAttribute.Id) return true;
            if (attributeId == TextPatternIdentifiers.FontNameAttribute.Id) return "Segoe UI";
            if (attributeId == TextPatternIdentifiers.FontSizeAttribute.Id) return 11.25d;
            if (attributeId == TextPatternIdentifiers.FontWeightAttribute.Id)
            {
                var weights = Current.Model.Items.SelectMany(item => item.Runs).Where(run => run.Start + run.Text.Length > start && run.Start <= end).Select(run => run.Bold ? 700 : 400).Distinct().ToArray();
                return weights.Length == 1 ? weights[0] : TextPatternIdentifiers.MixedAttributeValue;
            }
            return AutomationElementIdentifiers.NotSupported;
        }
        public double[] GetBoundingRectangles()
        {
            _ = Current;
            return peer.Surface.Bounds(start, end, true).SelectMany(rectangle =>
            { var point = peer.Surface.PointToScreen(rectangle.TopLeft); return new[] { point.X, point.Y, rectangle.Width, rectangle.Height }; }).ToArray();
        }
        public IRawElementProviderSimple GetEnclosingElement() { _ = Current; return peer.Provider(peer); }
        public string GetText(int maxLength)
        { var text = Current.Model.Text[start..end]; return maxLength < 0 || maxLength >= text.Length ? text : text[..maxLength]; }
        public int Move(TextUnit unit, int count)
        {
            var units = Units(unit); var index = Math.Max(0, Array.FindLastIndex(units, value => value <= start));
            var target = Math.Clamp(index + count, 0, units.Length - 2); var moved = target - index;
            var degenerate = start == end; start = units[target]; end = degenerate ? start : units[target + 1]; return moved;
        }
        public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
        {
            var units = Units(unit); var value = endpoint == TextPatternRangeEndpoint.Start ? start : end;
            var index = Math.Max(0, Array.FindLastIndex(units, at => at <= value));
            var target = Math.Clamp(index + count, 0, units.Length - 1);
            if (endpoint == TextPatternRangeEndpoint.Start) { start = units[target]; if (start > end) end = start; }
            else { end = units[target]; if (end < start) start = end; }
            return target - index;
        }
        public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
        {
            var other = Other(targetRange); var value = targetEndpoint == TextPatternRangeEndpoint.Start ? other.start : other.end;
            if (endpoint == TextPatternRangeEndpoint.Start) { start = value; if (start > end) end = start; }
            else { end = value; if (end < start) start = end; }
        }
        public void Select() { _ = Current; peer.Surface.Select(start, end); }
        public void AddToSelection() => Select();
        public void RemoveFromSelection() { _ = Current; peer.Surface.Select(end, end); }
        public void ScrollIntoView(bool alignToTop) { _ = Current; peer.Surface.Reveal(alignToTop ? start : end); }
        public IRawElementProviderSimple[] GetChildren() { _ = Current; return peer.Children(start, end); }
    }
}
