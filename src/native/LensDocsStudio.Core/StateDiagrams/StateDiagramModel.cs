using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.StateDiagrams;

public enum StateStatementKind { Header, Declaration, Description, Transition, Composite, End, Stereotype, Note, Direction, Style, Unknown }
public enum StateSourceCompatibility { Editable, PartiallyEditable, Protected, Invalid }
public sealed record StateSourceStatement(string Identity, StateStatementKind Kind, MermaidSourceSpan Span,
    MermaidSourceSpan Line, string? Scope, IReadOnlyDictionary<string, MermaidSourceSpan> Tokens)
{
    public string Read(string source, string token) => Tokens.TryGetValue(token, out var span) ? span.Read(source) : "";
}
public sealed record StateReference(string Id, MermaidSourceSpan Span, string? Scope, string StatementIdentity);
public sealed record StateNode(string Id, string Label, string Kind, StateSourceStatement First,
    IReadOnlyList<StateSourceStatement> Declarations, IReadOnlyList<StateReference> References, IReadOnlyList<string> Scopes)
{
    public string Identity => "state:" + Id;
    public StateSourceStatement? Declaration => Declarations.FirstOrDefault(item => item.Identity == First.Identity);
}
public sealed record StatePseudoNode(string Identity, string RendererId, string? Scope, bool Initial);
public sealed record StateTransition(string Identity, string Source, string Destination, string Label, StateSourceStatement Statement);
public sealed record StateGroup(string Id, string Label, string? Parent, StateSourceStatement Opening, StateSourceStatement? Closing)
{
    public string Identity => "group:" + Id;
}
public sealed record StateDiagramModel(string Source, string Header, IReadOnlyList<StateSourceStatement> Statements,
    IReadOnlyList<StateNode> Nodes, IReadOnlyList<StateTransition> Transitions, IReadOnlyList<StateGroup> Groups,
    IReadOnlyList<StatePseudoNode> PseudoNodes, IReadOnlyList<string> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
    public bool HasUnknownDependencies => Statements.Any(item => item.Kind == StateStatementKind.Unknown);
    public bool IsSimple => Groups.Count == 0 && !Statements.Any(item => item.Kind is StateStatementKind.Unknown or StateStatementKind.Style or StateStatementKind.Note or StateStatementKind.Stereotype
        || item.Tokens.ContainsKey("fromClass") || item.Tokens.ContainsKey("toClass"));
    public StateSourceCompatibility Compatibility => !IsValid ? StateSourceCompatibility.Invalid : HasUnknownDependencies ? StateSourceCompatibility.Protected
        : !IsSimple || Nodes.Any(item => item.Declarations.Count > 1 || item.Declarations.Count > 0 && item.Declaration is null || item.Scopes.Count > 1 || item.Kind != "state") ? StateSourceCompatibility.PartiallyEditable : StateSourceCompatibility.Editable;
    public string NewLine => Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    public StateNode Node(string id) => Nodes.SingleOrDefault(item => item.Id == id) ?? throw new ArgumentException("Select an existing state.");
    public StateTransition Transition(string identity) => Transitions.SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select an exact transition occurrence.");
    public string RendererId(string identity) => PseudoNodes.FirstOrDefault(item => item.Identity == identity)?.RendererId ?? identity;
}

public abstract record StateEditOperation;
public sealed record AddStateNode(string Id, string Label, string Kind = "state") : StateEditOperation;
public sealed record UpdateStateNode(string Id, string NewId, string Label) : StateEditOperation;
public sealed record DeleteStateNode(string Id) : StateEditOperation;
public sealed record DuplicateStateNode(string Id, string NewId) : StateEditOperation;
public sealed record AddStateTransition(string Source, string Destination, string Label = "") : StateEditOperation;
public sealed record UpdateStateTransition(string Identity, string Source, string Destination, string Label) : StateEditOperation;
public sealed record DeleteStateTransition(string Identity) : StateEditOperation;
public sealed record UpdateStateGroupLabel(string Id, string Label) : StateEditOperation;
public sealed record StateEditPlan(IReadOnlyList<MermaidDocumentPatch> Patches, IReadOnlyList<string> Identities);
public sealed record StateEditResult(bool Success, string? Error);
public sealed record StateOperationCapability(bool Allowed, string Reason);
