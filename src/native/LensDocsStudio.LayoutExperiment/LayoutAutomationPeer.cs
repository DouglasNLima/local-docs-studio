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
    internal static int TextEnd(LayoutSnapshot? snapshot) => snapshot?.Model.Items.LastOrDefault() is { } last ? last.Start + last.Length : 0;
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
        ResetChildrenCache(); InvalidatePeer();
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
        var native = surface.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(button => button.IsEnabled && AutomationProperties.GetAutomationId(button) == id);
        if (native?.Tag is ControlBox box && nodes.TryGetValue(box.Node, out var media)) return Range(media.Start, media.End);
        throw new ArgumentException("The element is not a child of the current document.");
    }
    internal TextRange Range(int start, int end) => new(this, surface.Snapshot, Math.Min(start, TextEnd(surface.Snapshot)), Math.Min(end, TextEnd(surface.Snapshot)));
    internal IRawElementProviderSimple[] Children(int start, int end)
    {
        if (start == end) return [];
        var enclosing = nodes.Values.Where(node => node.Start <= start && node.End >= end).MinBy(node => node.End - node.Start);
        var children = nodes.Values.Where(node => node.Start < end && node.End > start && node.Id != enclosing?.Id
            && (enclosing is null || node.Start >= enclosing.Start && node.End <= enclosing.End)
            && node.Kind is "List" or "Table" or "Image" or "Media" or "Link" or "Heading" or "Details").ToArray();
        return children.Where(node => !children.Any(parent => parent.Id != node.Id && parent.Start <= node.Start && parent.End >= node.End))
            .Select(node => Provider(Peer(node))).ToArray();
    }
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
        private readonly WeakReference<LayoutAutomationPeer> rootIdentity;
        private LayoutAutomationPeer root => rootIdentity.TryGetTarget(out var value) ? value : throw new ElementNotAvailableException();
        private readonly WeakReference<LayoutSnapshot> identity;
        private readonly int id;
        public NodePeer(LayoutAutomationPeer root, LayoutSnapshot snapshot, int id) { rootIdentity = new(root); identity = new(snapshot); this.id = id; }
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
        protected override List<AutomationPeer> GetChildrenCore() => Node.Children.Select(child => (AutomationPeer)root.Peer(child))
            .Concat(root.Surface.Children.OfType<System.Windows.Controls.Button>().Where(button => button.Tag is ControlBox box && box.Node == id).Select(button => (AutomationPeer)UIElementAutomationPeer.CreatePeerForElement(button))).ToList();
        protected override Rect GetBoundingRectangleCore()
        {
            var current = Current;
            if (Node.Kind is "Table" or "Row" or "Cell" or "Details" or "List" or "ListItem" or "Quote" or "Media"
                && current.NodeBounds.TryGetValue(id, out var structure))
            {
                var scroll = root.Surface.Scroller;
                structure.Intersect(new Rect(scroll?.HorizontalOffset ?? 0, scroll?.VerticalOffset ?? 0, scroll?.ViewportWidth ?? 640, scroll?.ViewportHeight ?? 300));
                return structure.IsEmpty ? Rect.Empty : new(root.Surface.PointToScreen(structure.TopLeft), root.Surface.PointToScreen(structure.BottomRight));
            }
            var rectangles = root.Surface.Bounds(Node.Start, Node.End, true).ToArray();
            if (rectangles.Length == 0) return Rect.Empty;
            var bounds = rectangles.Aggregate(Rect.Empty, Rect.Union);
            return new(root.Surface.PointToScreen(bounds.TopLeft), root.Surface.PointToScreen(bounds.BottomRight));
        }
        protected override AutomationHeadingLevel GetHeadingLevelCore() => Node.Kind == "Heading" ? (AutomationHeadingLevel)Node.Level : AutomationHeadingLevel.None;
        protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        public int RowCount => Node.Children.Length;
        public int ColumnCount => Math.Max(Node.Columns, Node.Children.SelectMany(row => row.Children).Select(cell => cell.Column + cell.ColumnSpan).DefaultIfEmpty().Max());
        public RowOrColumnMajor RowOrColumnMajor => RowOrColumnMajor.RowMajor;
        public IRawElementProviderSimple GetItem(int row, int column)
        {
            if (row < 0 || row >= RowCount || column < 0 || column >= ColumnCount) throw new ArgumentOutOfRangeException();
            for (var at = row; at >= 0; at--)
                if (Node.Children[at].Children.FirstOrDefault(cell => at + cell.RowSpan > row && column >= cell.Column && column < cell.Column + cell.ColumnSpan) is { } cell)
                    return root.Provider(root.Peer(cell));
            throw new ArgumentException("The requested table position has no cell.");
        }
        public IRawElementProviderSimple[] GetRowHeaders() => [];
        public IRawElementProviderSimple[] GetColumnHeaders() => Node.Children.Where(row => row.Level == 1).SelectMany(row => row.Children).Select(cell => root.Provider(root.Peer(cell))).ToArray();
        private SemanticNode Table => root.nodes.Values.Where(node => node.Kind == "Table" && node.Children.Any(row => row.Children.Any(cell => cell.Id == id))).MinBy(node => node.End - node.Start)!;
        private SemanticNode RowNode => Table.Children.Single(row => row.Children.Any(cell => cell.Id == id));
        public int Row => Table.Children.IndexOf(RowNode);
        public int Column => Node.Column;
        public int RowSpan => Node.RowSpan;
        public int ColumnSpan => Node.ColumnSpan;
        public IRawElementProviderSimple ContainingGrid => root.Provider(root.Peer(Table));
        public IRawElementProviderSimple[] GetRowHeaderItems() => [];
        public IRawElementProviderSimple[] GetColumnHeaderItems() => Table.Children.Where(row => row.Level == 1).SelectMany(row => row.Children).Where(cell => cell.Column <= Column && cell.Column + cell.ColumnSpan > Column).Select(cell => root.Provider(root.Peer(cell))).ToArray();
        public void Invoke() => root.Surface.Activate(Node);
        public ExpandCollapseState ExpandCollapseState => Node.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() { if (!Node.Expanded) root.Surface.Activate(Node); }
        public void Collapse() { if (Node.Expanded) root.Surface.Activate(Node); }
    }

    internal sealed class TextRange(LayoutAutomationPeer peer, LayoutSnapshot? snapshot, int start, int end) : ITextRangeProvider
    {
        private readonly WeakReference<LayoutAutomationPeer> owner = new(peer);
        private LayoutAutomationPeer Peer => owner.TryGetTarget(out var value) ? value : throw new ElementNotAvailableException();
        private int start = start, end = end;
        private bool formatBackward = true;
        // A styled-run edge and its text insertion point can share one text/source
        // offset. Preserve that real semantic boundary in endpoint comparison.
        private int startOrder = BeforeRun(snapshot, start);
        private int endOrder = start == end ? BeforeRun(snapshot, start) : 0;
        private static int BeforeRun(LayoutSnapshot? snapshot, int at) => snapshot?.Model.Items.SelectMany(item => item.Runs).Any(run => run.Start == at) == true ? -1 : 0;
        private void OrderEndpoints(TextPatternRangeEndpoint changed)
        {
            if ((start, startOrder).CompareTo((end, endOrder)) <= 0) return;
            if (changed == TextPatternRangeEndpoint.Start) { end = start; endOrder = startOrder; }
            else { start = end; startOrder = endOrder; }
        }
        private readonly WeakReference<LayoutSnapshot>? identity = snapshot is null ? null : new(snapshot);
        private LayoutSnapshot? Identity => identity is not null && identity.TryGetTarget(out var value) ? value : null;
        private LayoutSnapshot Current => Identity is { } current && ReferenceEquals(Peer.Surface.Snapshot, current) ? current : throw new ElementNotAvailableException();
        public ITextRangeProvider Clone() => new TextRange(Peer, Current, start, end) { formatBackward = formatBackward, startOrder = startOrder, endOrder = endOrder };
        private TextRange Other(ITextRangeProvider other)
        { _ = Current; return other is TextRange range && ReferenceEquals(range.Identity, Identity) ? range : throw new ArgumentException("Ranges belong to different documents."); }
        public bool Compare(ITextRangeProvider range) { var other = Other(range); return start == other.start && end == other.end && startOrder == other.startOrder && endOrder == other.endOrder; }
        public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
        {
            var other = Other(targetRange); var value = endpoint == TextPatternRangeEndpoint.Start ? (start, startOrder) : (end, endOrder);
            return value.CompareTo(targetEndpoint == TextPatternRangeEndpoint.Start ? (other.start, other.startOrder) : (other.end, other.endOrder));
        }
        private int[] Units(TextUnit unit)
        {
            var model = Current.Model;
            IEnumerable<int> units = unit switch
            {
                TextUnit.Character => Current.Indices.Characters,
                TextUnit.Word => Current.Indices.Words,
                TextUnit.Line => Current.Lines.Select(line => line.Start),
                TextUnit.Paragraph => model.Items.Select(item => item.Start),
                TextUnit.Format => model.Items.SelectMany(item => item.Runs.Select(run => run.Start)),
                _ => [0, model.Text.Length]
            };
            var limit = TextEnd(Current);
            return units.Append(0).Append(limit).Where(at => at <= limit).Distinct().Order().ToArray();
        }
        public void ExpandToEnclosingUnit(TextUnit unit)
        {
            end = Math.Min(TextEnd(Current), SnapshotIndices.Adjacent(Current.Indices.Characters, start, true));
            Expand(unit, true, true);
        }
        private (int Start, int End)[] Spans(TextUnit unit)
        {
            if (unit == TextUnit.Format)
            {
                var runs = Current.Model.Items.SelectMany(item => item.Runs).ToArray();
                return runs.Select((run, index) => (run.Start, index + 1 < runs.Length ? runs[index + 1].Start : TextEnd(Current))).ToArray();
            }
            return unit switch
            {
                TextUnit.Line => Current.Lines.Select(line => (line.Start, line.End)).ToArray(),
                TextUnit.Paragraph => Current.Model.Items.Select(item => (item.Start, item.Start + item.Length)).ToArray(),
                _ => []
            };
        }
        private void Expand(TextUnit unit, bool expandStart, bool expandEnd)
        {
            if (expandStart) startOrder = 0; if (expandEnd) endOrder = 0;
            var limit = TextEnd(Current);
            if (unit is TextUnit.Page or TextUnit.Document) { if (expandStart) start = 0; if (expandEnd) end = limit; return; }
            var spans = Spans(unit);
            if (spans.Length > 0)
            {
                var first = Array.FindLastIndex(spans, span => span.Start <= start);
                if (unit == TextUnit.Format && formatBackward && first > 0 && spans[first].Start == start && !Current.Model.Items.Any(item => item.Start == start)) first--;
                first = Math.Max(0, first);
                var last = Array.FindIndex(spans, span => span.End >= end); if (last < 0) last = spans.Length - 1;
                var sameLine = unit == TextUnit.Line && end >= spans[first].Start && end <= spans[first].End;
                if (expandStart) start = spans[first].Start;
                if (expandEnd || sameLine) end = spans[sameLine ? first : last].End;
                formatBackward = false; return;
            }
            var units = Units(unit);
            if (units.Length < 2) { start = end = 0; return; }
            if (expandStart) start = units[Math.Max(0, Array.FindLastIndex(units, value => value <= start))];
            if (expandEnd) { var next = Array.FindIndex(units, value => value >= end); end = next < 0 ? limit : units[next]; }
            if (unit == TextUnit.Word && expandEnd && Current.Model.Items.FirstOrDefault(item => start >= item.Start && start < item.Start + item.Length) is { } paragraph)
                end = Math.Min(end, paragraph.Start + paragraph.Length);
        }
        public ITextRangeProvider? FindText(string text, bool backward, bool ignoreCase)
        {
            var value = Current.Model.Text[start..end]; var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var at = backward ? value.LastIndexOf(text, comparison) : value.IndexOf(text, comparison);
            return at < 0 ? null : new TextRange(Peer, Current, start + at, start + at + text.Length);
        }
        public ITextRangeProvider? FindAttribute(int attributeId, object value, bool backward)
        {
            _ = Current;
            if (attributeId == TextPatternIdentifiers.IsReadOnlyAttribute.Id || attributeId == TextPatternIdentifiers.IsHiddenAttribute.Id || attributeId == TextPatternIdentifiers.CultureAttribute.Id)
                return AttributeEquals(value, GetAttributeValue(attributeId)) ? Clone() : null;
            var groups = new List<(int Start, int End)>();
            foreach (var run in Current.Model.Items.SelectMany(item => item.Runs).Where(run => run.Start + run.Text.Length > start && run.Start < end))
            {
                if (!AttributeEquals(value, RunAttribute(run, attributeId))) { groups.Add((-1, -1)); continue; }
                var begin = Math.Max(start, run.Start); var finish = Math.Min(end, run.Start + run.Text.Length);
                if (groups.Count > 0 && groups[^1].Start >= 0 && begin >= groups[^1].End
                    && Current.Model.Text.AsSpan(groups[^1].End, begin - groups[^1].End).IndexOfAnyExcept('\r', '\n', '\t') < 0)
                    groups[^1] = (groups[^1].Start, finish);
                else groups.Add((begin, finish));
            }
            var found = backward ? groups.LastOrDefault(group => group.Item1 >= 0, (-1, -1)) : groups.FirstOrDefault(group => group.Item1 >= 0, (-1, -1));
            return found.Item1 < 0 ? null : new TextRange(Peer, Current, found.Item1, found.Item2);
        }
        private static bool AttributeEquals(object left, object right) => Equals(left is Enum ? Convert.ToInt32(left) : left, right is Enum ? Convert.ToInt32(right) : right);
        public object GetAttributeValue(int attributeId)
        {
            _ = Current;
            if (attributeId == TextPatternIdentifiers.IsReadOnlyAttribute.Id) return true;
            if (attributeId == TextPatternIdentifiers.CultureAttribute.Id) return TextCulture.Value.LCID;
            if (attributeId == TextPatternIdentifiers.IsHiddenAttribute.Id) return false;
            return Attribute(run => RunAttribute(run, attributeId));
        }
        private object RunAttribute(StyledRun run, int attributeId)
        {
            if (attributeId == TextPatternIdentifiers.IsItalicAttribute.Id) return run.Italic;
            if (attributeId == TextPatternIdentifiers.FontNameAttribute.Id) return run.Code ? "Consolas" : "Segoe UI";
            if (attributeId == TextPatternIdentifiers.FontSizeAttribute.Id) return run.FontSize * .75;
            if (attributeId == TextPatternIdentifiers.FontWeightAttribute.Id) return run.Weight;
            if (attributeId == TextPatternIdentifiers.UnderlineStyleAttribute.Id) return run.Link is not null || run.Emphasis.HasFlag(LensDocsStudio.Core.Parsing.MarkdownEmphasis.Underline) ? TextDecorationLineStyle.Single : TextDecorationLineStyle.None;
            if (attributeId == TextPatternIdentifiers.StrikethroughStyleAttribute.Id) return run.Emphasis.HasFlag(LensDocsStudio.Core.Parsing.MarkdownEmphasis.Strike) ? TextDecorationLineStyle.Single : TextDecorationLineStyle.None;
            if (attributeId == TextPatternIdentifiers.IsSubscriptAttribute.Id) return run.Emphasis.HasFlag(LensDocsStudio.Core.Parsing.MarkdownEmphasis.Subscript);
            if (attributeId == TextPatternIdentifiers.IsSuperscriptAttribute.Id) return run.Emphasis.HasFlag(LensDocsStudio.Core.Parsing.MarkdownEmphasis.Superscript);
            if (attributeId == TextPatternIdentifiers.ForegroundColorAttribute.Id)
            {
                var appearance = Current.Appearance; var hc = appearance.StartsWith("HC:", StringComparison.Ordinal);
                var colour = run.Link is not null ? ExperimentAppearance.IsDark(appearance) ? "#87CEFA" : "#4169E1" : hc ? ExperimentAppearance.Slot(appearance, 0) : run.Colour is not null ? "#" + run.Colour : appearance == "Dark" ? "#E8EAED" : "#202124";
                var value = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colour);
                return value.R << 16 | value.G << 8 | value.B;
            }
            if (attributeId == TextPatternIdentifiers.BackgroundColorAttribute.Id)
            {
                var appearance = Current.Appearance; var hc = appearance.StartsWith("HC:", StringComparison.Ordinal);
                var surface = run.Code || run.Emphasis.HasFlag(LensDocsStudio.Core.Parsing.MarkdownEmphasis.Highlight);
                if (!surface) return 0; // Native text's transparent Background, separate from the canvas paint.
                var colour = hc ? ExperimentAppearance.Slot(appearance, 1) : appearance == "Dark" ? "#292B2F" : "#F3F4F6";
                var value = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colour); return value.R << 16 | value.G << 8 | value.B;
            }
            return AutomationElementIdentifiers.NotSupported;
        }

        private object Attribute<T>(Func<StyledRun, T> select)
        {
            var values = Current.Model.Items.SelectMany(item => item.Runs).Where(run => run.Start + run.Text.Length > start && run.Start < Math.Max(end, start + 1)).Select(select).Distinct().ToArray();
            if (values.Length == 0 && start == end && Current.Model.Items.SelectMany(item => item.Runs).LastOrDefault(run => run.Start <= start) is { } previous) return select(previous)!;
            return values.Length == 1 ? values[0]! : TextPatternIdentifiers.MixedAttributeValue;
        }
        public double[] GetBoundingRectangles()
        {
            _ = Current;
            return Peer.Surface.Bounds(start, end, true).SelectMany(rectangle =>
            { var point = Peer.Surface.PointToScreen(rectangle.TopLeft); var end = Peer.Surface.PointToScreen(rectangle.BottomRight); return new[] { point.X, point.Y, end.X - point.X, end.Y - point.Y }; }).ToArray();
        }
        public IRawElementProviderSimple GetEnclosingElement() { _ = Current; return Peer.Enclosing(start, end); }
        public string GetText(int maxLength)
        { var text = Current.Model.Text[start..end]; return maxLength < 0 || maxLength >= text.Length ? text : text[..maxLength]; }
        public int Move(TextUnit unit, int count)
        {
            if (unit != TextUnit.Paragraph) Expand(unit, true, true);
            if (count == 0) { _ = Current; return 0; }
            var units = Units(unit); if (units.Length < 2) return 0;
            var index = Math.Min(units.Length - 2, Math.Max(0, Array.FindLastIndex(units, value => value <= start)));
            var target = Math.Clamp(index + count, 0, units.Length - 2); var moved = target - index;
            if (moved == 0 && count > 0 && unit is TextUnit.Line or TextUnit.Paragraph && Current.Model.Items.LastOrDefault() is { } last && start < last.Start + last.Length)
            { Expand(unit, true, true); return 1; }
            if (moved == 0) return 0;
            start = units[target]; end = Math.Min(TextEnd(Current), SnapshotIndices.Adjacent(Current.Indices.Characters, start, true)); formatBackward = false;
            Expand(unit, true, true);
            return moved;
        }
        public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
        {
            if (count == 0) { _ = Current; return 0; }
            var units = Units(unit); var value = endpoint == TextPatternRangeEndpoint.Start ? start : end;
            if (unit == TextUnit.Format && count > 0 && formatBackward && Current.Model.Items.SelectMany(item => item.Runs).Any(run => run.Start == value)
                && (endpoint == TextPatternRangeEndpoint.Start || start == end && !Current.Model.Items.Any(item => item.Start == value)))
            {
                if (endpoint == TextPatternRangeEndpoint.Start) startOrder = 0;
                formatBackward = false;
                OrderEndpoints(endpoint);
                if (count == 1) return 1;
                return 1 + MoveEndpointByUnit(endpoint, unit, count - 1);
            }
            if (unit is TextUnit.Document or TextUnit.Page)
            {
                var boundary = unit == TextUnit.Page ? count < 0 ? 0 : TextEnd(Current) : endpoint == TextPatternRangeEndpoint.Start ? 0 : TextEnd(Current);
                if (boundary == value && (unit != TextUnit.Document || endpoint == TextPatternRangeEndpoint.Start && count < 0 || endpoint == TextPatternRangeEndpoint.End && count > 0)) return 0;
                if (endpoint == TextPatternRangeEndpoint.Start) start = boundary; else end = boundary;
                if (endpoint == TextPatternRangeEndpoint.Start) startOrder = 0; else endOrder = 0;
                OrderEndpoints(endpoint);
                return Math.Sign(count);
            }
            if (unit is TextUnit.Line or TextUnit.Paragraph)
            {
                var spans = Spans(unit); if (spans.Length == 0) return 0;
                var item = Math.Max(0, Array.FindLastIndex(spans, span => span.Start <= value));
                var targetItem = item; var boundary = value; var moved = 0;
                for (var step = 0; step < Math.Abs(count); step++)
                {
                    var isStart = endpoint == TextPatternRangeEndpoint.Start;
                    var at = count > 0 ? isStart ? targetItem + 1 : boundary < spans[targetItem].End ? targetItem : targetItem + 1
                        : isStart ? boundary > spans[targetItem].Start ? targetItem : targetItem - 1 : targetItem - 1;
                    if (at >= spans.Length && isStart && count > 0 && boundary < spans[^1].End)
                    { boundary = spans[^1].End; moved += Math.Sign(count); break; }
                    if (at < 0 || at >= spans.Length) break;
                    var next = isStart ? spans[at].Start : spans[at].End;
                    if (next == boundary) break; targetItem = at; boundary = next; moved += Math.Sign(count);
                }
                if (endpoint == TextPatternRangeEndpoint.Start) start = boundary; else end = boundary;
                formatBackward = false;
                if (moved != 0) Expand(unit, endpoint == TextPatternRangeEndpoint.Start, endpoint == TextPatternRangeEndpoint.End);
                OrderEndpoints(endpoint);
                return moved;
            }
            var index = count < 0 ? Array.FindIndex(units, at => at >= value) : Array.FindLastIndex(units, at => at <= value);
            index = Math.Max(0, index);
            var lastBoundary = unit == TextUnit.Format && endpoint == TextPatternRangeEndpoint.Start ? Math.Max(0, units.Length - 2) : units.Length - 1;
            var target = Math.Clamp(index + count, 0, lastBoundary);
            if (endpoint == TextPatternRangeEndpoint.Start) start = units[target];
            else end = units[target];
            if (endpoint == TextPatternRangeEndpoint.Start) startOrder = 0; else endOrder = unit == TextUnit.Format ? BeforeRun(Current, end) : 0;
            OrderEndpoints(endpoint);
            formatBackward = false;
            return target - index;
        }
        public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
        {
            var other = Other(targetRange); var value = targetEndpoint == TextPatternRangeEndpoint.Start ? other.start : other.end;
            var order = targetEndpoint == TextPatternRangeEndpoint.Start ? other.startOrder : other.endOrder;
            if (endpoint == TextPatternRangeEndpoint.Start) { start = value; startOrder = order; if ((start, startOrder).CompareTo((end, endOrder)) > 0) { end = start; endOrder = startOrder; } }
            else { end = value; endOrder = order; if ((end, endOrder).CompareTo((start, startOrder)) < 0) { start = end; startOrder = endOrder; } }
        }
        public void Select() { _ = Current; Peer.Surface.Select(start, end); }
        public void AddToSelection() { _ = Current; throw new InvalidOperationException("Only one text selection is supported."); }
        public void RemoveFromSelection() { _ = Current; throw new InvalidOperationException("Only one text selection is supported."); }
        public void ScrollIntoView(bool alignToTop) { _ = Current; Peer.Surface.ScrollRange(start, end, alignToTop); }
        public IRawElementProviderSimple[] GetChildren() { _ = Current; return Peer.Children(start, end); }
    }
}
