namespace LensDocsStudio.Core.Mermaid;

public abstract record MermaidEditOperation;
public sealed record AddMermaidNode(string Id, string Label, FlowNodeShape Shape, string? SubgraphId = null) : MermaidEditOperation;
public sealed record UpdateMermaidNode(string Id, string Label, FlowNodeShape Shape, string NewId) : MermaidEditOperation;
public sealed record DeleteMermaidNodes(IReadOnlyList<string> Ids) : MermaidEditOperation;
public sealed record AddMermaidConnection(string Source, string Target, string Label = "", FlowConnectionType Type = FlowConnectionType.Directed) : MermaidEditOperation;
public sealed record UpdateMermaidConnection(string Id, string Source, string Target, string Label, FlowConnectionType Type) : MermaidEditOperation;
public sealed record DeleteMermaidConnection(string Id) : MermaidEditOperation;
public sealed record AddMermaidSubgraph(string Id, string Label, string? ParentId = null) : MermaidEditOperation;
public sealed record RenameMermaidSubgraph(string Id, string Label) : MermaidEditOperation;
public sealed record MoveMermaidNode(string Id, string? SubgraphId) : MermaidEditOperation;
public sealed record DeleteMermaidSubgraph(string Id, bool DeleteContents = false) : MermaidEditOperation;
public sealed record ChangeMermaidDirection(string Direction, string? SubgraphId = null) : MermaidEditOperation;
public sealed record SetMermaidClass(string Id, string ClassName) : MermaidEditOperation;
public sealed record SetMermaidStyle(string Id, string Fill, string Stroke, string Colour) : MermaidEditOperation;
public sealed record AddMermaidClassDefinition(string ClassName, string Properties) : MermaidEditOperation;
public sealed record SetMermaidStyleProperties(string Id, string Properties) : MermaidEditOperation;

