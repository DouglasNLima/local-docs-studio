using System.Globalization;

namespace LensDocsStudio.LayoutExperiment;

internal static class DeterministicChecks
{
    public static void Run(string output)
    {
        var checks = new List<string>();
        foreach (var ending in new[] { "\n", "\r\n" })
        {
            var source = "- **Café 😀 é**" + ending + "- A *styled* boundary and `code`" + ending + "- " + string.Join(' ', Enumerable.Repeat("wrapped content", 40));
            var model = CompleteLayout.Parse(source, CancellationToken.None);
            var layout = CompleteLayout.Format(model, 640, 1, "Light", CancellationToken.None);
            if (model.Source != source || model.Items.Length != 3 || model.SourceBoundaries.Length != model.Text.Length + 1)
                throw new IOException("Logical/source ownership assertion failed.");
            foreach (var item in model.Items)
            foreach (var run in item.Runs)
                if (source.Substring(run.SourceStart, run.Text.Length) != run.Text) throw new IOException("Exact literal source span failed.");
            var unicode = model.Items[0];
            var boundaries = StringInfo.ParseCombiningCharacters(model.Text[..unicode.Length]).Append(unicode.Length).ToHashSet();
            if (layout.Lines.Where(line => line.Item == 0).SelectMany(line => line.Carets).Any(caret => !boundaries.Contains(caret.Offset)))
                throw new IOException("A shaped caret split a combining or surrogate sequence.");
            if (layout.Lines.Length <= 3 || layout.Lines.Any(line => !line.Drawing.IsFrozen || line.Carets.Length < 2 || line.Clusters.Length == 0)
                || layout.Lines.Zip(layout.Lines.Skip(1)).Any(pair => pair.First.Top + pair.First.Height > pair.Second.Top + .01))
                throw new IOException("Complete wrapped geometry/frozen ownership failed.");
            checks.Add(ending == "\n" ? "LF exact source, Unicode caret clusters and full wrapped layout" : "CRLF exact source, Unicode caret clusters and full wrapped layout");
        }
        var rejected = false;
        try { CompleteLayout.Parse("| A | B |\n|---|---|\n| x | y |", CancellationToken.None); }
        catch (NotSupportedException) { rejected = true; }
        if (!rejected) throw new IOException("Unsupported tables must fail explicitly.");
        checks.Add("Unsupported structure fails explicitly");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); rejected = false;
        try { CompleteLayout.Parse("- **cancelled**", cancelled.Token); } catch (OperationCanceledException) { rejected = true; }
        if (!rejected) throw new IOException("Queued cancelled preparation was not rejected.");
        checks.Add("Cancellation before preparation");
        Program.Write(Path.Combine(output, "deterministic.json"), new { Status = "PASS", Checks = checks, TimingThresholds = false });
    }
}
