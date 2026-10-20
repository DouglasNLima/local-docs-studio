using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Mindmaps;

public sealed record MindmapSourceNode(string Identity, string Label, string Indentation, string? Parent, int Depth,
    int LabelStart, int LabelLength, string Shape, string? Protection);
public sealed record MindmapSourceModel(string HeaderIdentity, IReadOnlyList<MindmapSourceNode> Nodes, IReadOnlyList<string> Errors, IReadOnlyList<string> Protected);

public sealed class MindmapVisualDraft : SourceDraft<MindmapSourceModel>
{
    public const string Starter = "mindmap\n  root((Documentation))\n    Guides\n      Getting started\n      Authoring\n    Reference\n      Commands\n      Exports\n";
    private static readonly (string Shape, Regex Pattern)[] Shapes = [
        ("circle", SourceText.Pattern(@"\(\((?<label>.+?)\)\)$")), ("bang", SourceText.Pattern(@"\)\)(?<label>.+?)\(\($")),
        ("hexagon", SourceText.Pattern(@"\{\{(?<label>.+?)\}\}$")), ("cloud", SourceText.Pattern(@"(?<!\))\)(?<label>.+?)\($")),
        ("square", SourceText.Pattern(@"\[(?<label>.+?)\]$")), ("rounded", SourceText.Pattern(@"(?<!\()\((?<label>[^()]+?)\)$")) ];
    public static bool IsMindmap(string source) => SourceText.Header(source).Equals("mindmap", StringComparison.OrdinalIgnoreCase);
    public MindmapVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override MindmapSourceModel Scan(SourceText text)
    {
        var nodes = new List<MindmapSourceNode>(); var errors = new List<string>(); var protect = new List<string>(); string header = ""; var stack = new List<MindmapSourceNode>();
        foreach (var line in text.Lines)
        {
            if (line.Content.Length == 0 || line.Comment) continue;
            if (line.InitDirective) { protect.Add(line.Key); continue; }
            if (header.Length == 0) { header = line.Key; if (!line.Content.Equals("mindmap", StringComparison.OrdinalIgnoreCase)) errors.Add("Mind map requires a mindmap header."); continue; }
            var raw = line.Body; var indent = line.Indent; var content = line.Content;
            while (stack.Count > 0 && stack[^1].Indentation.Length >= indent.Length) stack.RemoveAt(stack.Count - 1);
            var parent = stack.LastOrDefault();
            if (parent is null && nodes.Count > 0) errors.Add("A Mind map must have exactly one root. The pinned parser would omit a second root and its later branches.");
            string? protection = null; var labelStart = raw.IndexOf(content, StringComparison.Ordinal); var label = content; var shape = "default";
            foreach (var form in Shapes)
            {
                var match = form.Pattern.Match(content); if (!match.Success) continue;
                label = match.Groups["label"].Value; labelStart += match.Groups["label"].Index; shape = form.Shape;
                var prefix = content[..match.Index];
                if (prefix.Length > 0 && !SourceText.Pattern(@"^[\p{L}_][\p{L}\p{M}\p{N}_-]*$").IsMatch(prefix)) protection = "The shaped node prefix cannot be proved to be a single ID. Its complete source is preserved.";
                break;
            }
            if (content.Contains("::", StringComparison.Ordinal) || content.StartsWith("style ", StringComparison.Ordinal) || content.StartsWith("classDef ", StringComparison.Ordinal))
                protection = "Icons/classes/styles or suffix metadata are not represented by the pinned Mind map parser. This source occurrence and structural attachment are protected.";
            if (shape == "default" && content.Any(character => "[](){}".Contains(character))) protection = "Unrecognised shape delimiters protect this node's label and attachment.";
            if (indent.Contains(' ') && indent.Contains('\t')) protection = "Mixed tabs and spaces protect local structural edits. Use source to establish unambiguous indentation.";
            var node = new MindmapSourceNode(line.Key, label, indent, parent?.Identity, stack.Count, labelStart, label.Length, shape, protection);
            nodes.Add(node); stack.Add(node);
        }
        if (nodes.Count == 0) errors.Add("Mind map requires one root; add it in source before applying.");
        if (nodes.Count > 500) errors.Add("Mind map exceeds the 500-element rendering budget.");
        return new(header, nodes, errors, protect);
    }
    protected override bool Contains(MindmapSourceModel model, string identity) => model.Nodes.Any(node => node.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, MindmapSourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "Mind map rendering does not match current source.");
        var nodes = XDocument.Parse(rendered.Svg!).Descendants().Where(element => ((string?)element.Attribute("class") ?? "").Split(' ').Contains("mindmap-node")).ToArray();
        var ordered = CanonicalOrder(model);
        // Pinned LayoutBalanced emits root first, then each contiguous branch in post-order.
        // This is structural occurrence correspondence, independent of duplicate label text.
        if (nodes.Length != model.Nodes.Count) throw new ArgumentException("Canonical Mind map omitted source nodes. Apply is protected.");
        for (var index = 0; index < nodes.Length; index++)
            if (!DisplayLabelMatches(ordered[index].Label, (string?)nodes[index].Attribute("data-label") ?? "") || (string?)nodes[index].Attribute("data-depth") != ordered[index].Depth.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new ArgumentException("Canonical Mind map node occurrence/depth does not match the complete source tree. Apply is protected.");
    }
    public static bool DisplayLabelMatches(string source, string rendered) => source == rendered || rendered.Contains('\n') && rendered.Replace('\n', ' ') == string.Join(' ', source.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    public static IReadOnlyList<MindmapSourceNode> CanonicalOrder(MindmapSourceModel model)
    {
        if (model.Nodes.Count == 0) return [];
        var result = new List<MindmapSourceNode> { model.Nodes[0] };
        void PostOrder(MindmapSourceNode node) { foreach (var child in model.Nodes.Where(item => item.Parent == node.Identity)) PostOrder(child); result.Add(node); }
        foreach (var child in model.Nodes.Where(item => item.Parent == model.Nodes[0].Identity)) PostOrder(child);
        return result;
    }
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceText.Label(value); var node = Node(model, identity); Editable(node);
        var changed = text.Replace(text.Line(identity).Replace(node.LabelStart, node.LabelLength, value));
        var parsed = Scan(changed).Nodes.Single(item => item.Identity == identity);
        if (parsed.Label != value || parsed.Shape != node.Shape || parsed.Protection is not null) throw new ArgumentException("This label changes shape or metadata syntax. Use plain text inside the existing recognised delimiters.");
        return changed;
    }, token);
    public Task<SourceAuthoringResult> AddAsync(string identity, bool child, string value = "New branch", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceText.Label(value); var node = Node(model, identity); Editable(node); var subtree = Subtree(model, node); Structural(text, model, subtree);
        if (!child && node.Parent is null) throw new ArgumentException("The root cannot have a sibling. Add a child branch.");
        var indent = child ? ChildIndent(model, node) : node.Indentation;
        if (value.Any(character => "[](){}".Contains(character)) || value.Contains("::", StringComparison.Ordinal)) throw new ArgumentException("New branches use simple text without shape or metadata delimiters.");
        return text.Insert(text.Index(subtree[^1].Identity) + 1, indent + value);
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = Node(model, identity); if (node.Parent is null) throw new ArgumentException("The single root cannot be removed. Edit its text or remove a child subtree.");
        var subtree = Subtree(model, node); Structural(text, model, subtree);
        var start = text.AttachedStart(identity); return text.Splice(start, text.Index(subtree[^1].Identity) + 1 - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = Node(model, identity); var siblings = model.Nodes.Where(item => item.Parent == node.Parent).ToArray(); var index = Array.IndexOf(siblings, node); var other = index + Math.Sign(direction);
        if (node.Parent is null || other < 0 || other >= siblings.Length) throw new ArgumentException("There is no sibling in that direction.");
        var first = siblings[Math.Min(index, other)]; var second = siblings[Math.Max(index, other)];
        var a = Subtree(model, first); var b = Subtree(model, second); Structural(text, model, a.Concat(b).ToArray());
        return CheckParents(model, SourceRowMoves.Swap(text, first.Identity, second.Identity, text.Index(a[^1].Identity) + 1, text.Index(b[^1].Identity) + 1));
    }, token);
    public Task<SourceAuthoringResult> ReparentAsync(string identity, string parentIdentity, CancellationToken token = default) => ChangeAsync((text, model) => Reparent(text, model, identity, parentIdentity), token);
    public Task<SourceAuthoringResult> IndentAsync(string identity, bool outdent, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = Node(model, identity); if (node.Parent is null) throw new ArgumentException("The single root cannot be reparented.");
        var parent = Node(model, node.Parent);
        if (outdent) return Reparent(text, model, identity, parent.Parent ?? throw new ArgumentException("A child of the root cannot be outdented into a second root."));
        var siblings = model.Nodes.Where(item => item.Parent == node.Parent).ToArray(); var index = Array.IndexOf(siblings, node);
        return Reparent(text, model, identity, index > 0 ? siblings[index - 1].Identity : throw new ArgumentException("Indent needs a previous sibling to become the parent."));
    }, token);
    private SourceText Reparent(SourceText text, MindmapSourceModel model, string identity, string parentIdentity)
    {
        var node = Node(model, identity); var parent = Node(model, parentIdentity);
        if (node.Parent is null) throw new ArgumentException("The single root cannot be reparented.");
        var subtree = Subtree(model, node); if (subtree.Any(item => item.Identity == parentIdentity)) throw new ArgumentException("A node cannot become its own parent or a descendant's child.");
        if (node.Parent == parentIdentity) return text;
        Structural(text, model, subtree); Editable(parent);
        var targetTree = Subtree(model, parent); Structural(text, model, targetTree);
        var indent = ChildIndent(model, parent, identity); var start = text.AttachedStart(identity); var end = text.Index(subtree[^1].Identity) + 1;
        var destination = text.Index(targetTree[^1].Identity) + 1;
        // If the target contains this subtree, append after its last remaining descendant.
        if (destination == end) destination = start;
        var moved = text.Lines.Skip(start).Take(end - start).Select(line =>
        {
            if (line.Content.Length == 0) return line;
            if (!line.Indent.StartsWith(node.Indentation, StringComparison.Ordinal)) throw new ArgumentException("An attached line has ambiguous indentation; reparenting is protected.");
            return line.Replace(0, node.Indentation.Length, indent);
        }).ToArray();
        if (moved.Any(line => line.Ending.Length == 0)) throw new ArgumentException("Reparenting an unterminated source group is protected. Add a line ending in source first.");
        var remaining = text.Splice(start, end - start, []); if (destination > start) destination -= end - start;
        return CheckParents(model, remaining.Splice(destination, 0, moved), identity, parentIdentity);
    }
    private SourceText CheckParents(MindmapSourceModel before, SourceText changed, string? moved = null, string? parent = null)
    {
        var after = Scan(changed);
        foreach (var node in before.Nodes)
            if (after.Nodes.Single(item => item.Identity == node.Identity).Parent != (node.Identity == moved ? parent : node.Parent))
                throw new ArgumentException("This indentation change would alter an unintended parent or subtree. The operation is protected.");
        return changed;
    }
    private static MindmapSourceNode Node(MindmapSourceModel model, string identity) => model.Nodes.SingleOrDefault(node => node.Identity == identity) ?? throw new ArgumentException("Select a Mind map node.");
    public static IReadOnlyList<MindmapSourceNode> Subtree(MindmapSourceModel model, MindmapSourceNode node) => model.Nodes.SkipWhile(item => item.Identity != node.Identity).Take(1).Concat(model.Nodes.SkipWhile(item => item.Identity != node.Identity).Skip(1).TakeWhile(item => item.Depth > node.Depth)).ToArray();
    private static void Editable(MindmapSourceNode node) { if (node.Protection is { } reason) throw new ArgumentException(reason); }
    private static void Structural(SourceText text, MindmapSourceModel model, IReadOnlyList<MindmapSourceNode> nodes)
    {
        if (nodes.Count > 0)
        {
            var first = nodes.Min(node => text.Index(node.Identity)); var last = nodes.Max(node => text.Index(node.Identity));
            if (text.Lines.Skip(first).Take(last - first + 1).Any(line => line.InitDirective)) throw new ArgumentException("An initialisation directive inside this subtree protects its structural source group.");
        }
        foreach (var node in nodes)
        {
            Editable(node);
            if (node.Parent is { } parentKey)
            {
                var parent = Node(model, parentKey);
                if (!node.Indentation.StartsWith(parent.Indentation, StringComparison.Ordinal) || node.Indentation.Length <= parent.Indentation.Length)
                    throw new ArgumentException("Mixed or ambiguous parent indentation protects this subtree operation.");
            }
            // All intervening comments must have a demonstrated following-node attachment.
            var row = text.Index(node.Identity); var next = row + 1;
            while (next < text.Lines.Count && (text.Lines[next].Comment || text.Lines[next].Content.Length == 0)) next++;
            for (var index = row + 1; index < next; index++)
                if (text.Lines[index].Comment && (next >= text.Lines.Count || index < text.AttachedStart(text.Lines[next].Key)))
                    throw new ArgumentException("A trailing or differently indented comment has ambiguous attachment; structural edits are protected locally.");
        }
    }
    private static string ChildIndent(MindmapSourceModel model, MindmapSourceNode parent, string? excluded = null)
    {
        var children = model.Nodes.Where(node => node.Parent == parent.Identity && node.Identity != excluded).ToArray();
        if (children.Length > 0)
        {
            var indent = children[0].Indentation;
            if (children.Any(node => node.Indentation != indent) || !indent.StartsWith(parent.Indentation, StringComparison.Ordinal)) throw new ArgumentException("Sibling indentation is mixed or ambiguous; child insertion/reparenting is protected.");
            return indent;
        }
        var unit = parent.Parent is { } key ? parent.Indentation[Node(model, key).Indentation.Length..] : parent.Indentation.Contains('\t') ? "\t" : "  ";
        if (unit.Length == 0 || unit.Contains(' ') && unit.Contains('\t')) throw new ArgumentException("An indentation unit cannot be proved locally.");
        return parent.Indentation + unit;
    }
}
