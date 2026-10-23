using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.TreeViewDiagrams;

public sealed record TreeViewSourceNode(string Identity, string Prefix, SourceField LabelToken, bool Quoted, SourceField DirectoryToken, SourceField? DescriptionToken, SourceField? ClassToken, SourceField? IconToken, string? Icon, bool Proved)
{
    public string Label => LabelToken.Value;
    public bool Directory => DirectoryToken.Raw == "/";
}
public sealed record TreeViewSourceModel(string HeaderIdentity, bool BoxMode, IReadOnlyList<TreeViewSourceNode> Nodes, IReadOnlyList<SourceHierarchyRow> Hierarchy)
{
    public bool StructureSafe => !BoxMode && Nodes.All(n => n.Proved);
}

/// <summary>Two-mode native hierarchy with exact field spans and conservative box-prefix structural protection.</summary>
public sealed class TreeViewVisualDraft : SourceDraft<TreeViewSourceModel>
{
    public const string Starter = "treeView-beta\nproject/\n    \"source files\"/ :::highlight ## Local code\n        main.cs\n    README.md\n    hidden.txt icon(none)\n";
    private static readonly Regex Header = SourceText.Pattern(@"^treeView(?:-beta)?$", RegexOptions.IgnoreCase);
    private static readonly Regex Branch = SourceText.Pattern("[├└┣┗]");
    private static readonly Regex Content = SourceText.Pattern("^(?<label>\"[^\"]+\"|[^\"\\r\\n]+?)(?<directory>/)?(?:[ \\t]+icon\\((?<icon>[^()]*)\\))?(?:[ \\t]+:::(?<class>[\\w][\\w-]*))?(?:[ \\t]+##[ \\t]*(?<description>.+?))?[ \\t]*$");
    public static bool IsTreeView(string source) => Header.IsMatch(ChartSource.Header(source));
    public TreeViewVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override TreeViewSourceModel Scan(SourceText text) => Read(text);
    public static TreeViewSourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !Header.IsMatch(body[0].Content)) throw new ArgumentException("This editor requires treeView or treeView-beta source.");
        var box = body.Count > 1 && Branch.IsMatch(body[1].Body); var nodes = new List<TreeViewSourceNode>(); var rows = new List<(string, int)>();
        foreach (var line in body.Skip(1))
        {
            var prefixLength = box ? line.Body.TakeWhile(c => "├└│─┣┗┃━┬┳ \t".Contains(c)).Count() : line.Indent.Length;
            var prefix = line.Body[..prefixLength]; var content = line.Body[prefixLength..].TrimEnd(); var match = Content.Match(content);
            var expanded = line.Body.Replace("\t", "    ", StringComparison.Ordinal); var branch = Branch.Match(expanded); var indent = box ? branch.Success ? branch.Index / 4 : 0 : line.Indent.Sum(c => c == '\t' ? 4 : 1);
            rows.Add((line.Key, indent));
            SourceField Field(string name, Group group, string? value = null) => new(line.Key, name, value ?? group.Value, prefixLength + group.Index, group.Length, group.Value);
            var label = match.Groups["label"]; var quoted = label.Value.StartsWith('"') && label.Value.EndsWith('"'); var value = quoted ? label.Value[1..^1] : label.Value;
            var token = match.Success ? Field("label", label, value) : new SourceField(line.Key, "label", content, prefixLength, content.Length, content);
            var directory = match.Groups["directory"]; var slash = directory.Success ? Field("directory", directory) : new SourceField(line.Key, "directory", "", prefixLength + label.Index + label.Length, 0, "");
            var icon = match.Groups["icon"]; var className = match.Groups["class"]; var desc = match.Groups["description"];
            var proved = match.Success && !value.Contains("icon(", StringComparison.Ordinal) && !value.Contains(":::", StringComparison.Ordinal) && !value.Contains("##", StringComparison.Ordinal)
                && !value.Any(c => c is '"' or '<' or '>') && !Branch.IsMatch(value) && !value.EndsWith('/') && value == value.Trim() && !desc.Value.Contains(":::", StringComparison.Ordinal)
                && !icon.Value.Contains(":::", StringComparison.Ordinal) && !icon.Value.Contains("##", StringComparison.Ordinal) && (!box || Branch.IsMatch(prefix));
            nodes.Add(new(line.Key, prefix, token, quoted, slash, desc.Success ? Field("description", desc) : null, className.Success ? Field("class", className) : null, icon.Success ? Field("icon", icon) : null,
                icon.Success ? icon.Value.Trim().Length == 0 || icon.Value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? "" : icon.Value.Trim() : null, proved));
        }
        return new(body[0].Key, box, nodes, SourceHierarchy.Link(rows));
    }
    protected override bool Contains(TreeViewSourceModel model, string identity) => identity == model.HeaderIdentity || model.Nodes.Any(n => n.Identity == identity);
    protected override void Validate(SourceText text, TreeViewSourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered); var shapes = SourceDeclarations.Groups(rendered.Svg!, "treeview-node");
        if (shapes.Length != model.Nodes.Count) throw new ArgumentException("The native TreeView occurrence count differs. Inspect source; Apply is protected.");
        foreach (var ((shape, node), hierarchy) in shapes.Zip(model.Nodes).Zip(model.Hierarchy))
        {
            if (!node.Proved) continue;
            var texts = shape.Elements().Where(e => e.Name.LocalName == "text").ToArray();
            if ((string?)shape.Attribute("data-depth") != hierarchy.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture) || texts.Length == 0 || texts[0].Value != node.Label || node.DescriptionToken is not null && texts.Last().Value != node.DescriptionToken.Value)
                throw new ArgumentException("TreeView fields or hierarchy differ from actual native row text/depth. Inspect source; Apply is protected.");
            var glyph = shape.Elements().Any(e => (string?)e.Attribute("class") == "treeview-icon");
            if (node.Icon is null && !glyph || node.Icon == "" && glyph) throw new ArgumentException("Native default/hidden icon correspondence changed. Inspect source.");
        }
    }
    public static IReadOnlyList<(TreeViewSourceNode Node, XElement Shape)> Correspondence(TreeViewSourceModel model, string svg)
    {
        var shapes = SourceDeclarations.Groups(svg, "treeview-node"); if (shapes.Length != model.Nodes.Count) return [];
        return model.Nodes.Zip(shapes).Where(p => p.First.Proved && model.Nodes.Count(n => n.Label == p.First.Label) == 1 && p.First.Icon is null or "").Select(p => (p.First, p.Second)).ToArray();
    }
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, bool directory, string description, string cssClass, string iconChoice, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = model.Nodes.Single(n => n.Identity == identity); if (!node.Proved) throw new ArgumentException("Ambiguous parser annotations/prefix protect this occurrence. Inspect the exact source.");
        Label(label); var patches = new List<(SourceField, string)>();
        if (label != node.Label) patches.Add((node.LabelToken, node.Quoted ? "\"" + label + "\"" : label));
        if (directory != node.Directory) patches.Add((node.DirectoryToken, directory ? "/" : ""));
        if (description != (node.DescriptionToken?.Value ?? ""))
        {
            if (description.Contains(":::", StringComparison.Ordinal) || description.Any(char.IsControl) || description.Length > 2000 || description != description.Trim()) throw new ArgumentException("Enter a literal description without class annotations, surrounding whitespace or control characters.");
            if (node.DescriptionToken is { } field && description.Length > 0) patches.Add((field, description));
        }
        if (cssClass != (node.ClassToken?.Value ?? ""))
        {
            if (cssClass.Length > 0 && !SourceText.Pattern(@"^[\w][\w-]*$").IsMatch(cssClass)) throw new ArgumentException("Enter a parser-compatible class name.");
            if (node.ClassToken is { } field && cssClass.Length > 0) patches.Add((field, cssClass));
        }
        var currentIcon = node.Icon is null ? "default" : node.Icon == "" ? "hidden" : "protected";
        if (iconChoice != currentIcon)
        {
            if (iconChoice is not ("hidden" or "default")) throw new ArgumentException("Choose default native glyph or hidden icon. Explicit artwork remains source-only under native sanitisation.");
        }
        var changed = TopologySource.Patch(text, patches);
        // Re-scan between optional-annotation changes so every following token span remains authoritative.
        changed = Annotation(changed, identity, "description", description);
        changed = Annotation(changed, identity, "class", cssClass);
        if (iconChoice != currentIcon) changed = Annotation(changed, identity, "icon", iconChoice == "hidden" ? "none" : "");
        var proved = Read(changed).Nodes.Single(n => n.Identity == identity);
        if (!proved.Proved || proved.Label != label || proved.Directory != directory || (proved.DescriptionToken?.Value ?? "") != description || (proved.ClassToken?.Value ?? "") != cssClass || proved.Icon != (iconChoice == "default" ? null : iconChoice == "hidden" ? "" : node.Icon))
            throw new ArgumentException("The edited annotations do not reparse to their intended native fields. Pending input is retained.");
        return changed;
    }, token);
    public Task<SourceAuthoringResult> AddAsync(string? parent, string label, bool directory, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model); Label(label); var indent = parent is null ? model.Hierarchy.FirstOrDefault()?.Indent ?? 0 : model.Hierarchy.Single(r => r.Identity == parent).Indent + 4;
        var at = parent is null ? text.Lines.Count : SourceHierarchy.Bounds(text, model.Hierarchy, parent).End; SourceSections.CheckTail(text, 0, at);
        var changed = text.Insert(at, new string(' ', indent) + "\"" + label + "\"" + (directory ? "/" : "")); var next = Read(changed);
        SourceHierarchy.ProveMembership(model.Hierarchy, next.Hierarchy); if (next.Hierarchy.Single(r => !model.Hierarchy.Any(old => old.Identity == r.Identity)).Parent != parent) throw new ArgumentException("The new node would have an unintended parent."); return changed;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) => { Structure(model); return SourceHierarchy.Delete(text, model.Hierarchy, identity); }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    { Structure(model); var changed = SourceHierarchy.Move(text, model.Hierarchy, identity, direction); SourceHierarchy.ProveMembership(model.Hierarchy, Read(changed).Hierarchy); return changed; }, token);
    public Task<SourceAuthoringResult> ReparentAsync(string identity, string? parent, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model); var indent = parent is null ? model.Hierarchy.Min(r => r.Indent) : model.Hierarchy.Single(r => r.Identity == parent).Indent + 4;
        var changed = SourceHierarchy.Reparent(text, model.Hierarchy, identity, parent, indent); SourceHierarchy.ProveMembership(model.Hierarchy, Read(changed).Hierarchy, identity, parent); return changed;
    }, token);
    private static void Structure(TreeViewSourceModel model)
    { if (!model.StructureSafe) throw new ArgumentException(model.BoxMode ? "Box-drawing field edits retain their exact glyphs. Structural prefix re-encoding has not been independently proved; use Edit source." : "Ambiguous mixed annotations or prefixes protect complete subtree structure. Inspect source."); }
    private static void Label(string value)
    { SourceText.Label(value); if (value.IndexOfAny(['"', '/', '<', '>']) >= 0 || value.Contains("icon(", StringComparison.Ordinal) || value.Contains(":::", StringComparison.Ordinal) || value.Contains("##", StringComparison.Ordinal) || value.StartsWith("%%", StringComparison.Ordinal) || Branch.IsMatch(value)) throw new ArgumentException("This label contains unproved TreeView quote, slash, prefix or annotation syntax. Pending input is retained."); }
    private static SourceText Annotation(SourceText text, string identity, string kind, string value)
    {
        var node = Read(text).Nodes.Single(n => n.Identity == identity); var field = kind == "description" ? node.DescriptionToken : kind == "class" ? node.ClassToken : node.IconToken;
        if (field?.Value == value || field is null && value.Length == 0) return text;
        var line = text.Line(identity);
        if (field is not null)
        {
            if (value.Length > 0) return field.Set(text, value);
            var start = kind == "description" ? line.Body.LastIndexOf("##", field.Start, StringComparison.Ordinal) : field.Start - (kind == "class" ? 3 : 5);
            var end = field.Start + field.Length + (kind == "icon" ? 1 : 0); return text.Replace(line.Replace(start, end - start, ""));
        }
        var insertion = kind == "description" ? line.Body.TrimEnd().Length : node.DescriptionToken is { } desc ? line.Body.LastIndexOf("##", desc.Start, StringComparison.Ordinal) : line.Body.TrimEnd().Length;
        if (kind == "icon" && node.ClassToken is { } css) insertion = Math.Min(insertion, css.Start - 3);
        var encoded = kind switch { "description" => " ## " + value, "class" => " :::" + value + " ", _ => " icon(" + value + ") " };
        return text.Replace(line.Replace(insertion, 0, encoded));
    }
}
