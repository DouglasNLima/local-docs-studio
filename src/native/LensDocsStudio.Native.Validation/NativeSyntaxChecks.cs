using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using ICSharpCode.AvalonEdit.Highlighting;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSyntaxChecks
{
    public static void Run(string output)
    {
        var parser = new MarkdownService();
        const string code = "/* First line\r\n   Comment continues */\r\npublic string Describe()\r\n{\r\n    var label = \"Café <studio> 😀\";\r\n    return label + 42;\r\n}";
        var parsed = parser.Parse("# Native code preview\r\n\r\n```csharp\r\n" + code + "\r\n```\r\n\r\nUnchanged, selectable source.");
        var stopwatch = Stopwatch.StartNew();
        var syntax = Task.Run(() => CodeSyntaxService.Prepare(parsed)).GetAwaiter().GetResult();
        var firstMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        var block = parsed.Syntax.Descendants<FencedCodeBlock>().Single();
        if (!syntax.Blocks.TryGetValue(block, out var prepared) || syntax.Limited || prepared.Text != code)
            throw new IOException("Background syntax preparation lost the complete C# source: " + JsonSerializer.Serialize(new { syntax.Limited, Count = syntax.Blocks.Count, firstMilliseconds }));
        CodeTokenRole At(string fragment)
        {
            var start = code.IndexOf(fragment, StringComparison.Ordinal);
            return prepared.Spans.Single(span => span.Start <= start && span.Start + span.Length > start).Role;
        }
        if (At("Comment continues") != CodeTokenRole.Comment || At("public") != CodeTokenRole.Keyword
            || At("Café") != CodeTokenRole.String || At("42") != CodeTokenRole.Number)
            throw new IOException("Native syntax colouring lost multiline comments, keywords, Unicode strings or numbers.");
        var languages = new Dictionary<string, string>
        {
            ["js"] = "// Comment\nconst label = 'Café'; return 42;", ["json"] = "{\"label\": \"Café\", \"count\": 42, \"ready\": true}",
            ["python"] = "# Comment\ndef describe():\n    return 'Café'", ["xml"] = "<!-- Comment -->\n<document label=\"Café\" />",
            ["html"] = "<!-- Comment -->\n<p title=\"Café\">Text</p>", ["css"] = "/* Comment */\np { color: red; }",
            ["powershell"] = "# Comment\nWrite-Host 'Café'", ["sql"] = "-- Comment\nSELECT 'Café', 42;", ["java"] = "// Comment\npublic String label = \"Café\";",
            ["cpp"] = "// Comment\nconst char* label = \"Café\";", ["php"] = "<?php\n// Comment\necho 'Café'; ?>", ["vb"] = "' Comment\nDim label As String = \"Café\"",
            ["diff"] = "--- before\n+++ after\n@@ -1 +1 @@\n-Removed line\n+Added line"
        };
        var languageCounts = new Dictionary<string, int>();
        foreach (var (language, text) in languages)
        {
            var sample = parser.Parse("```" + language + "\n" + text + "\n```");
            var plan = Task.Run(() => CodeSyntaxService.Prepare(sample)).GetAwaiter().GetResult();
            if (plan.Limited || plan.Blocks.Count != 1 || plan.Blocks.Single().Value.Text != text)
                throw new IOException("Bundled syntax definition did not preserve and colour " + language + ".");
            languageCounts.Add(language, plan.Blocks.Single().Value.Spans.Count);
        }
        foreach (var dark in new[] { false, true })
        {
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark,
                new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, codeSyntax: syntax);
            var paragraph = (Paragraph)rendered.SourceElements[block.Span.Start];
            var runs = paragraph.Inlines.OfType<Run>().ToArray();
            if (string.Concat(runs.Select(run => run.Text)) != code || paragraph.Tag is not SourceReference reference || reference.Start != block.Span.Start
                || new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text != code)
                throw new IOException("Native coloured text lost CRLF, Unicode, selection text or source mapping.");
            var background = ((SolidColorBrush)paragraph.Background).Color;
            foreach (var run in runs)
                if (Contrast(((SolidColorBrush)run.Foreground).Color, background) < 4.5)
                    throw new IOException("A native code colour does not meet 4.5:1 contrast in the " + (dark ? "dark" : "light") + " palette.");
            if (runs.Select(run => ((SolidColorBrush)run.Foreground).Color).Distinct().Count() < 5)
                throw new IOException("Native code runs did not receive distinct semantic colours.");
            rendered.Document.Background = new SolidColorBrush(dark ? Color.FromRgb(28, 30, 33) : Color.FromRgb(250, 250, 250));
            var paginator = ((IDocumentPaginatorSource)rendered.Document).DocumentPaginator; paginator.PageSize = new Size(840, 600);
            var bitmap = new RenderTargetBitmap(840, 600, 96, 96, PixelFormats.Pbgra32); bitmap.Render(paginator.GetPage(0).Visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, "native-syntax-" + (dark ? "dark" : "light") + ".png")); encoder.Save(file);
        }
        var contrastParagraph = new Paragraph(); CodeSyntaxColours.Add(contrastParagraph.Inlines, code, prepared, false, true);
        if (contrastParagraph.Inlines.Count != 1 || contrastParagraph.Inlines.FirstInline.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue
            || ((Run)contrastParagraph.Inlines.FirstInline).Text != code) throw new IOException("High-contrast code did not inherit system text colours.");
        var stale = new Paragraph(); CodeSyntaxColours.Add(stale.Inlines, "New text", prepared, false, false);
        if (stale.Inlines.Count != 1 || ((Run)stale.Inlines.FirstInline).Text != "New text") throw new IOException("A stale syntax plan changed replacement text.");
        var unsupported = parser.Parse("```unrecognised\n<label>  'literal' 😀\n```");
        var unsupportedPlan = CodeSyntaxService.Prepare(unsupported);
        if (unsupportedPlan.Blocks.Count != 0 || unsupportedPlan.Limited) throw new IOException("Unknown languages were not left as plain source.");
        foreach (var text in new[] { new string('x', CodeSyntaxService.MaximumBlockCharacters + 1), new string('x', CodeSyntaxService.MaximumLineCharacters + 1),
            string.Join("\n", Enumerable.Repeat("var a = 42;", 600)) })
        {
            var oversized = parser.Parse("```cs\n" + text + "\n```"); var plan = CodeSyntaxService.Prepare(oversized);
            if (!plan.Limited || plan.Blocks.Count != 0) throw new IOException("A syntax size/span limit did not preserve a plain fallback.");
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(oversized, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, codeSyntax: plan);
            if (rendered.Warnings.Count != 1 || string.Concat(((Paragraph)rendered.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Select(run => run.Text)) != text)
                throw new IOException("Syntax fallback truncated source or omitted its limit feedback.");
        }
        var many = parser.Parse(string.Join("\n\n", Enumerable.Repeat("```cs\n" + new string(' ', 2048) + "var a = 42;\n```", 600)));
        var bounded = CodeSyntaxService.Prepare(many);
        if (!bounded.Limited || bounded.Blocks.Values.Sum(value => value.Text.Length) > CodeSyntaxService.MaximumDocumentCharacters
            || bounded.Blocks.Values.Sum(value => value.Spans.Count) > CodeSyntaxService.MaximumDocumentSpans)
            throw new IOException("Aggregate syntax preparation exceeded its document budget.");
        try { CodeSyntaxService.Prepare(parsed, new(true)); throw new IOException("Cancelled syntax preparation returned a plan."); }
        catch (OperationCanceledException) { }
        var originalRules = new HighlightingRuleSet();
        originalRules.Rules.Add(new() { Regex = new Regex("^(a+)+$") });
        originalRules.Spans.Add(new() { StartExpression = new Regex("/\\*"), EndExpression = new Regex("\\*/"), RuleSet = originalRules });
        var boundedRules = BoundedCodeRules.Create(originalRules);
        if (ReferenceEquals(originalRules, boundedRules) || !ReferenceEquals(boundedRules.Spans[0].RuleSet, boundedRules)
            || originalRules.Rules[0].Regex.MatchTimeout != Regex.InfiniteMatchTimeout
            || boundedRules.Spans[0].StartExpression.MatchTimeout != BoundedCodeRules.MatchTimeout
            || boundedRules.Spans[0].EndExpression.MatchTimeout != BoundedCodeRules.MatchTimeout)
            throw new IOException("Bounded syntax rules changed shared definitions or lost recursive span state/timeouts.");
        try { boundedRules.Rules[0].Regex.IsMatch(new string('a', 1024) + "!"); throw new IOException("A pathological syntax expression did not time out."); }
        catch (RegexMatchTimeoutException) { }
        File.WriteAllText(Path.Combine(output, "native-syntax.json"), JsonSerializer.Serialize(new
        {
            Passed = true, FirstPreparationMilliseconds = firstMilliseconds, Languages = languageCounts,
            Checks = new[] { "Bundled native definitions colour fourteen language families on a background worker", "Multiline comments, keywords, Unicode strings and numeric tokens retain their roles",
                "Light/dark WPF runs preserve exact selectable CRLF source and source mapping", "Both code palettes meet 4.5:1 contrast; high contrast inherits system text",
                "Unknown languages and stale plans preserve plain text", "Block, line, span and aggregate limits retain full visible source with feedback", "Cancelled preparation produces no plan",
                "Copied rule graphs retain recursion, leave shared definitions unchanged and time out pathological expressions" },
            Limitation = "The 250 ms text-processing budget and cancellation are checked between bounded lines, with a 25 ms timeout per regular-expression match. Initial bundled-definition loading is separate. Export syntax colouring is separate."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    internal static double Contrast(Color first, Color second)
    {
        double Luminance(Color colour)
        {
            double Channel(byte value) { var normal = value / 255d; return normal <= .04045 ? normal / 12.92 : Math.Pow((normal + .055) / 1.055, 2.4); }
            return .2126 * Channel(colour.R) + .7152 * Channel(colour.G) + .0722 * Channel(colour.B);
        }
        var a = Luminance(first); var b = Luminance(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}