/// <summary>Plans exact edits against the verified source. It never serialises the entire graph.</summary>
public sealed class MermaidTransformationService
{
    public IReadOnlyList<MermaidDocumentPatch> Plan(MermaidDiagramModel model, MermaidEditOperation operation)
    {
        model.Capabilities.Require(operation);
        var patches = new List<MermaidDocumentPatch>();
        switch (operation)
        {
            case AddMermaidNode add:
                CheckNewId(add.Id); Add(NodeText(add.Id, add.Label, add.Shape), add.SubgraphId); break;
            case UpdateMermaidNode update:
            {
                var node = model.Node(update.Id); CheckLabel(update.Label); CheckShape(update.Shape);
                if (update.NewId != node.Id) CheckNewId(update.NewId);
                var declaration = node.Declaration;
                foreach (var occurrence in node.Occurrences)
                {
                    if (occurrence == declaration && (update.Label != node.Label || update.Shape != node.Shape))
                    {
                        // Preserve the existing shape delimiters and spacing when only the label changes.
                        if (update.Shape == node.Shape && occurrence.LabelSpan is { } label)
                        { Replace(label, QuotedLabel(update.Label)); if (update.NewId != node.Id) Replace(occurrence.IdSpan, update.NewId); }
                        else
                        {
                            var syntax = NodeDelimiters(update.Shape); var rawLabel = occurrence.LabelSpan!.Value.Read(model.Source);
                            var text = update.Label == node.Label && !rawLabel.Contains(syntax.Close, StringComparison.Ordinal) && !rawLabel.Contains('\n')
                                ? update.NewId + syntax.Open + rawLabel + syntax.Close : NodeText(update.NewId, update.Label, update.Shape);
                            Replace(new(occurrence.Span.Start, occurrence.Span.Length - (occurrence.ClassName?.Length + 3 ?? 0)), text);
                        }
                    }
                    else if (update.NewId != node.Id) Replace(occurrence.IdSpan, update.NewId);
                }
                if (declaration is null && (update.Label != node.Label || update.Shape != node.Shape || update.NewId != node.Id))
                {
                    // Upgrade one bare reference in place, avoiding a second node identity or a global rewrite.
                    var first = node.Occurrences[0]; patches.RemoveAll(patch => patch.Span == first.IdSpan);
                    Replace(first.IdSpan, NodeText(update.NewId, update.Label, update.Shape));
                }
                if (update.NewId != node.Id)
                    foreach (var target in model.Styles.Where(style => style.Kind != "classDef").SelectMany(style => style.Targets).Where(target => target.Id == node.Id)) Replace(target.Span, update.NewId);
                break;
            }
            case DeleteMermaidNodes delete:
                DeleteNodes(delete.Ids); break;
            case AddMermaidConnection add:
                model.Node(add.Source); model.Node(add.Target); CheckLabel(add.Label);
                Add(add.Source + " " + ConnectionText(add.Type, add.Label) + " " + add.Target); break;
            case UpdateMermaidConnection update:
            {
                var edge = model.Edge(update.Id); model.Node(update.Source); model.Node(update.Target); CheckLabel(update.Label);
                if (edge.Source.Id != update.Source || edge.Target.Id != update.Target)
                {
                    // Inline declarations belong to their nodes. Retain them when an endpoint is disconnected.
                    var declarations = new[] { edge.Source, edge.Target }.DistinctBy(item => item.Id)
                        .Select(item => item.Span.Read(model.Source));
                    Replace(edge.Statement, string.Join(model.NewLine + model.Indentation, declarations));
                    // Mermaider honours the first occurrence, even a bare reference. Place the changed
                    // connection after existing declarations so it cannot hide a later node label/shape.
                    Add(update.Source + " " + ConnectionText(update.Type, update.Label) + " " + update.Target);
                }
                else
                {
                    if (edge.InlineLabel && update.Label.Length == 0 && edge.Label != update.Label)
                        Replace(edge.ConnectionSpan, ConnectionToken(update.Type));
                    else if (edge.InlineLabel && edge.Type != update.Type)
                        Replace(edge.ConnectionSpan, ConnectionToken(update.Type) + "|" + (edge.Label == update.Label ? edge.LabelSpan!.Value.Read(model.Source) : EscapeLabel(update.Label)) + "|");
                    else if (edge.Type != update.Type) Replace(edge.ConnectionSpan, ConnectionToken(update.Type));
                    if (edge.Label != update.Label && !(edge.InlineLabel && (edge.Type != update.Type || update.Label.Length == 0)))
                    {
                        if (edge.LabelSpan is { } span)
                        {
                            var label = EscapeLabel(update.Label);
                            var quoted = span.Start > 0 && span.End < model.Source.Length && model.Source[span.Start - 1] == '"' && model.Source[span.End] == '"';
                            // Numeric escapes contain semicolons. A quoted inline token keeps
                            // them inside the label rather than creating source statements.
                            Replace(span, edge.InlineLabel && !quoted && label.Contains(';') ? "\"" + label + "\"" : label);
                        }
                        else Insert(edge.ConnectionSpan.End, "|" + EscapeLabel(update.Label) + "|");
                    }
                }
                break;
            }
            case DeleteMermaidConnection delete:
            {
                var edge = model.Edge(delete.Id);
                Replace(edge.Statement, string.Join(model.NewLine + model.Indentation,
                    new[] { edge.Source, edge.Target }.DistinctBy(item => item.Id).Select(item => item.Span.Read(model.Source)))); break;
            }
            case AddMermaidSubgraph add:
                CheckNewId(add.Id); CheckLabel(add.Label);
                Add("subgraph " + add.Id + "[" + EscapeLabel(add.Label) + "]" + model.NewLine + model.Indentation + "end", add.ParentId); break;
            case RenameMermaidSubgraph rename:
            {
                CheckLabel(rename.Label); var group = model.Subgraph(rename.Id);
                if (rename.Label != group.Label)
                {
                    var heading = group.Heading.Read(model.Source); var opening = heading.IndexOf('[');
                    if (opening >= 0) Replace(new(group.Heading.Start + opening + 1, group.Heading.Length - opening - 2), EscapeLabel(rename.Label));
                    else Replace(group.Heading, group.Id + "[" + EscapeLabel(rename.Label) + "]");
                }
                break;
            }
            case MoveMermaidNode move:
            {
                var node = model.Node(move.Id); if (move.SubgraphId is not null) model.Subgraph(move.SubgraphId);
                if (node.SubgraphId == move.SubgraphId && node.Occurrences.Select(item => item.SubgraphId).Where(id => id is not null).Distinct().Count() <= 1) break;
                if (node.Occurrences.Any(item => item.SubgraphId is not null && model.Edges.Any(edge => edge.Statement == item.Statement)))
                    throw new ArgumentException("Move this node's connections outside subgraphs in source mode first. Its existing membership is preserved.");
                var declarations = node.Occurrences.Where(item => !model.Edges.Any(edge => edge.Statement == item.Statement)).ToArray();
                // Bare references at root do not add a group membership; declarations move without touching connections.
                foreach (var item in declarations) Replace(item.Statement, "");
                var declaration = node.Declaration?.Span.Read(model.Source) ?? NodeText(node.Id, node.Label, node.Shape);
                Insert(model.DirectionSpan.End, model.NewLine + model.Indentation + declaration);
                if (move.SubgraphId is not null) Add(node.Id, move.SubgraphId);
                break;
            }
            case DeleteMermaidSubgraph delete:
            {
                var group = model.Subgraph(delete.Id);
                var descendants = model.Subgraphs.Where(item => item.Opening.Start >= group.Opening.Start && item.Closing.End <= group.Closing.End).ToArray();
                if (delete.DeleteContents)
                    DeleteNodes(model.Nodes.Where(node => node.Occurrences.Any(item => descendants.Any(child => child.Id == item.SubgraphId))).Select(node => node.Id).ToArray());
                foreach (var item in delete.DeleteContents ? descendants : [group])
                { Replace(item.Opening, ""); Replace(item.Closing, ""); if (item.DirectionSpan is { } span) Replace(span, ""); }
                break;
            }
            case ChangeMermaidDirection change:
                if (!MermaidSourceParser.IsDirection(change.Direction)) throw new ArgumentException("Choose TD, TB, BT, LR or RL.");
                if (change.SubgraphId is null) Replace(model.DirectionSpan, change.Direction);
                else
                {
                    var group = model.Subgraph(change.SubgraphId);
                    if (group.DirectionSpan is { } span) Replace(span, "direction " + change.Direction);
                    else Insert(group.Opening.End, model.NewLine + model.Indentation + "direction " + change.Direction);
                }
                break;
            case SetMermaidClass assign:
                model.Node(assign.Id);
                if (!MermaidSourceParser.IsClassId(assign.ClassName) || !model.Styles.Any(style => style.Kind == "classDef" && style.Targets.Any(target => target.Id == assign.ClassName)))
                    throw new ArgumentException("Choose a class defined in this diagram's source.");
                var existing = model.Styles.LastOrDefault(style => style.Kind == "class" && style.Targets.Any(target => target.Id == assign.Id));
                if (existing?.Targets.Count == 1) Replace(new(existing.Statement.End - existing.Value.Length, existing.Value.Length), assign.ClassName);
                else Add("class " + assign.Id + " " + assign.ClassName);
                break;
            case SetMermaidStyle style:
                model.Node(style.Id); CheckColour(style.Fill); CheckColour(style.Stroke); CheckColour(style.Colour);
                var value = "style " + style.Id + " fill:" + style.Fill + ",stroke:" + style.Stroke + ",color:" + style.Colour;
                // An override keeps custom properties (width, dashes, fonts) and their original bytes.
                Add(value); break;
            case AddMermaidClassDefinition definition:
                if (!MermaidSourceParser.IsId(definition.ClassName) || model.Styles.Any(style => style.Kind == "classDef" && style.Targets.Any(target => target.Id == definition.ClassName)))
                    throw new ArgumentException("Choose a new class identifier.");
                CheckStyleProperties(definition.Properties); Add("classDef " + definition.ClassName + " " + definition.Properties.TrimEnd(';')); break;
            case SetMermaidStyleProperties properties:
                model.Node(properties.Id); CheckStyleProperties(properties.Properties); Add("style " + properties.Id + " " + properties.Properties.TrimEnd(';')); break;
            default: throw new ArgumentException("Choose a supported visual editing command.");
        }
        return patches;

        void Replace(MermaidSourceSpan span, string replacement) => patches.Add(new(span, span.Read(model.Source), replacement));
        void Insert(int offset, string text) => patches.Add(new(new(offset, 0), "", text));
        void CheckNewId(string id)
        {
            if (!MermaidSourceParser.IsId(id)) throw new ArgumentException("Use a unique identifier starting with a letter or underscore, followed by letters, numbers, underscores or hyphens (up to 128 characters).");
            if (model.Nodes.Any(node => node.Id == id) || model.Subgraphs.Any(group => group.Id == id)) throw new ArgumentException("That identifier already exists in this diagram.");
        }
        void Add(string text, string? groupId = null)
        {
            if (groupId is null)
            {
                var prefix = model.Source.Length == 0 || model.Source.EndsWith('\n') ? "" : model.NewLine;
                Insert(model.Source.Length, prefix + model.Indentation + text + model.NewLine);
            }
            else
            {
                var group = model.Subgraph(groupId);
                Insert(group.Closing.Start, text + model.NewLine + model.Indentation);
            }
        }
        void DeleteNodes(IReadOnlyList<string> ids)
        {
            var removed = ids.ToHashSet(StringComparer.Ordinal); foreach (var id in removed) model.Node(id);
            foreach (var edge in model.Edges.Where(edge => removed.Contains(edge.Source.Id) || removed.Contains(edge.Target.Id)))
                Replace(edge.Statement, string.Join(model.NewLine + model.Indentation,
                    new[] { edge.Source, edge.Target }.Where(item => !removed.Contains(item.Id)).DistinctBy(item => item.Id).Select(item => item.Span.Read(model.Source))));
            foreach (var node in model.Nodes.Where(node => removed.Contains(node.Id)))
                foreach (var item in node.Occurrences.Where(item => model.Edges.All(edge => edge.Statement != item.Statement))) Replace(item.Statement, "");
            foreach (var style in model.Styles.Where(style => style.Kind != "classDef" && style.Targets.Any(target => removed.Contains(target.Id))))
            {
                var remaining = style.Targets.Where(target => !removed.Contains(target.Id)).Select(target => target.Id).ToArray();
                Replace(style.Statement, remaining.Length == 0 ? "" : style.Kind + " " + string.Join(',', remaining) + " " + style.Value);
            }
        }
    }

