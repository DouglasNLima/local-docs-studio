using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class SequenceVisualTests
{
    private const string Simple = "sequenceDiagram\r\n    %% Retain this comment\r\n    participant A as Author\r\n    actor B as Studio\r\n    A->>B: Request\r\n    B-->>A: Response\r\n";
    private static async Task Execute(SequenceVisualDraft draft, SequenceEditOperation operation)
    { var result = await draft.ExecuteAsync(operation); Assert.True(result.Success, result.Error); }

    [Theory]
    [InlineData("->>")] [InlineData("-->>")] [InlineData("->")] [InlineData("-->")]
    [InlineData("-)")] [InlineData("--)")] [InlineData("-x")] [InlineData("--x")]
    [InlineData("<<->>")] [InlineData("<<-->>")]
    public async Task Pinned_arrows_have_verified_canonical_semantics(string arrow)
    {
        var draft = new SequenceVisualDraft(Simple);
        await Execute(draft, new UpdateSequenceMessage(draft.Model.Messages[0].Identity, "A", "B", "Changed", arrow));
        Assert.Contains("A" + arrow + "B: Changed", draft.Source);
    }

    [Fact]
    public async Task Repeated_messages_keep_occurrence_identity_through_edit_move_and_history()
    {
        var source = Simple + "    A->>B: Request\r\n    A->>B: Request\r\n";
        var draft = new SequenceVisualDraft(source); var identity = draft.Model.Messages[3].Identity;
        draft.Select(identity);
        await Execute(draft, new UpdateSequenceMessage(identity, "A", "B", "Third request", "->>"));
        Assert.Equal(source[..source.LastIndexOf("Request", StringComparison.Ordinal)] + "Third request\r\n", draft.Source);
        Assert.Equal(identity, draft.Model.Messages[3].Identity);
        await Execute(draft, new MoveSequenceMessage(identity, -1)); Assert.Equal(identity, draft.Model.Messages[2].Identity);
        draft.Undo(); Assert.Equal(identity, draft.Model.Messages[3].Identity); draft.Undo(); Assert.Equal(source, draft.Source);
        Assert.Equal(identity, draft.Selection); draft.Redo(); Assert.Contains("Third request", draft.Source);
    }

    [Fact]
    public async Task Participant_rename_updates_all_known_references_without_rewriting_comments_or_standalone_activation()
    {
        var source = Simple.Replace("    B-->>A: Response", "    activate B\r\n    Note over A,B: Local files\r\n    B-->>A: Response\r\n    deactivate B", StringComparison.Ordinal);
        var draft = new SequenceVisualDraft(source);
        await Execute(draft, new UpdateSequenceParticipant("B", "Studio", "Lens Docs Studio", false));
        Assert.Contains("activate Studio\r\n", draft.Source); Assert.Contains("deactivate Studio\r\n", draft.Source);
        Assert.Contains("Note over A,Studio: Local files", draft.Source); Assert.Contains("%% Retain this comment", draft.Source);
        Assert.Contains("A->>Studio: Request", draft.Source); Assert.DoesNotContain("->>+", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }

    [Fact]
    public async Task Participant_creation_type_reorder_and_dependency_checked_deletion_work()
    {
        var draft = new SequenceVisualDraft(Simple); await Execute(draft, new AddSequenceParticipant("C", "Workspace", true));
        await Execute(draft, new MoveSequenceParticipant("C", -1)); Assert.Equal(new[] { "A", "C", "B" }, draft.Model.Participants.Select(item => item.Id));
        await Execute(draft, new DeleteSequenceParticipant("C"));
        var source = draft.Source; var history = draft.HistoryIdentity;
        Assert.False((await draft.ExecuteAsync(new DeleteSequenceParticipant("A"))).Success);
        Assert.Equal(source, draft.Source); Assert.Equal(history, draft.HistoryIdentity);
    }

    [Theory]
    [InlineData("left of", null)] [InlineData("right of", null)] [InlineData("over", null)] [InlineData("over", "B")]
    public async Task Notes_preserve_their_exact_interaction_position(string placement, string? second)
    {
        var draft = new SequenceVisualDraft(Simple); var first = draft.Model.Messages[0].Identity;
        await Execute(draft, new AddSequenceNote(placement, "A", second, "Documents remain local", first, true));
        Assert.True(draft.Source.IndexOf("Note", StringComparison.Ordinal) < draft.Source.IndexOf("A->>", StringComparison.Ordinal));
        var note = Assert.Single(draft.Model.Notes); var before = draft.Source;
        await Execute(draft, new UpdateSequenceNote(note.Identity, placement, "A", second, "Local\nUnicode café"));
        Assert.Equal(before.Replace("Documents remain local", "Local<br/>Unicode café", StringComparison.Ordinal), draft.Source);
        await Execute(draft, new DeleteSequenceInteraction(note.Identity)); Assert.Empty(draft.Model.Notes);
    }

    [Fact]
    public async Task Standalone_activation_edits_preserve_the_original_representation()
    {
        var source = Simple.Replace("    B-->>A: Response", "    activate B\r\n    B-->>A: Response\r\n    deactivate B", StringComparison.Ordinal);
        var draft = new SequenceVisualDraft(source); var trigger = draft.Model.Messages[1].Identity;
        await Execute(draft, new UpdateSequenceMessage(draft.Model.Messages[0].Identity, "A", "B", "Open", "->>"));
        Assert.Contains("    activate B\r\n", draft.Source);
        await Execute(draft, new SetSequenceActivation(trigger, "")); Assert.DoesNotContain("deactivate B", draft.Source);
        draft.Undo(); Assert.Contains("deactivate B", draft.Source); draft.Redo(); Assert.DoesNotContain("deactivate B", draft.Source);
    }

    [Fact]
    public async Task Marker_addition_and_removal_validate_complete_activation_lifecycles()
    {
        var draft = new SequenceVisualDraft(Simple); var first = draft.Model.Messages[0].Identity; var last = draft.Model.Messages[1].Identity;
        await Execute(draft, new SetSequenceActivation(first, "+")); await Execute(draft, new SetSequenceActivation(last, "-"));
        var source = draft.Source; var history = draft.HistoryIdentity;
        Assert.False((await draft.ExecuteAsync(new SetSequenceActivation(first, ""))).Success);
        Assert.Equal(source, draft.Source); Assert.Equal(history, draft.HistoryIdentity);
        await Execute(draft, new SetSequenceActivation(last, "")); await Execute(draft, new SetSequenceActivation(first, ""));
        Assert.Equal(Simple, draft.Source);
    }

    [Theory]
    [InlineData("loop", "retries")] [InlineData("alt", "online")] [InlineData("opt", "cache")]
    [InlineData("par", "requests")] [InlineData("critical", "write")] [InlineData("break", "failure")] [InlineData("rect", "rgb(240,240,240)")]
    public async Task Structural_blocks_render_and_preserve_labels_but_reject_message_moves(string type, string label)
    {
        var source = Simple.Replace("    A->>B: Request", "    " + type + " " + label + "\r\n    A->>B: Request\r\n    end", StringComparison.Ordinal);
        var draft = new SequenceVisualDraft(source); var identity = draft.Model.Messages[0].Identity;
        var validation = await draft.ValidateAsync(); Assert.True(validation.Success, validation.Error);
        await Execute(draft, new UpdateSequenceMessage(identity, "A", "B", "Changed", "->>"));
        Assert.Contains(type + " " + label + "\r\n", draft.Source);
        Assert.False((await draft.ExecuteAsync(new MoveSequenceMessage(identity, 1))).Success);
        Assert.False((await draft.ExecuteAsync(new DeleteSequenceInteraction(identity))).Success);
    }

    [Theory]
    [InlineData("box red Services\nend")] [InlineData("create participant C")] [InlineData("destroy B")]
    [InlineData("unknown A")] [InlineData("activate Missing")] [InlineData("deactivate B")]
    [InlineData("loop test")] [InlineData("end")]
    public async Task Unknown_or_ambiguous_dependencies_fail_closed_without_history_changes(string unsupported)
    {
        var source = Simple + unsupported + "\n"; var draft = new SequenceVisualDraft(source); var history = draft.HistoryIdentity;
        Assert.False((await draft.ExecuteAsync(new UpdateSequenceParticipant("A", "Renamed", "Author", false))).Success);
        Assert.Equal(source, draft.Source); Assert.Equal(history, draft.HistoryIdentity); Assert.False(draft.CanUndo);
    }

    [Fact]
    public async Task Implicit_participants_allow_local_label_edits_but_protect_order_and_new_endpoints()
    {
        var draft = new SequenceVisualDraft("sequenceDiagram\n A->>B: Request\n B->>A: Response\n");
        Assert.All(draft.Model.Participants, item => Assert.Null(item.Declaration));
        await Execute(draft, new UpdateSequenceMessage(draft.Model.Messages[0].Identity, "A", "B", "Open", "->>"));
        Assert.False((await draft.ExecuteAsync(new MoveSequenceMessage(draft.Model.Messages[0].Identity, 1))).Success);
    }

    [Fact]
    public async Task Message_creation_duplication_deletion_and_redo_branching_work()
    {
        var draft = new SequenceVisualDraft(Simple); await Execute(draft, new AddSequenceMessage("A", "A", "Self\nmessage"));
        var identity = draft.Model.Messages[^1].Identity; await Execute(draft, new DuplicateSequenceMessage(identity));
        Assert.Equal(4, draft.Model.Messages.Count); Assert.NotEqual(identity, draft.Model.Messages[^1].Identity);
        await Execute(draft, new DeleteSequenceInteraction(identity)); Assert.Equal(3, draft.Model.Messages.Count);
        draft.Undo(); Assert.True(draft.CanRedo); await Execute(draft, new AddSequenceParticipant("C", "Other")); Assert.False(draft.CanRedo);
    }

    [Theory]
    [InlineData("\u0001")] [InlineData("<script>bad</script>")] [InlineData("%% comment")] [InlineData("")]
    public async Task Invalid_labels_reject_without_mutation(string text)
    {
        var draft = new SequenceVisualDraft(Simple);
        Assert.False((await draft.ExecuteAsync(new UpdateSequenceMessage(draft.Model.Messages[0].Identity, "A", "B", text, "->>"))).Success);
        Assert.Equal(Simple, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }

    [Fact]
    public async Task Cancellation_does_not_create_a_history_step()
    {
        var draft = new SequenceVisualDraft(Simple); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.False((await draft.ExecuteAsync(new AddSequenceMessage("A", "B", "Cancelled"), cancellation.Token)).Success);
        Assert.Equal(Simple, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }

    [Fact]
    public async Task Mixed_line_endings_unicode_and_autonumber_preserve_unrelated_source()
    {
        var source = Simple.Replace("    A->>B", "    autonumber 5 2\n    A->>B", StringComparison.Ordinal) + "    Note over A,B: café<br/>本地\n";
        var draft = new SequenceVisualDraft(source);
        await Execute(draft, new UpdateSequenceMessage(draft.Model.Messages[1].Identity, "B", "A", "Réponse", "-->>"));
        Assert.Equal(source.Replace("Response", "Réponse", StringComparison.Ordinal), draft.Source);
        var render = new DiagramService().Render(draft.Source); Assert.Contains("7. Réponse", render.Svg);
    }
}
