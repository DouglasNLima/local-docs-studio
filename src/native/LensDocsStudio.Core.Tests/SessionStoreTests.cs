using System.Text;
using System.Text.Json;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-session-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public SessionStoreTests() => Directory.CreateDirectory(root);
    private string Current => Path.Combine(root, "session.json");
    private static SessionState State(string text) => new(1, null, null,
        [new(Guid.NewGuid(), null, text, "original-fingerprint", true, 4, 20, "Original\r\n", DisplayName: "Café")]);

    [Fact]
    public async Task Multiple_documents_restore_current_text_baselines_and_identity_from_a_fresh_store()
    {
        var documents = Enumerable.Range(0, 9).Select(index => State(string.Concat(Enumerable.Repeat($"Café {index} こんにちは 🌍\r\n", 100))).Documents.Single()).ToArray();
        var state = new SessionState(1, root, documents[4].Id, documents);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        // The same exact byte budget is accepted by both ends, including JSON escaping.
        await new SessionStore(root, bytes.Length).WriteAsync("session.json", state);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Current));
        var restored = await new SessionStore(root, bytes.Length).ReadAsync<SessionState>("session.json");
        Assert.Equal(state.Workspace, restored!.Workspace);
        Assert.Equal(state.ActiveDocument, restored.ActiveDocument);
        Assert.Equal(documents, restored.Documents);
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("Café こんにちは 🌍\r\n\t\"\\")]
    public async Task A_write_one_byte_over_the_read_budget_never_replaces_either_recovery_version(string text)
    {
        var tooLarge = State(string.Concat(Enumerable.Repeat(text, 2000)));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(tooLarge, Options);
        var store = new SessionStore(root, bytes.Length - 1);
        await store.WriteAsync("session.json", State("Earlier"));
        await store.WriteAsync("session.json", State("Current"));
        var before = await File.ReadAllBytesAsync(Current);
        var backup = await File.ReadAllBytesAsync(Current + ".bak");
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("session.json", tooLarge));
        Assert.Equal(before, await File.ReadAllBytesAsync(Current));
        Assert.Equal(backup, await File.ReadAllBytesAsync(Current + ".bak"));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        var recovered = await new SessionStore(root, bytes.Length - 1).ReadAsync<SessionState>("session.json");
        Assert.Equal("Current", Assert.Single(recovered!.Documents).Text);
    }

    [Fact]
    public async Task Partial_streaming_output_is_removed_when_a_later_document_exceeds_the_budget()
    {
        var store = new SessionStore(root, 64000);
        await store.WriteAsync("session.json", State("Keep this draft"));
        var before = await File.ReadAllBytesAsync(Current);
        var documents = Enumerable.Range(0, 20).Select(_ => State(new string('x', 8000)).Documents.Single()).ToArray();
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("session.json", new SessionState(1, null, null, documents)));
        Assert.Equal(before, await File.ReadAllBytesAsync(Current));
        Assert.False(File.Exists(Current + ".bak"));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Fact]
    public async Task Oversized_legacy_state_is_retained_before_falling_back_and_rotating_the_backup()
    {
        var store = new SessionStore(root, 2048);
        var previous = State("Recover previous draft");
        await store.WriteAsync("session.json", previous);
        await store.WriteAsync("session.json", State("Current"));
        var legacy = JsonSerializer.SerializeToUtf8Bytes(State(new string('x', 4096)), Options);
        await File.WriteAllBytesAsync(Current, legacy);
        var restored = await store.ReadAsync<SessionState>("session.json");
        Assert.Equal(previous.Documents, restored!.Documents);
        Assert.Contains("size limit", Assert.Single(store.ReadWarnings));
        await store.WriteAsync("session.json", State("New state"));
        await store.WriteAsync("session.json", State("Rotate again"));
        Assert.Equal(legacy, await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(root, "*.unreadable-*.json"))));
    }

    [Fact]
    public async Task Both_oversized_versions_survive_a_fresh_session()
    {
        var first = new byte[4096]; Array.Fill(first, (byte)'x');
        var second = new byte[3000]; Array.Fill(second, (byte)'y');
        await File.WriteAllBytesAsync(Current, first);
        await File.WriteAllBytesAsync(Current + ".bak", second);
        var store = new SessionStore(root, 2048);
        Assert.Null(await store.ReadAsync<SessionState>("session.json"));
        await store.WriteAsync("session.json", State("New"));
        await store.WriteAsync("session.json", State("Newer"));
        var retained = Directory.GetFiles(root, "*.unreadable-*.json").Select(File.ReadAllBytes).ToArray();
        Assert.Equal(2, retained.Length);
        Assert.Contains(retained, bytes => bytes.SequenceEqual(first));
        Assert.Contains(retained, bytes => bytes.SequenceEqual(second));
    }

    [Fact]
    public async Task A_failed_read_prevents_later_autosave_until_a_successful_retry()
    {
        var store = new SessionStore(root);
        var state = State("Unread draft");
        await store.WriteAsync("session.json", state);
        var original = await File.ReadAllBytesAsync(Current);
        using (var held = new FileStream(Current, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => store.ReadAsync<SessionState>("session.json"));
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("session.json", State("Must not overwrite")));
        Assert.Equal(original, await File.ReadAllBytesAsync(Current));
        var restored = await store.ReadAsync<SessionState>("session.json");
        Assert.Equal(state.Documents, restored!.Documents);
        await store.WriteAsync("session.json", State("Reviewed after retry"));
        Assert.Equal(original, await File.ReadAllBytesAsync(Current + ".bak"));
    }

    [Fact]
    public async Task Existing_utf8_bom_state_remains_readable()
    {
        var state = State("Café 🌍\r\n");
        await File.WriteAllTextAsync(Current, JsonSerializer.Serialize(state, Options), new UTF8Encoding(true));
        var restored = await new SessionStore(root).ReadAsync<SessionState>("session.json");
        Assert.NotNull(restored);
        Assert.Equal(state.Documents, restored.Documents);
    }

    [Fact]
    public async Task Cancellation_preserves_both_existing_versions()
    {
        var store = new SessionStore(root);
        await store.WriteAsync("session.json", State("Previous"));
        await store.WriteAsync("session.json", State("Current"));
        var current = await File.ReadAllBytesAsync(Current);
        var backup = await File.ReadAllBytesAsync(Current + ".bak");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync("session.json", State("Cancelled"), cancellation.Token));
        Assert.Equal(current, await File.ReadAllBytesAsync(Current));
        Assert.Equal(backup, await File.ReadAllBytesAsync(Current + ".bak"));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(SessionStore.DefaultMaximumStateBytes + 1)]
    public void Invalid_recovery_budgets_are_rejected(long limit) => Assert.Throws<ArgumentOutOfRangeException>(() => new SessionStore(root, limit));

    public void Dispose() => Directory.Delete(root, true);
}
