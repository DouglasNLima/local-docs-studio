using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class NavigationTests
{
    [Fact] public void Navigation_retains_viewports_and_discards_the_forward_branch_after_a_new_visit()
    {
        var history = new NavigationHistory();
        var a = new NavigationLocation(Guid.NewGuid(), null, 0, 0);
        var b = new NavigationLocation(Guid.NewGuid(), null, 12, 240);
        history.Visit(a); history.UpdateCurrent(a with { CaretOffset = 8, VerticalOffset = 120 }); history.Visit(b);
        Assert.Equal(8, history.Back!.CaretOffset);
        Assert.False(history.CanGoForward);
        var destination = history.Back;
        // Looking up a destination does not mutate state: opening may fail.
        Assert.Equal(destination, history.Back);
        history.CompleteMove(false, destination!);
        Assert.Equal(b, history.Forward);
        history.Visit(a with { CaretOffset = 40 });
        Assert.False(history.CanGoForward);
        Assert.Equal(8, history.Back!.CaretOffset);
    }

    [Fact] public void Navigation_is_bounded_and_ignores_identical_visits()
    {
        var history = new NavigationHistory(2);
        var a = new NavigationLocation(Guid.NewGuid(), null, 0, 0);
        history.Visit(a); history.Visit(a);
        Assert.False(history.CanGoBack);
        history.Visit(a with { CaretOffset = 1 }); history.Visit(a with { CaretOffset = 2 });
        Assert.Equal(1, history.Back!.CaretOffset);
        history.CompleteMove(false, history.Back!);
        Assert.False(history.CanGoBack);
    }

    [Fact] public void Recent_paths_are_bounded_case_insensitive_and_require_absolute_paths()
    {
        var paths = Enumerable.Range(0, 30).Select(n => Path.Combine(Path.GetTempPath(), $"document-{n}.md"));
        var first = paths.First();
        var recent = RecentPaths.Touch(paths, first.ToUpperInvariant());
        Assert.Equal(20, recent.Length);
        Assert.Equal(first.ToUpperInvariant(), recent[0]);
        Assert.Single(recent, p => p.Equals(first, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(RecentPaths.Normalise(["relative.md", "", "\0"]));
    }
    [Fact] public void Renaming_a_document_updates_paths_in_previous_and_forward_history_entries()
    {
        var history = new NavigationHistory();
        var id = Guid.NewGuid();
        var first = new NavigationLocation(id, "old.md", 0, 0);
        history.Visit(first); history.Visit(new(Guid.NewGuid(), "other.md", 0, 0)); history.Visit(first with { CaretOffset = 8 });
        history.CompleteMove(false, history.Back!);
        history.RelocateDocument(id, "new.md");
        Assert.Equal("new.md", history.Back!.Path); Assert.Equal("new.md", history.Forward!.Path);
        Assert.Equal(8, history.Forward.CaretOffset);
    }

    [Fact] public void Document_links_encode_portable_paths_and_escape_labels()
    {
        var root = Path.Combine(Path.GetTempPath(), "lens-links-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        try
        {
            var target = Path.Combine(root, "Café [plan] #1.md");
            File.WriteAllText(target, "# A heading");
            var link = DocumentLinks.Create(new(root), Path.Combine(root, "docs", "index.md"), target, "A [plan]", "a-heading");
            Assert.Equal("[A \\[plan\\]](../Caf%C3%A9%20%5Bplan%5D%20%231.md#a-heading)", link);
            var reference = link[(link.IndexOf("](", StringComparison.Ordinal) + 2)..^1];
            Assert.Equal(target, new WorkspacePath(root).ResolveLink(Path.Combine(root, "docs", "index.md"), reference));
            Assert.Throws<IOException>(() => DocumentLinks.Create(new(root), Path.Combine(root, "index.md"), Path.Combine(root, "..", "outside.md")));
        }
        finally { Directory.Delete(root, true); }
    }
}
