using System.Text.Json;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

/// <summary>Document/session checks only: no window, desktop input, personal files or system preference changes.</summary>
internal static class MermaidCreationSessionChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        foreach (var (remaining, expected) in new[] { (-1d, -1d), (0d, -1d), (49.999d, -1d), (50d, 50d), (50.999d, 50d), (3001d, 3000d) })
        {
            var timeout = NativeUiChecks.EditorSaveReadiness.ProviderTimeout(TimeSpan.FromMilliseconds(remaining));
            Check((timeout?.TotalMilliseconds ?? -1) == expected, "Save readiness provider timeout respects supported whole milliseconds and the remaining deadline: " + remaining);
        }
        foreach (var definition in MermaidDiagramCatalogue.All)
        foreach (var variant in definition.Variants)
        foreach (var original in new[] { "", "Before Ω😀\nAfter", "\uFEFFBefore\r\nAfter\r\n", "```mermaid\nflowchart TD\n```\n\nAfter" })
        foreach (var position in new[] { 0, original.Length })
        {
            var owner = new DocumentViewModel(original); var context = new MermaidCreationCoordinator(owner, position, 0, position);
            var target = context.ResolveAsync().GetAwaiter().GetResult();
            if (target.Route != MermaidCreationRoute.NewDiagram) continue;
            Check(owner.EditorDocument.Text == original && !owner.EditorDocument.UndoStack.CanUndo, "Capture/cancel preserves source and Undo");
            var draft = variant.Source.ReplaceLineEndings(original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
            var session = context.CreateSession(target); var prepared = session.PrepareAsync(draft).GetAwaiter().GetResult();
            Check(owner.EditorDocument.Text == original && !owner.EditorDocument.UndoStack.CanUndo, "Prepare remains private");
            session.Commit(prepared, true); var applied = owner.EditorDocument.Text;
            Check(applied.Remove(prepared.Span.Start, prepared.Replacement.Length) == original, "Every surrounding source character is preserved");
            Check(MermaidBlockSource.Find(new MarkdownService().Parse(applied)).Count(block => block.Source == draft
                && block.Body.Start >= prepared.Span.Start && block.Body.End <= prepared.Span.Start + prepared.Replacement.Length) == 1, definition.Name + "/" + variant.Id + " exact parsed insertion");
            owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == original && !owner.EditorDocument.UndoStack.CanUndo, "Apply is exactly one Undo");
            owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == applied, "Redo restores exact source");
            Check(owner.SavedSnapshot is null, "Apply never saves");
        }
        foreach (var extension in new[] { ".mmd", ".mermaid" })
        foreach (var definition in MermaidDiagramCatalogue.All)
        {
            var owner = new DocumentViewModel("") { DisplayName = "Untitled" + extension };
            var context = new MermaidCreationCoordinator(owner, 0, 0, 0); var target = context.ResolveAsync().GetAwaiter().GetResult();
            context.CreateSession(target).Apply(definition.Variants[0].Source, true);
            Check(owner.EditorDocument.Text == definition.Variants[0].Source, "Standalone Apply is raw " + definition.Name + " " + extension);
            var nonempty = new MermaidCreationCoordinator(owner, 0, 0, 0).ResolveAsync().GetAwaiter().GetResult();
            Check(nonempty.Route == MermaidCreationRoute.ExistingDiagram, "Nonempty standalone never offers replacement family");
        }
        {
            const string original = "Before selected Ω😀 after";
            var owner = new DocumentViewModel(original); var context = new MermaidCreationCoordinator(owner, 7, 8, 15);
            var target = context.ResolveAsync().GetAwaiter().GetResult(); context.CreateSession(target).Apply("flowchart TD\n", true);
            Check(owner.EditorDocument.Text.StartsWith(original[..15], StringComparison.Ordinal) && owner.EditorDocument.Text.EndsWith(original[15..], StringComparison.Ordinal), "Nonempty selected text is retained before insertion");
        }
        foreach (var scenario in new[] { "closed", "switched", "replaced", "read-only", "saving", "renamed", "changed", "same-text-version" })
        {
            var owner = new DocumentViewModel("original"); var context = new MermaidCreationCoordinator(owner, 0, 0, 0);
            var active = owner; var current = owner.EditorDocument;
            if (scenario == "switched") active = new("other");
            if (scenario == "replaced") current = new("original");
            if (scenario == "saving") owner.IsSaving = true;
            if (scenario == "renamed") owner.DisplayName = "renamed.md";
            if (scenario == "changed") owner.EditorDocument.Insert(0, "external ");
            if (scenario == "same-text-version") { owner.EditorDocument.Insert(0, "external "); owner.EditorDocument.UndoStack.Undo(); }
            Check(context.CurrentProblem(active, current, scenario == "read-only", scenario != "closed") is not null, scenario + " rejects captured context");
        }
        foreach (var scenario in new[] { "closed", "saving", "renamed", "changed" })
        {
            var owner = new DocumentViewModel(""); var context = new MermaidCreationCoordinator(owner, 0, 0, 0);
            var session = context.CreateSession(context.ResolveAsync().GetAwaiter().GetResult());
            var prepared = session.PrepareAsync("flowchart TD\n").GetAwaiter().GetResult();
            if (scenario == "saving") owner.IsSaving = true;
            if (scenario == "renamed") owner.DisplayName = "changed.md";
            if (scenario == "changed") owner.EditorDocument.Insert(0, "external");
            var before = owner.EditorDocument.Text;
            Reject(() => session.Commit(prepared, scenario != "closed"));
            Check(owner.EditorDocument.Text == before, "Final commit rejects " + scenario + " without replacing source");
        }
        File.WriteAllText(Path.Combine(output, "sessions.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks.Count, Details = checks }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Mermaid creation sessions: {checks.Count} checks passed; no GUI used.");
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks.Add(message); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidDataException("Unsafe commit was accepted."); }
    }
}
