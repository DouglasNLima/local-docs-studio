using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;

namespace LensDocsStudio.LayoutExperiment;

internal sealed class LayoutAutomationPeer(LayoutSurface surface) : FrameworkElementAutomationPeer(surface), ITextProvider
{
    private Dictionary<int, NodePeer> peers = [];
    private Dictionary<int, SemanticNode> nodes = surface.Snapshot?.Model.Root.Descendants().ToDictionary(node => node.Id) ?? [];
    public LayoutSurface Surface => surface;
    public IRawElementProviderSimple Provider(AutomationPeer peer) => ProviderFromPeer(peer);
    protected override string GetClassNameCore() => "CompleteLayoutSurface";
    protected override string GetNameCore() => "Experimental Continuous document";
    protected override string GetAutomationIdCore() => "ExperimentalDocument";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
    protected override bool IsKeyboardFocusableCore() => true;
    protected override Rect GetBoundingRectangleCore() => surface.Scroller is { } scroll
        ? new Rect(scroll.PointToScreen(new Point()), scroll.PointToScreen(new Point(scroll.ActualWidth, scroll.ActualHeight))) : base.GetBoundingRectangleCore();
    public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.Text ? this : base.GetPattern(pattern);
    private NodePeer Peer(SemanticNode node)
    {
        if (!peers.TryGetValue(node.Id, out var peer)) peers.Add(node.Id, peer = new(this, surface.Snapshot!, node.Id));
        return peer;
    }
    protected override List<AutomationPeer>? GetChildrenCore() => surface.Snapshot is { } snapshot ? snapshot.Model.Root.Children.Select(node => (AutomationPeer)Peer(node)).ToList() : null;
    public void Changed()
    {
        peers = []; nodes = surface.Snapshot?.Model.Root.Descendants().ToDictionary(node => node.Id) ?? [];
        RaiseAutomationEvent(AutomationEvents.TextPatternOnTextChanged);
    }
    public void SelectionChanged() => RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
    public ITextRangeProvider DocumentRange => Range(0, surface.Snapshot?.Model.Text.Length ?? 0);
    public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;
    public ITextRangeProvider[] GetSelection() => [Range(surface.Start, surface.End)];
    public ITextRangeProvider[] GetVisibleRanges()
    {
        if (surface.Snapshot is not { } snapshot) return [];
        var top = surface.Scroller?.VerticalOffset ?? 0; var bottom = top + (surface.Scroller?.ViewportHeight ?? 300);
        return snapshot.Lines.Where(line => !line.Hidden && line.Top + line.Height >= top && line.Top <= bottom).Select(line => Range(line.Start, line.End)).ToArray();
    }
    public ITextRangeProvider RangeFromPoint(Point point) { var at = surface.Hit(surface.PointFromScreen(point)); return Range(at, at); }
    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple child)
    {
        var id = child.GetPropertyValue(AutomationElementIdentifiers.AutomationIdProperty.Id) as string;
        var snapshot = surface.Snapshot ?? throw new ElementNotAvailableException();
        if (id?.StartsWith($"Node-{snapshot.Version}-", StringComparison.Ordinal) == true && int.TryParse(id.Split('-')[^1], out var key) && nodes.TryGetValue(key, out var node)) return Range(node.Start, node.End);
        throw new ArgumentException("The element is not a child of the current document.");
    }
    internal TextRange Range(int start, int end) => new(this, surface.Snapshot, start, end);
    internal IRawElementProviderSimple[] Children(int start, int end) => nodes.Values.Where(node => node.Start >= start && node.End <= end && node.Kind is "List" or "Table" or "Image" or "Media" or "Link" or "Heading" or "Details")
        .Select(node => Provider(Peer(node))).ToArray();
    internal IRawElementProviderSimple Enclosing(int start, int end)
    {
        var node = nodes.Values.Where(node => node.Start <= start && node.End >= end).MinBy(node => node.End - node.Start);
        return node is null ? Provider(this) : Provider(Peer(node));
    }

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
    private sealed class NodePeer : SemanticPeer, ITableProvider, ITableItemProvider, IInvokeProvider, IExpandCollapseProvider
    {
        private readonly LayoutAutomationPeer root;
        private readonly WeakReference<LayoutSnapshot> identity;
        private readonly int id;
        public NodePeer(LayoutAutomationPeer root, LayoutSnapshot snapshot, int id) { this.root = root; identity = new(snapshot); this.id = id; }
        private LayoutSnapshot Current => identity.TryGetTarget(out var value) && ReferenceEquals(root.Surface.Snapshot, value) ? value : throw new ElementNotAvailableException();
        private SemanticNode Node { get { _ = Current; return root.nodes[id]; } }
        public override object? GetPattern(PatternInterface pattern) => pattern switch
        {
            PatternInterface.Grid or PatternInterface.Table when Node.Kind == "Table" => this,
            PatternInterface.GridItem or PatternInterface.TableItem when Node.Kind == "Cell" => this,
            PatternInterface.Invoke when Node.Kind == "Link" => this,
            PatternInterface.ExpandCollapse when Node.Kind == "Details" => this,
            _ => null
        };
        protected override string GetAutomationIdCore() => $"Node-{Current.Version}-{id}";
        protected override string GetClassNameCore() => "Semantic" + Node.Kind;
        protected override string GetNameCore() => Node.Kind == "List" ? Node.Ordered ? "Ordered list" : "Unordered list"
            : Node.Label.Length > 0 ? Node.Label : Current.Model.Text[Node.Start..Node.End].TrimEnd('\r', '\n');
        protected override AutomationControlType GetAutomationControlTypeCore() => Node.Kind switch
        {
            "List" => AutomationControlType.List, "ListItem" => AutomationControlType.ListItem,
            "Table" => AutomationControlType.Table, "Cell" => AutomationControlType.DataItem, "Row" => AutomationControlType.Custom,
            "Link" => AutomationControlType.Hyperlink, "Image" or "Media" => AutomationControlType.Image,
            "Details" => AutomationControlType.Group, _ => AutomationControlType.Text
        };
        protected override List<AutomationPeer> GetChildrenCore() => Node.Children.Select(child => (AutomationPeer)root.Peer(child)).ToList();
        protected override Rect GetBoundingRectangleCore()
        {
            _ = Current; var rectangles = root.Surface.Bounds(Node.Start, Node.End, true).ToArray();
            if (rectangles.Length == 0) return Rect.Empty;
            var bounds = rectangles.Aggregate(Rect.Empty, Rect.Union);
            return new(root.Surface.PointToScreen(bounds.TopLeft), root.Surface.PointToScreen(bounds.BottomRight));
        }
        protected override AutomationHeadingLevel GetHeadingLevelCore() => Node.Kind == "Heading" ? (AutomationHeadingLevel)Node.Level : AutomationHeadingLevel.None;
        protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        public int RowCount => Node.Children.Length;
        public int ColumnCount => Node.Children.Select(row => row.Children.Length).DefaultIfEmpty().Max();
        public RowOrColumnMajor RowOrColumnMajor => RowOrColumnMajor.RowMajor;
        public IRawElementProviderSimple GetItem(int row, int column) => root.Provider(root.Peer(Node.Children[row].Children[column]));
        public IRawElementProviderSimple[] GetRowHeaders() => [];
        public IRawElementProviderSimple[] GetColumnHeaders() => Node.Children.Where(row => row.Level == 1).SelectMany(row => row.Children).Select(cell => root.Provider(root.Peer(cell))).ToArray();
        private SemanticNode Table => root.nodes.Values.Single(node => node.Kind == "Table" && node.Start <= Node.Start && node.End >= Node.End);
        private SemanticNode RowNode => Table.Children.Single(row => row.Children.Any(cell => cell.Id == id));
        public int Row => Table.Children.IndexOf(RowNode);
        public int Column => RowNode.Children.IndexOf(Node);
        public int RowSpan => 1;
        public int ColumnSpan => 1;
        public IRawElementProviderSimple ContainingGrid => root.Provider(root.Peer(Table));
        public IRawElementProviderSimple[] GetRowHeaderItems() => [];
        public IRawElementProviderSimple[] GetColumnHeaderItems() => Table.Children.Where(row => row.Level == 1 && Column < row.Children.Length).Select(row => root.Provider(root.Peer(row.Children[Column]))).ToArray();
        public void Invoke() => root.Surface.Activate(Node);
        public ExpandCollapseState ExpandCollapseState => Node.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() { if (!Node.Expanded) root.Surface.Activate(Node); }
        public void Collapse() { if (Node.Expanded) root.Surface.Activate(Node); }
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
                TextUnit.Character => Current.Lines.SelectMany(line => line.Carets.Select(caret => caret.Offset)).Concat(model.Items.SelectMany(item => new[] { item.Start, item.Start + item.Length })),
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
            if (attributeId == TextPatternIdentifiers.IsItalicAttribute.Id) return Attribute(run => run.Italic);
            if (attributeId == TextPatternIdentifiers.FontNameAttribute.Id) return Attribute(run => run.Code ? "Consolas" : "Segoe UI");
            if (attributeId == TextPatternIdentifiers.FontSizeAttribute.Id) return Attribute(run => run.FontSize * .75);
            if (attributeId == TextPatternIdentifiers.FontWeightAttribute.Id)
            {
                var weights = Current.Model.Items.SelectMany(item => item.Runs).Where(run => run.Start + run.Text.Length > start && run.Start < Math.Max(end, start + 1)).Select(run => run.Weight).Distinct().ToArray();
                return weights.Length == 1 ? weights[0] : TextPatternIdentifiers.MixedAttributeValue;
            }
            return AutomationElementIdentifiers.NotSupported;
        }
        private object Attribute<T>(Func<StyledRun, T> select)
        {
            var values = Current.Model.Items.SelectMany(item => item.Runs).Where(run => run.Start + run.Text.Length > start && run.Start < Math.Max(end, start + 1)).Select(select).Distinct().ToArray();
            return values.Length == 1 ? values[0]! : TextPatternIdentifiers.MixedAttributeValue;
        }
        public double[] GetBoundingRectangles()
        {
            _ = Current;
            return peer.Surface.Bounds(start, end, true).SelectMany(rectangle =>
            { var point = peer.Surface.PointToScreen(rectangle.TopLeft); var end = peer.Surface.PointToScreen(rectangle.BottomRight); return new[] { point.X, point.Y, end.X - point.X, end.Y - point.Y }; }).ToArray();
        }
        public IRawElementProviderSimple GetEnclosingElement() { _ = Current; return peer.Enclosing(start, end); }
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
