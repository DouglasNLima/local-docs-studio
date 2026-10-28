using System.Text.Json;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

[Collection("Complex ER")]
public sealed class ArrangementTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-arrangement-" + Guid.NewGuid().ToString("N"));
    public ArrangementTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static ArrangementEngine Engine(string source) => new(new DiagramService().Render(source));

    [Theory]
    [InlineData("erDiagram\n A ||--o{ B : owns\n", "A")]
    [InlineData("flowchart TD\n A[Start] --> B[Finish]\n", "A")]
    public void Move_has_final_routes_preserves_source_and_keeps_canonical_export(string source, string id)
    {
        var engine = Engine(source); var canonical = engine.Canonical.Render.Svg;
        var positions = engine.Canonical.State.Positions.ToDictionary(p => p.Key, p => p.Value);
        positions[id] = new(80, 80); positions["B"] = new(600, 420);
        var moved = engine.Arrange(new(positions, ArrangementStrategy.Manual));
        Assert.Equal(source, moved.Render.Source); Assert.NotEqual(canonical, moved.Render.Svg);
        Assert.Equal(canonical, engine.Canonical.Render.Svg); Assert.Equal(0, moved.Quality.BodyIntersections); Assert.Equal(0, moved.Quality.LabelBodyOverlaps);
        Assert.Equal(moved.Render.Svg, engine.Arrange(moved.State).Render.Svg);
    }
    [Fact]
    public void Layout_history_is_independent_and_rejects_stale_generation()
    {
        var engine = Engine("flowchart TD\n A --> B"); var session = new ArrangementSession(engine.Canonical);
        var pinned = engine.Arrange(session.Current.State.TogglePin("A"));
        Assert.True(session.Accept(pinned, 0)); Assert.True(session.IsDirty); Assert.False(session.Accept(engine.Canonical, 0));
        session.MarkSaved(); Assert.False(session.IsDirty); session.Undo(); Assert.True(session.IsDirty); session.Redo(); Assert.False(session.IsDirty);
        Assert.True(session.Current.State.Positions["A"].Pinned); Assert.Equal(engine.Canonical.Render.Source, session.Current.Render.Source);
    }
    [Theory]
    [InlineData("flowchart TD\n A --> B\n A -.-> B\n")]
    [InlineData("flowchart TD\n A --> B\n B -.-> A\n")]
    [InlineData("flowchart TD\n A --> A\n A -.-> A\n A --> B\n")]
    public void Parallel_and_reciprocal_flowchart_occurrences_keep_distinct_visible_routes(string source)
    {
        var engine = Engine(source);
        var state = engine.Canonical.State.Move("A", 100, 100).Move("B", 100, 600);
        var snapshot = engine.Arrange(state);
        var routes = snapshot.Render.FlowchartLayout!.Edges.Take(2).Select(e => string.Join(";", e.Points.Select(p => $"{p.X:R},{p.Y:R}"))).ToArray();
        Assert.NotEqual(routes[0], routes[1]);
        Assert.NotEqual(routes[0], string.Join(";", snapshot.Render.FlowchartLayout.Edges[1].Points.Reverse().Select(p => $"{p.X:R},{p.Y:R}")));
        Assert.Equal(0, snapshot.Quality.BodyIntersections); Assert.Equal(source, snapshot.Render.Source);
        Assert.Equal(snapshot.Render.Svg, engine.Arrange(snapshot.State).Render.Svg);
    }
    [Fact]
    public async Task Existing_sidecar_cannot_commit_while_an_external_writer_owns_it()
    {
        var store = new ArrangementStore(new(root)); var expected = await store.SaveAsync(new(1, [Record()]), await store.ReadAsync());
        var path = Path.Combine(root, ArrangementStore.RelativePath); var bytes = await File.ReadAllBytesAsync(path);
        await using (var external = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(expected.File, expected));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        var saved = await store.SaveAsync(expected.File, expected);
        Assert.Equal(saved.Fingerprint, (await store.ReadAsync()).Fingerprint);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.pending-*.json"));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path + ".bak"));
    }
    [Fact]
    public void Overlap_and_unknown_nodes_reject_without_changing_the_accepted_snapshot()
    {
        var engine = Engine("flowchart TD\n A --> B"); var state = engine.Canonical.State;
        var b = state.Positions["B"];
        Assert.Throws<InvalidDataException>(() => engine.Arrange(state.Move("A", b.X, b.Y)));
        Assert.Throws<InvalidDataException>(() => engine.Arrange(new(new Dictionary<string, ArrangementPosition> { ["unknown"] = new(10, 20) }, ArrangementStrategy.Manual)));
        Assert.Equal(state, engine.Canonical.State);
    }
    [Fact]
    public void Cancellation_reaches_candidate_search_and_final_routing()
    {
        var engine = Engine("erDiagram\n A ||--o{ B : owns"); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => engine.Candidates(engine.Canonical.State, cts.Token));
        Assert.Throws<OperationCanceledException>(() => engine.Arrange(engine.Canonical.State, cts.Token));
    }
    [Fact]
    public void Reordering_unique_blocks_matches_but_duplicate_and_edited_source_never_auto_apply()
    {
        const string first = "# First\n```mermaid\nflowchart TD\n A --> B\n```\n";
        const string second = "# Second\n```mermaid\nerDiagram\n X ||--o{ Y : owns\n```\n";
        var parser = new MarkdownService(); var original = parser.Parse(first + second); var block = MermaidBlockSource.Find(original)[0];
        var identity = ArrangementIdentity.Capture(original, block);
        Assert.Equal(ArrangementMatchKind.Exact, ArrangementIdentity.Match(parser.Parse(second + first), identity, false).Kind);
        Assert.Equal(ArrangementMatchKind.Ambiguous, ArrangementIdentity.Match(parser.Parse(first + first), identity, false).Kind);
        Assert.Equal(ArrangementMatchKind.Stale, ArrangementIdentity.Match(parser.Parse(first.Replace("A --> B", "A --> C")), identity, false).Kind);
        Assert.Equal(ArrangementMatchKind.Missing, ArrangementIdentity.Match(parser.Parse(first), identity, true).Kind);
        var duplicates = parser.Parse(first + first);
        var duplicateIdentity = ArrangementIdentity.Capture(duplicates, MermaidBlockSource.Find(duplicates)[1]);
        Assert.Equal(ArrangementMatchKind.Ambiguous, ArrangementIdentity.Match(parser.Parse(first), duplicateIdentity, false).Kind);
        Assert.Equal(ArrangementMatchKind.Stale, ArrangementIdentity.Match(parser.Parse(first.Replace("# First", "# Different owner")), identity, false).Kind);
    }
    private SavedArrangement Record()
    {
        var engine = Engine("flowchart TD\n A --> B");
        return new(Guid.NewGuid(), "docs/diagram.mmd", new("Flowchart", ArrangementIdentity.Fingerprint(engine.Canonical.Render.Source), ArrangementIdentity.Fingerprint("document"), true),
            ArrangementStrategy.Manual, engine.Canonical.State.Positions);
    }
    [Fact]
    public async Task Read_is_passive_and_save_reload_copy_are_portable()
    {
        var store = new ArrangementStore(new(root)); var empty = await store.ReadAsync(); Assert.Empty(Directory.GetFileSystemEntries(root));
        var record = Record(); var saved = await store.SaveAsync(new(1, [record]), empty); var reloaded = await store.ReadAsync();
        Assert.Equal(saved.Fingerprint, reloaded.Fingerprint); Assert.Equal(record.Id, Assert.Single(reloaded.File.Layouts).Id);
        var copy = Path.Combine(root, "copied"); Directory.CreateDirectory(Path.Combine(copy, ".lens-docs"));
        File.Copy(Path.Combine(root, ArrangementStore.RelativePath), Path.Combine(copy, ArrangementStore.RelativePath));
        Assert.Equal(saved.Fingerprint, (await new ArrangementStore(new(copy)).ReadAsync()).Fingerprint);
        var json = await File.ReadAllTextAsync(Path.Combine(root, ArrangementStore.RelativePath)); Assert.DoesNotContain(root, json); Assert.DoesNotContain("A --> B", json);
    }
    [Fact]
    public async Task External_conflict_keeps_external_bytes_and_backup_recovery_is_explicit()
    {
        var store = new ArrangementStore(new(root)); var saved = await store.SaveAsync(new(1, [Record()]), await store.ReadAsync());
        saved = await store.SaveAsync(saved.File, saved);
        var path = Path.Combine(root, ArrangementStore.RelativePath); await File.WriteAllTextAsync(path, "broken");
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(saved.File, saved)); Assert.Equal("broken", await File.ReadAllTextAsync(path));
        await Assert.ThrowsAnyAsync<JsonException>(() => store.ReadAsync());
        var backup = await store.ReadBackupAsync(); Assert.True(backup.FromBackup); Assert.Equal("broken", await File.ReadAllTextAsync(path));
        await store.SaveAsync(backup.File, backup); Assert.Single((await store.ReadAsync()).File.Layouts);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.unreadable-*"));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"layouts\":[]}")]
    [InlineData("{\"schemaVersion\":99,\"layouts\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"layouts\":null}")]
    public async Task Corrupt_duplicate_or_unknown_schema_is_retained(string json)
    {
        var path = Path.Combine(root, ArrangementStore.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ArrangementStore(new(root)).ReadAsync()); Assert.Equal(json, await File.ReadAllTextAsync(path));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interrupted_save_requires_explicit_backup_recovery_even_for_a_valid_or_missing_primary(bool missingPrimary)
    {
        var store = new ArrangementStore(new(root));
        var saved = await store.SaveAsync(new(1, [Record()]), await store.ReadAsync());
        saved = await store.SaveAsync(saved.File, saved);
        var path = Path.Combine(root, ArrangementStore.RelativePath);
        var journal = path + ".pending-test.json";
        const string evidence = "{\"interrupted\":true}";
        await File.WriteAllTextAsync(journal, evidence);
        if (missingPrimary) File.Delete(path);
        await Assert.ThrowsAsync<IOException>(() => store.ReadAsync());
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(saved.File, saved));
        Assert.Equal(evidence, await File.ReadAllTextAsync(journal));
        var backup = await store.ReadBackupAsync();
        await store.SaveAsync(backup.File, backup);
        Assert.Single((await store.ReadAsync()).File.Layouts);
        Assert.False(File.Exists(journal));
        Assert.Equal(evidence, await File.ReadAllTextAsync(path + ".reviewed-test.json"));
    }
    [Fact]
    public async Task Backup_publication_failure_does_not_leave_an_unrecoverable_pending_journal()
    {
        var store = new ArrangementStore(new(root));
        var saved = await store.SaveAsync(new(1, [Record()]), await store.ReadAsync());
        var path = Path.Combine(root, ArrangementStore.RelativePath);
        Directory.CreateDirectory(path + ".bak");
        var error = await Xunit.Record.ExceptionAsync(() => store.SaveAsync(new(1, []), saved));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.pending-*.json"));
        Assert.Equal(saved.Fingerprint, (await store.ReadAsync()).Fingerprint);
    }
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(1e100)]
    public void Unsafe_coordinates_are_rejected(double x) => Assert.Throws<InvalidDataException>(() => new ArrangementState(new Dictionary<string, ArrangementPosition> { ["A"] = new(x, 5) }, ArrangementStrategy.Manual));
    [Fact]
    public async Task Traversal_is_rejected_before_any_sidecar_is_created()
    {
        var store = new ArrangementStore(new(root)); var record = Record() with { Document = "../outside.mmd" };
        var empty = await store.ReadAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new(1, [record]), empty));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Theory]
    [InlineData("ER")]
    [InlineData("Flowchart")]
    public void Strategies_are_distinct_improve_their_objective_and_round_trip_deterministically(string family)
    {
        var source = family == "ER" ? File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/native-complex-er/monitoring.mmd")) : DenseFlowchart();
        var engine = Engine(source); var candidates = engine.Candidates(engine.Canonical.State);
        var compact = Assert.Single(candidates, c => c.State.Strategy == ArrangementStrategy.Compact);
        var crossings = Assert.Single(candidates, c => c.State.Strategy == ArrangementStrategy.FewerCrossings);
        Assert.True(compact.Quality.Area < engine.Canonical.Quality.Area); Assert.True(crossings.Quality.Crossings < engine.Canonical.Quality.Crossings);
        Assert.NotEqual(compact.Render.Svg, crossings.Render.Svg);
        foreach (var candidate in candidates)
        {
            Assert.Equal(0, candidate.Quality.BodyIntersections); Assert.Equal(0, candidate.Quality.LabelBodyOverlaps);
            Assert.Equal(source, candidate.Render.Source);
            Assert.Equal(candidate.Render.Svg, Engine(source).Arrange(candidate.State).Render.Svg);
        }
        Assert.Equal(candidates.Select(c => c.Render.Svg), engine.Candidates(engine.Canonical.State).Select(c => c.Render.Svg));
    }
    internal static string DenseFlowchart()
    {
        var random = new Random(2); var source = new System.Text.StringBuilder("flowchart TD\n");
        for (var i = 0; i < 14; i++) source.Append($" N{i}[Node {i}]\n");
        for (var i = 0; i < 36; i++) source.Append($" N{random.Next(14)} --> N{random.Next(14)}\n");
        return source.ToString();
    }
    [Fact]
    public void Every_candidate_preserves_pins_or_reports_no_feasible_improvement()
    {
        var engine = Engine(DenseFlowchart()); var state = engine.Canonical.State.TogglePin("N0").TogglePin("N3");
        foreach (var candidate in engine.Candidates(state))
        { Assert.Equal(state.Positions["N0"], candidate.State.Positions["N0"]); Assert.Equal(state.Positions["N3"], candidate.State.Positions["N3"]); }
        Assert.Equal(state.Positions["N0"], engine.Arrange(state).State.Positions["N0"]);
    }
    [Fact]
    public void Moving_a_group_member_outside_its_parent_is_rejected()
    {
        const string source = "flowchart TD\n subgraph Group[Group]\n A --> B\n end\n B --> C\n";
        var engine = Engine(source);
        Assert.Throws<InvalidDataException>(() => engine.Arrange(engine.Canonical.State.Move("A", 8000, 8000)));
        Assert.Empty(engine.Candidates(engine.Canonical.State));
        Assert.Equal(source, engine.Canonical.Render.Source);
    }

    [Fact]
    public void A_parent_owned_node_cannot_visually_enter_a_nested_group()
    {
        const string source = "flowchart TD\n subgraph outer[Outer]\n A[Parent node]\n subgraph inner[Inner]\n B[Child B] --> C[Child C]\n end\n end\n A --> B\n";
        var engine = Engine(source); var b = engine.Boxes["B"]; var a = engine.Boxes["A"];
        var inner = engine.Canonical.Render.FlowchartLayout!.Groups.Single().Children.Single();
        var state = engine.Canonical.State.Move("A", b.X + b.Width / 2 - a.Width / 2, inner.Y + 8);
        var error = Assert.Throws<InvalidDataException>(() => engine.Arrange(state));
        Assert.Contains("source does not belong", error.Message);
        Assert.Equal(source, engine.Canonical.Render.Source);
    }
    [Fact]
    public async Task Oversized_metadata_fails_closed()
    {
        var folder = Path.Combine(root, ".lens-docs"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "diagram-layouts.json");
        await File.WriteAllBytesAsync(path, new byte[ArrangementStore.MaximumBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ArrangementStore(new(root)).ReadAsync());
        Assert.Equal(ArrangementStore.MaximumBytes + 1, new FileInfo(path).Length);
    }

    [Theory]
    [InlineData("complete-detailed.mmd")]
    [InlineData("complete-relationships.mmd")]
    [InlineData("monitoring.mmd")]
    [InlineData("operations.mmd")]
    [InlineData("workflow-assignment.mmd")]
    [InlineData("workflow-definitions.mmd")]
    public void Complex_manual_routes_keep_every_label_on_its_actual_connection(string fixture)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/native-complex-er", fixture));
        var engine = Engine(source); var id = engine.Canonical.State.Positions.Keys.First();
        var state = engine.Canonical.State.Move(id, engine.Boxes.Values.Max(b => b.Right) + 150, engine.Boxes.Values.Max(b => b.Bottom) + 150);
        var moved = engine.Arrange(state);
        Assert.Equal(source, moved.Render.Source); Assert.Equal(0, moved.Quality.BodyIntersections); Assert.Equal(0, moved.Quality.LabelBodyOverlaps);
        foreach (var edge in moved.Render.ErLayout!.Relationships)
        {
            if (edge.LabelPosition is not { } label) continue;
            Assert.Contains(Enumerable.Range(1, edge.Points.Count - 1), i => DiagramViewportMath.NearSegment(label.X, label.Y,
                edge.Points[i - 1].X, edge.Points[i - 1].Y, edge.Points[i].X, edge.Points[i].Y, .01));
        }
    }

    [Fact]
    public async Task Read_only_sidecar_rejects_save_and_keeps_the_previous_bytes()
    {
        var store = new ArrangementStore(new(root)); var saved = await store.SaveAsync(new(1, [Record()]), await store.ReadAsync());
        var path = Path.Combine(root, ArrangementStore.RelativePath); var bytes = await File.ReadAllBytesAsync(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => store.SaveAsync(new(1, []), saved));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }

    [Fact]
    public async Task Junction_sidecar_directory_is_rejected_without_touching_the_target()
    {
        var outside = Path.Combine(root, "external-target"); Directory.CreateDirectory(outside);
        var link = Path.Combine(root, ".lens-docs");
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, outside }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        try
        {
            var store = new ArrangementStore(new(root));
            await Assert.ThrowsAsync<IOException>(() => store.ReadAsync());
            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(new(1, [Record()]), new(new(1, []), null)));
            Assert.Empty(Directory.GetFileSystemEntries(outside));
        }
        finally { Directory.Delete(link); }
    }
}
