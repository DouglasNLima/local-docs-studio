using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.Sequence;

/// <summary>Plans expected-text patches against the exact recognised occurrence; never serialises a diagram.</summary>
public sealed class SequenceTransformationService
{
    public SequenceEditPlan Plan(SequenceDiagramModel model, SequenceEditOperation operation)
    {
        if (!model.CanEdit) throw new ArgumentException(string.Join(" ", model.Diagnostics));
        var patches = new List<MermaidDocumentPatch>(); var identities = model.Statements.Select(item => item.Identity).ToList();
        var source = model.Source;
        void Patch(MermaidSourceSpan span, string replacement) => patches.Add(new(span, span.Read(source), replacement));
        void Token(SequenceStatement statement, string name, string replacement) => Patch(statement.Tokens[name], replacement);
        void Participant(string id) { if (model.Participants.All(item => item.Id != id)) throw new ArgumentException("Choose an existing participant. Add implicit participants explicitly before changing their references."); }
        void ExplicitParticipants() { if (model.Participants.Any(item => item.Declaration is null)) throw new ArgumentException("Declare all implicit participants in source before changing interaction order or endpoints."); }
        string Label(string value) => SequenceSourceParser.EncodeLabel(value);
        void Arrow(string arrow) { if (!SequenceSourceParser.Arrows.Contains(arrow)) throw new ArgumentException("Choose a renderer-supported message arrow."); }
        void Identifier(string id) { if (!SequenceSourceParser.ValidId(id)) throw new ArgumentException("Use a participant ID containing letters, digits and underscores, beginning with a letter or underscore."); }
        void Insert(string text, string? anchor, bool before)
        {
            SequenceStatement? target = anchor is null ? null : model.Statement(anchor);
            if (target is not null && target.Kind is not (SequenceStatementKind.Message or SequenceStatementKind.Note)) throw new ArgumentException("Choose a message or note as the insertion position.");
            if (target?.Depth > 0) throw new ArgumentException("Insertion inside structural blocks is protected. Use Edit source.");
            if (target?.Kind == SequenceStatementKind.Message && !before && model.Activations.Any(item => item.MessageIdentity == target.Identity && item.Standalone))
                throw new ArgumentException("Insert after the standalone activation through Edit source, or choose another interaction.");
            var offset = target is null ? source.Length : before ? target.Line.Start : target.Line.End;
            var prefix = offset > 0 && source[offset - 1] != '\n' ? model.NewLine : "";
            Patch(new(offset, 0), prefix + "    " + text + model.NewLine);
            identities.Insert(target is null ? identities.Count : identities.IndexOf(target.Identity) + (before ? 0 : 1), Guid.NewGuid().ToString("N"));
        }
        void Remove(SequenceStatement target)
        { Patch(target.Line, ""); identities.Remove(target.Identity); }
        void Swap(SequenceStatement first, SequenceStatement second)
        {
            Patch(first.Span, second.Span.Read(source)); Patch(second.Span, first.Span.Read(source));
            var a = identities.IndexOf(first.Identity); var b = identities.IndexOf(second.Identity);
            (identities[a], identities[b]) = (identities[b], identities[a]);
        }
        switch (operation)
        {
            case AddSequenceParticipant add:
                Identifier(add.Id); if (model.Participants.Any(item => item.Id == add.Id)) throw new ArgumentException("That participant ID already exists.");
                var declaration = "    " + (add.Actor ? "actor " : "participant ") + add.Id + " as " + Label(add.Label) + model.NewLine;
                var last = model.Statements.TakeWhile(item => item.Kind is SequenceStatementKind.Header or SequenceStatementKind.Participant).Last();
                Patch(new(last.Line.End, 0), (last.Line.End > 0 && source[last.Line.End - 1] != '\n' ? model.NewLine : "") + declaration);
                identities.Insert(identities.IndexOf(last.Identity) + 1, Guid.NewGuid().ToString("N")); break;
            case UpdateSequenceParticipant update:
                Identifier(update.NewId); var participant = model.Participants.SingleOrDefault(item => item.Id == update.Id) ?? throw new ArgumentException("Select a participant.");
                var statement = participant.Declaration ?? throw new ArgumentException("Implicit participants are inspectable. Declare this participant explicitly in source first.");
                if (update.NewId != update.Id && model.Participants.Any(item => item.Id == update.NewId)) throw new ArgumentException("That participant ID already exists.");
                Patch(statement.Span, (update.Actor ? "actor " : "participant ") + update.NewId + " as " + Label(update.Label));
                if (update.NewId != update.Id)
                    foreach (var reference in model.Statements.Where(item => item.Identity != statement.Identity))
                        foreach (var token in new[] { "from", "to", "first", "second", "id" })
                            if (reference.Read(source, token) == update.Id) Token(reference, token, update.NewId);
                break;
            case DeleteSequenceParticipant delete:
                var removing = model.Participants.SingleOrDefault(item => item.Id == delete.Id) ?? throw new ArgumentException("Select a participant.");
                if (removing.Declaration is null || model.Messages.Any(item => item.Sender == delete.Id || item.Recipient == delete.Id)
                    || model.Notes.Any(item => item.Participants.Contains(delete.Id)) || model.Activations.Any(item => item.Participant == delete.Id))
                    throw new ArgumentException("Remove this participant's messages, notes and activations first. Implicit participants require Edit source.");
                Remove(removing.Declaration); break;
            case MoveSequenceParticipant move:
                ExplicitParticipants(); var ordered = model.Participants.ToArray(); var position = Array.FindIndex(ordered, item => item.Id == move.Id);
                var next = position + Math.Sign(move.Direction);
                if (position < 0 || next < 0 || next >= ordered.Length || next == position) throw new ArgumentException("The participant is already at this end of the sequence.");
                var left = ordered[position].Declaration!; var right = ordered[next].Declaration!;
                if (left.Depth != 0 || right.Depth != 0 || model.Statements.Take(Math.Max(identities.IndexOf(left.Identity), identities.IndexOf(right.Identity)) + 1)
                    .Any(item => item.Kind is not (SequenceStatementKind.Header or SequenceStatementKind.Participant)))
                    throw new ArgumentException("Reordering requires a contiguous explicit participant declaration section.");
                Swap(left, right); break;
            case UpdateSequenceMessage update:
                Participant(update.Sender); Participant(update.Recipient); Arrow(update.Arrow);
                var message = model.Messages.SingleOrDefault(item => item.Identity == update.Identity) ?? throw new ArgumentException("Select a message.");
                if (message.Sender != update.Sender || message.Recipient != update.Recipient) { ExplicitParticipants(); if (model.Activations.Any(item => item.MessageIdentity == update.Identity)) throw new ArgumentException("Keep activation-triggering message endpoints unchanged, or edit their lifecycle in source."); }
                if (update.Arrow.StartsWith("<<", StringComparison.Ordinal) && message.Marker.Length > 0) throw new ArgumentException("Bidirectional activation shorthand is protected.");
                Token(message.Statement, "from", update.Sender); Token(message.Statement, "to", update.Recipient);
                Token(message.Statement, "arrow", update.Arrow); Token(message.Statement, "label", Label(update.Text)); break;
            case AddSequenceMessage add:
                Participant(add.Sender); Participant(add.Recipient); Arrow(add.Arrow);
                Insert(add.Sender + add.Arrow + add.Recipient + ": " + Label(add.Text), add.Anchor, add.Before); break;
            case DuplicateSequenceMessage duplicate:
                var original = model.Messages.SingleOrDefault(item => item.Identity == duplicate.Identity) ?? throw new ArgumentException("Select a message.");
                if (model.Activations.Any(item => item.MessageIdentity == original.Identity)) throw new ArgumentException("Activation-triggering messages cannot be duplicated safely.");
                return Plan(model, new AddSequenceMessage(original.Sender, original.Recipient, original.Text, original.Arrow, original.Identity));
            case AddSequenceNote add:
                ExplicitParticipants(); Participant(add.First); if (!string.IsNullOrEmpty(add.Second)) Participant(add.Second);
                Insert(Note(add.Placement, add.First, add.Second, Label(add.Text)), add.Anchor, add.Before); break;
            case UpdateSequenceNote update:
                Participant(update.First); if (!string.IsNullOrEmpty(update.Second)) Participant(update.Second);
                var note = model.Notes.SingleOrDefault(item => item.Identity == update.Identity) ?? throw new ArgumentException("Select a note.");
                if (note.Participants[0] != update.First || note.Participants.ElementAtOrDefault(1) != (string.IsNullOrEmpty(update.Second) ? null : update.Second)) ExplicitParticipants();
                Patch(note.Statement.Span, Note(update.Placement, update.First, update.Second, Label(update.Text))); break;
            case DeleteSequenceInteraction delete:
                var interaction = model.Statement(delete.Identity);
                if (interaction.Kind is not (SequenceStatementKind.Message or SequenceStatementKind.Note)) throw new ArgumentException("Select a message or note.");
                if (interaction.Depth > 0 || model.Activations.Any(item => item.MessageIdentity == interaction.Identity)) throw new ArgumentException("Block and activation dependencies protect this interaction from deletion. Use Edit source.");
                ExplicitParticipants(); Remove(interaction); break;
            case MoveSequenceMessage move:
                ExplicitParticipants(); var moving = model.Statement(move.Identity); var from = identities.IndexOf(move.Identity); var to = from + Math.Sign(move.Direction);
                if (to < 0 || to >= identities.Count || to == from || moving.Kind != SequenceStatementKind.Message) throw new ArgumentException("Select a message with an adjacent interaction.");
                var neighbour = model.Statements[to];
                if (neighbour.Kind != SequenceStatementKind.Message || moving.Depth != 0 || neighbour.Depth != 0 || model.Activations.Count > 0)
                    throw new ArgumentException("Only adjacent top-level messages without notes, blocks or activation dependencies can move.");
                Swap(moving, neighbour); break;
            case SetSequenceActivation activation:
                if (activation.Marker is not ("" or "+" or "-")) throw new ArgumentException("Choose no marker, activate (+) or deactivate (-).");
                var trigger = model.Messages.SingleOrDefault(item => item.Identity == activation.Identity) ?? throw new ArgumentException("Select the triggering message.");
                if (trigger.Statement.Depth != 0 || trigger.Arrow.StartsWith("<<", StringComparison.Ordinal)) throw new ArgumentException("Block and bidirectional activation changes require Edit source.");
                var standalone = model.Activations.SingleOrDefault(item => item.MessageIdentity == activation.Identity && item.Standalone);
                if (standalone is not null)
                {
                    if (activation.Marker.Length == 0) Remove(standalone.Statement);
                    else
                    {
                        var actor = activation.Marker == "+" ? trigger.Recipient : trigger.Sender;
                        Token(standalone.Statement, "verb", activation.Marker == "+" ? "activate" : "deactivate"); Token(standalone.Statement, "id", actor);
                    }
                }
                else Token(trigger.Statement, "marker", activation.Marker);
                break;
            default: throw new ArgumentException("This Sequence operation is not supported.");
        }
        return new(patches.AsReadOnly(), identities.AsReadOnly());
    }
    private static string Note(string placement, string first, string? second, string label)
    {
        if (placement is not ("left of" or "right of" or "over") || !string.IsNullOrEmpty(second) && placement != "over")
            throw new ArgumentException("Choose left of, right of, or over. Only over notes may span two participants.");
        return "Note " + placement + " " + first + (string.IsNullOrEmpty(second) ? "" : "," + second) + ": " + label;
    }
}
