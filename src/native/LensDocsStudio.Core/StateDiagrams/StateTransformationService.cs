using System.Text;
using System.Xml;
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.StateDiagrams;

public static class StateCapabilityAnalysis
{
    public static StateOperationCapability For(StateDiagramModel model, StateEditOperation operation)
    {
        try { _ = new StateTransformationService().Plan(model, operation); return new(true, "The source spans and dependencies permit this operation."); }
        catch (ArgumentException error) { return new(false, error.Message); }
    }
}

/// <summary>Plans minimal expected-text patches. All plans still require complete semantic and renderer validation.</summary>
public sealed class StateTransformationService
{
    public StateEditPlan Plan(StateDiagramModel model, StateEditOperation operation)
    {
        if (!model.IsValid) throw new ArgumentException(string.Join(" ", model.Diagnostics));
        if (model.HasUnknownDependencies) throw new ArgumentException("Unrecognised source may affect identities or hierarchy. Retain it and use Edit source.");
        var patches = new List<MermaidDocumentPatch>(); var identities = model.Statements.Select(item => item.Identity).ToList();
        void Patch(MermaidSourceSpan span, string replacement) { if (span.Read(model.Source) != replacement) patches.Add(new(span, span.Read(model.Source), replacement)); }
        void Token(StateSourceStatement statement, string token, string value) => Patch(statement.Tokens[token], value);
        void Simple()
        { if (!model.IsSimple) throw new ArgumentException("Hierarchy, notes and styling protect structural operations. Independent labels remain editable where dependencies are known."); }
        void NewId(string id)
        {
            if (!StateDiagramSourceParser.IsIdentifier(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, @"^\w+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                throw new ArgumentException("Use a unique letter/word state ID without spaces, hyphens, reserved underscores or delimiters.");
            if (model.Nodes.Any(item => item.Id == id) || model.Groups.Any(item => item.Id == id)) throw new ArgumentException("That state ID is already in use.");
        }
        void SafeNode(StateNode node)
        {
            if (node.Kind != "state" || node.Declarations.Count > 1 || node.Scopes.Count > 1)
                throw new ArgumentException("Repeated declarations, special states or shared hierarchy protect this state. Use Edit source.");
        }
        void Insert(string text, int position, int statementIndex)
        {
            var prefix = position > 0 && model.Source[position - 1] != '\n' ? model.NewLine : "";
            Patch(new(position, 0), prefix + "    " + text + model.NewLine);
            identities.Insert(statementIndex, Guid.NewGuid().ToString("N"));
        }
        void Append(string text) => Insert(text, model.Source.Length, identities.Count);
        void Remove(StateSourceStatement statement)
        { Patch(statement.Line, ""); identities.Remove(statement.Identity); }
        string Endpoint(string value, bool initial, string? scope)
        {
            if (value == "[*]")
            { if (scope is not null) throw new ArgumentException("Create scoped pseudo-state transitions in source."); return value; }
            if (value.StartsWith("pseudo:", StringComparison.Ordinal))
            {
                var pseudo = model.PseudoNodes.SingleOrDefault(item => item.Identity == value);
                if (pseudo is null || pseudo.Initial != initial || pseudo.Scope != scope) throw new ArgumentException("An initial marker must be a source and a final marker a destination in its own scope.");
                return "[*]";
            }
            var node = model.Nodes.SingleOrDefault(item => item.Id == value);
            if (node is null || node.Scopes.Count != 1 || node.Scopes[0] != (scope ?? "")) throw new ArgumentException("Choose an existing state in the same source scope. Group endpoints and membership changes are protected.");
            return value;
        }
        switch (operation)
        {
            case AddStateNode add:
                Simple(); NewId(add.Id);
                if (add.Kind != "state") throw new ArgumentException("Special state insertion is protected in this version.");
                Append("state \"" + Label(add.Label, true) + "\" as " + add.Id); break;
            case DuplicateStateNode duplicate:
                var original = model.Node(duplicate.Id); SafeNode(original);
                return Plan(model, new AddStateNode(duplicate.NewId, original.Label));
            case UpdateStateNode update:
            {
                var node = model.Node(update.Id); SafeNode(node); var label = Label(update.Label, true);
                if (update.Id != update.NewId)
                {
                    Simple(); NewId(update.NewId);
                    foreach (var reference in node.References) Patch(reference.Span, update.NewId);
                }
                if (node.Label != update.Label)
                {
                    if (node.Declaration is { } declaration) Token(declaration, "label", label);
                    else
                    {
                        if (!model.IsSimple) throw new ArgumentException("An implicit state in a complex diagram needs an explicit declaration in source before its label can be edited.");
                        // The pinned parser keeps the first occurrence, including bare references.
                        var declarationText = update.NewId.Contains('-') ? update.NewId + " : " + Label(update.Label, false) : "state \"" + label + "\" as " + update.NewId;
                        Insert(declarationText, node.First.Line.Start, model.Statements.ToList().IndexOf(node.First));
                    }
                }
                break;
            }
            case DeleteStateNode delete:
            {
                Simple(); var node = model.Node(delete.Id); SafeNode(node);
                if (model.Transitions.Any(item => item.Source == delete.Id || item.Destination == delete.Id)) throw new ArgumentException("Delete incident transitions before deleting this state.");
                if (node.Declaration is null || node.References.Count != 1) throw new ArgumentException("Only an independent explicitly declared state can be deleted safely.");
                Remove(node.Declaration); break;
            }
            case AddStateTransition add:
                Simple(); var from = Endpoint(add.Source, true, null); var to = Endpoint(add.Destination, false, null);
                if (from == "[*]" && to == "[*]") throw new ArgumentException("Connect a pseudo-state to a named state.");
                Append(from + " --> " + to + (add.Label.Length > 0 ? " : " + Label(add.Label, false) : "")); break;
            case UpdateStateTransition update:
            {
                var transition = model.Transition(update.Identity); var statement = transition.Statement;
                if (transition.Source != update.Source || transition.Destination != update.Destination)
                {
                    Simple(); Token(statement, "from", Endpoint(update.Source, true, statement.Scope)); Token(statement, "to", Endpoint(update.Destination, false, statement.Scope));
                }
                if (transition.Label != update.Label)
                {
                    var label = Label(update.Label, false, allowEmpty: true);
                    if (statement.Tokens.TryGetValue("label", out var span))
                    {
                        if (label.Length > 0) Patch(span, label);
                        else
                        { var end = statement.Tokens["to"].End; if (statement.Tokens.TryGetValue("toClass", out var classSpan)) end = classSpan.End; Patch(new(end, statement.Span.End - end), ""); }
                    }
                    else Patch(new(statement.Span.End, 0), " : " + label);
                }
                break;
            }
            case DeleteStateTransition delete:
                Simple(); Remove(model.Transition(delete.Identity).Statement); break;
            case UpdateStateGroupLabel update:
            {
                var group = model.Groups.SingleOrDefault(item => item.Id == update.Id) ?? throw new ArgumentException("Select a composite state.");
                if (update.Label.Contains('\n') || update.Label.Contains('\r')) throw new ArgumentException("Composite headings must stay on one line.");
                if (group.Opening.Tokens.ContainsKey("label")) Token(group.Opening, "label", Label(update.Label, true));
                else Patch(group.Opening.Span, "state \"" + Label(update.Label, true) + "\" as " + group.Id + " {");
                break;
            }
            default: throw new ArgumentException("This State operation is protected in this version.");
        }
        return new(patches.AsReadOnly(), identities.AsReadOnly());
    }
    internal static string Label(string value, bool quoted, bool allowEmpty = false)
    {
        if (value.Length > 2000 || !allowEmpty && string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Use a non-empty label of at most 2,000 characters.");
        try { XmlConvert.VerifyXmlChars(value); } catch (XmlException) { throw new ArgumentException("Use valid Unicode text in State labels."); }
        var output = new StringBuilder();
        foreach (var rune in value.ReplaceLineEndings("\n").EnumerateRunes())
        {
            if (rune.Value == '\n') output.Append("<br/>");
            else if (rune.Value is '"' or '#' or '<' or '>' or '&' or '{' or '}' or '`' or ';' || !quoted && rune.Value == ':') output.Append('#').Append(rune.Value).Append(';');
            else output.Append(rune);
        }
        return output.ToString();
    }
}
