using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/native/validation");
        var repository = Path.GetFullPath(args.ElementAtOrDefault(1) ?? ".");
        Directory.CreateDirectory(output);
        var parser = new MarkdownService();
        var diagrams = new DiagramService();
        var native = new NativeSvgService();
        var results = new List<object>();
        var failures = new List<string>();
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(repository, "tests", "fixtures"), "*", SearchOption.AllDirectories)
            .Where(p => Path.GetExtension(p) is ".md" or ".mmd" or ".mermaid").Order())
        {
            var text = File.ReadAllText(path);
            var standalone = Path.GetExtension(path) is ".mmd" or ".mermaid";
            var document = parser.Parse(text, standalone);
            foreach (var code in document.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info is "mermaid" or "mmd"))
            {
                foreach (var dark in new[] { false, true })
                {
                    var name = $"{++count:D3}-{Path.GetFileNameWithoutExtension(path)}-{(dark ? "dark" : "light")}";
                    var watch = Stopwatch.StartNew();
                    var diagram = diagrams.Render(code.Lines.ToString(), dark);
                    // The web autocomplete fixture intentionally contains only the incomplete header "flo".
                    var expectedInvalid = Path.GetFileName(path) is "broken-mermaid.md" or "autocomplete.mmd";
                    var valid = diagram.Success;
                    string? error = diagram.Error;
                    int bytes = 0;
                    if (valid)
                    {
                        try
                        {
                            var drawing = native.Render(diagram.Svg!);
                            var png = NativeSvgService.ToPng(drawing);
                            File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                            File.WriteAllText(Path.Combine(output, name + ".svg"), native.Prepare(diagram.Svg!));
                            bytes = png.Length;
                            if (bytes < 100 || drawing.Width < 10 || drawing.Height < 10) throw new InvalidDataException("No meaningful native drawing.");
                        }
                        catch (Exception ex) { valid = false; error = ex.Message; }
                    }
                    var passed = expectedInvalid ? !diagram.Success : valid;
                    if (!passed) failures.Add(name + ": " + error);
                    results.Add(new { Fixture = Path.GetRelativePath(repository, path), Line = code.Line + 1, Theme = dark ? "Dark" : "Light", Passed = passed, ExpectedInvalid = expectedInvalid, Error = error, PngBytes = bytes, ElapsedMilliseconds = watch.ElapsedMilliseconds });
                }
            }
        }
        if (count == 0) failures.Add("No Mermaid fixtures discovered.");
        File.WriteAllText(Path.Combine(output, "mermaid-corpus.json"), JsonSerializer.Serialize(new { Total = count, Failures = failures, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native Mermaid corpus: {count - failures.Count}/{count} passed. Evidence: {output}");
        foreach (var failure in failures) Console.WriteLine(failure);
        return failures.Count == 0 ? 0 : 1;
    }
}
