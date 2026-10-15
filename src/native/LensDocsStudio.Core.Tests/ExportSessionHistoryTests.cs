using LensDocsStudio.Core.Exports;

namespace LensDocsStudio.Core.Tests;

public sealed class ExportSessionHistoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-exports-" + Guid.NewGuid().ToString("N"));
    public ExportSessionHistoryTests() => Directory.CreateDirectory(root);
    private SavedExport Save(string name, byte[]? content = null)
    {
        var bytes = content ?? [1, 2, 3]; var path = Path.Combine(root, name); File.WriteAllBytes(path, bytes);
        return SavedExport.FromSavedBytes(path, bytes);
    }

    [Theory]
    [InlineData("document.docx")][InlineData("document.pdf")][InlineData("document.html")][InlineData("bundle.zip")]
    [InlineData("table.csv")][InlineData("diagram.svg")][InlineData("diagram.png")][InlineData("settings.json")]
    public async Task Only_saved_outputs_have_verifiable_session_receipts(string name)
    {
        var history = new ExportSessionHistory(); var saved = Save(name); history.Remember(saved);
        await history.VerifyAsync(saved);
        Assert.Equal(name, saved.Name); Assert.Equal(Path.Combine(root, name), saved.Path);
        Assert.Single(history.Entries);
        Assert.Empty(new ExportSessionHistory().Entries);
    }

    [Fact]
    public async Task Missing_changed_and_superseded_outputs_cannot_use_old_actions()
    {
        var history = new ExportSessionHistory(); var saved = Save("output.docx"); history.Remember(saved);
        File.WriteAllBytes(saved.Path, [3, 2, 1]);
        await Assert.ThrowsAsync<IOException>(() => history.VerifyAsync(saved));
        var replacement = Save("output.docx", [3, 2, 1]); history.Remember(replacement);
        await Assert.ThrowsAsync<IOException>(() => history.VerifyAsync(saved));
        await history.VerifyAsync(replacement);
        File.Delete(saved.Path);
        await Assert.ThrowsAnyAsync<IOException>(() => history.VerifyAsync(replacement));
    }

    [Fact]
    public async Task Sequential_exports_are_bounded_and_evicted_actions_are_rejected()
    {
        var history = new ExportSessionHistory(); var first = Save("first.docx"); history.Remember(first);
        for (var index = 0; index < ExportSessionHistory.MaximumEntries + 2; index++) history.Remember(Save($"output-{index}.pdf"));
        Assert.Equal(ExportSessionHistory.MaximumEntries, history.Entries.Count);
        Assert.Equal("output-11.pdf", history.Entries[0].Name);
        await Assert.ThrowsAsync<IOException>(() => history.VerifyAsync(first));
    }

    [Fact]
    public async Task Failed_and_cancelled_writes_do_not_create_success_history()
    {
        var history = new ExportSessionHistory(); var path = Path.Combine(root, "cancelled.docx");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExportFileStore(Path.Combine(root, "recovery")).WriteAsync(path, [1], null, cancellation.Token));
        Assert.False(File.Exists(path)); Assert.Empty(history.Entries);
        Assert.Throws<IOException>(() => SavedExport.FromSavedBytes(path, [1]));
        var saved = Save("existing.docx");
        await Assert.ThrowsAsync<IOException>(() => new ExportFileStore(Path.Combine(root, "recovery")).WriteAsync(saved.Path, [2], null));
        Assert.Empty(history.Entries); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(saved.Path));
    }

    [Theory]
    [InlineData("file.exe")][InlineData("file.cmd")][InlineData("file.lnk")][InlineData("file.url")]
    public void Shell_command_and_shortcut_outputs_are_not_actionable(string name) => Assert.Throws<IOException>(() => Save(name));

    public void Dispose()
    {
        // Each test owns this generated directory, including its local write-recovery journal.
        if (!Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "lens-exports-"), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The generated export test directory escaped its temporary root.");
        Directory.Delete(root, true);
    }
}
