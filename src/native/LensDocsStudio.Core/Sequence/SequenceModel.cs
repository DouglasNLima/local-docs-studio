using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.Sequence;

public enum SequenceStatementKind { Header, Participant, Message, Note, Activation, Block, Divider, End, Autonumber, Protected }
public sealed record SequenceStatement(string Identity, SequenceStatementKind Kind, MermaidSourceSpan Span,
    MermaidSourceSpan Line, IReadOnlyDictionary<string, MermaidSourceSpan> Tokens, int Depth)
{
    public string Read(string source, string token) => Tokens.TryGetValue(token, out var span) ? span.Read(source) : "";
}
public sealed record SequenceParticipant(string Id, string Label, bool Actor, SequenceStatement? Declaration);
public sealed record SequenceMessage(string Identity, string Sender, string Recipient, string Arrow, string Text,
    string Marker, SequenceStatement Statement);
public sealed record SequenceNote(string Identity, string Placement, IReadOnlyList<string> Participants, string Text, SequenceStatement Statement);
public sealed record SequenceActivation(string Identity, string Participant, bool Activate, string MessageIdentity, SequenceStatement Statement, bool Standalone);
public sealed record SequenceDiagramModel(string Source, IReadOnlyList<SequenceStatement> Statements,
    IReadOnlyList<SequenceParticipant> Participants, IReadOnlyList<SequenceMessage> Messages, IReadOnlyList<SequenceNote> Notes,
    IReadOnlyList<SequenceActivation> Activations, IReadOnlyList<string> Diagnostics)
{
    public bool CanEdit => Diagnostics.Count == 0;
    public string NewLine => Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    public SequenceStatement Statement(string identity) => Statements.SingleOrDefault(item => item.Identity == identity)
        ?? throw new ArgumentException("Select an existing Sequence element.");
}

public abstract record SequenceEditOperation;
public sealed record AddSequenceParticipant(string Id, string Label, bool Actor = false) : SequenceEditOperation;
public sealed record UpdateSequenceParticipant(string Id, string NewId, string Label, bool Actor) : SequenceEditOperation;
public sealed record DeleteSequenceParticipant(string Id) : SequenceEditOperation;
public sealed record MoveSequenceParticipant(string Id, int Direction) : SequenceEditOperation;
public sealed record AddSequenceMessage(string Sender, string Recipient, string Text, string Arrow = "->>", string? Anchor = null, bool Before = false) : SequenceEditOperation;
public sealed record UpdateSequenceMessage(string Identity, string Sender, string Recipient, string Text, string Arrow) : SequenceEditOperation;
public sealed record AddSequenceNote(string Placement, string First, string? Second, string Text, string? Anchor = null, bool Before = false) : SequenceEditOperation;
public sealed record UpdateSequenceNote(string Identity, string Placement, string First, string? Second, string Text) : SequenceEditOperation;
public sealed record DeleteSequenceInteraction(string Identity) : SequenceEditOperation;
public sealed record DuplicateSequenceMessage(string Identity) : SequenceEditOperation;
public sealed record MoveSequenceMessage(string Identity, int Direction) : SequenceEditOperation;
public sealed record SetSequenceActivation(string Identity, string Marker) : SequenceEditOperation;
public sealed record SequenceEditPlan(IReadOnlyList<MermaidDocumentPatch> Patches, IReadOnlyList<string> Identities);
public sealed record SequenceEditResult(bool Success, string? Error);