    public static string UniqueId(MermaidDiagramModel model, string prefix = "Node")
    {
        if (!MermaidSourceParser.IsId(prefix)) prefix = "Node";
        var used = model.Nodes.Select(node => node.Id).Concat(model.Subgraphs.Select(group => group.Id)).ToHashSet(StringComparer.Ordinal);
        for (var i = 1; i <= MermaidSourceParser.MaximumElements + 1; i++) if (!used.Contains(prefix + i)) return prefix + i;
        throw new ArgumentException("The diagram has reached the editing limit.");
    }
    public static string NodeText(string id, string label, FlowNodeShape shape)
    {
        CheckLabel(label); CheckShape(shape);
        var (open, close) = NodeDelimiters(shape);
        return id + open + QuotedLabel(label) + close;
    }
    private static (string Open, string Close) NodeDelimiters(FlowNodeShape shape)
    {
        CheckShape(shape); return shape switch
        {
            FlowNodeShape.Decision => ("{", "}"), FlowNodeShape.Rounded => ("(", ")"), FlowNodeShape.Terminal => ("([", "])") ,
            FlowNodeShape.Circle => ("((", "))"), FlowNodeShape.DoubleCircle => ("(((", ")))"), FlowNodeShape.Subroutine => ("[[", "]]"),
            FlowNodeShape.Cylinder => ("[(", ")]"), FlowNodeShape.Hexagon => ("{{", "}}"), _ => ("[", "]")
        };
    }
    public static string ConnectionToken(FlowConnectionType type) => type switch
    {
        FlowConnectionType.Directed => "-->", FlowConnectionType.Undirected => "---", FlowConnectionType.Dotted => "-.->",
        FlowConnectionType.Thick => "==>", FlowConnectionType.Bidirectional => "<-->", _ => throw new ArgumentException("Choose a supported connection type.")
    };
    private static string ConnectionText(FlowConnectionType type, string label) => ConnectionToken(type) + (label.Length == 0 ? "" : "|" + EscapeLabel(label) + "|");
    private static string QuotedLabel(string label) => "\"" + EscapeLabel(label) + "\"";
    public static string EscapeLabel(string label)
    {
        CheckLabel(label); var escaped = new System.Text.StringBuilder();
        foreach (var character in label)
        {
            if (character == '\r') escaped.Append("#13;");
            else if (character == '\n') escaped.Append("<br/>");
            else if ("\"|[]{}()<>\\#&;`".Contains(character)) escaped.Append('#').Append((int)character).Append(';');
            else escaped.Append(character);
        }
        return escaped.ToString();
    }
    private static void CheckLabel(string label)
    {
        MermaidSourceParser.CheckUnicode(label);
        if (label.Length is 0 or > MermaidSourceParser.MaximumLabelCharacters)
        {
            if (label.Length == 0) return;
            throw new ArgumentException("Use a label of at most 4,096 characters.");
        }
    }
    private static void CheckShape(FlowNodeShape shape) { if (!Enum.IsDefined(shape)) throw new ArgumentException("Choose a supported node shape."); }
    private static void CheckColour(string value)
    {
        if (value.Length != 7 || value[0] != '#' || value[1..].Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Use a six-digit hexadecimal colour such as #FAFAFA.");
    }
    private static void CheckStyleProperties(string value)
    {
        if (value.Length is 0 or > 512) throw new ArgumentException("Copied styles support at most 512 characters.");
        foreach (var property in value.TrimEnd(';').Split(',', StringSplitOptions.TrimEntries))
        {
            var parts = property.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) throw new ArgumentException("This copied style is protected. Paste it in source mode to retain it.");
            if (parts[0] is "fill" or "stroke" or "color") CheckColour(parts[1]);
            else if (parts[0] is "stroke-width" or "font-size")
            {
                var number = parts[1].EndsWith("px", StringComparison.Ordinal) ? parts[1][..^2] : parts[1];
                if (!double.TryParse(number, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var size) || !double.IsFinite(size) || size is < 0 or > 64)
                    throw new ArgumentException("Copied numeric style values must be between 0 and 64 pixels.");
            }
            else if (parts[0] == "stroke-dasharray")
            {
                var numbers = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts[1].Length > 32 || numbers.Length is 0 or > 8 || numbers.Any(number =>
                    !double.TryParse(number, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var size)
                    || !double.IsFinite(size) || size is < 0 or > 64)) throw new ArgumentException("This dash pattern is protected. Use source mode.");
            }
            else if (parts[0] == "font-weight")
            { if (parts[1] is not ("normal" or "bold" or "400" or "600" or "700")) throw new ArgumentException("This font weight is protected. Use source mode."); }
            else throw new ArgumentException("This copied style uses a protected property. Use source mode to retain it.");
        }
    }
}
