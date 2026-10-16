using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeMathChecks
{
    public static void Run(string output)
    {
        var cases = new[] { "E=mc^2", @"\frac{a_1}{b^2}", @"\sqrt{x^2+y^2}", @"\sum_{i=1}^{n} i = \frac{n(n+1)}{2}",
            @"\int_0^1 x^2\,dx = \frac{1}{3}", @"\alpha + \beta \leq \gamma", @"\left(\frac{a}{b}\right)^2", @"\begin{pmatrix}a & b \\ c & d\end{pmatrix}",
            @"\text{Café} = 42", @"\binom{n}{k}", @"\overline{AB} + \underline{x}", @"\color{blue}{x}+y",
            @"\begin{bmatrix}1 & 2 \\ 3 & 4\end{bmatrix}", @"\begin{cases}x & x>0 \\ -x & x\leq 0\end{cases}",
            @"\begin{aligned}a &= b+c \\ d &= e+f\end{aligned}" };
        var parser = new MarkdownService(); var service = new NativeMathService();
        var source = "# Native maths\n\nInline $E=mc^2$ and \\(\\sqrt{x}\\) remain in prose.\n\n" + string.Join("\n\n", cases.Skip(1).Select(formula => "$$\n" + formula + "\n$$"));
        var document = parser.Parse(source); var expressions = DocumentMathematics.Read(document.Syntax).ToArray();
        var results = new List<object>(); PreviewTextCopy? copied = null;
        foreach (var dark in new[] { false, true })
        {
            var prepared = service.Prepare(document, dark);
            foreach (var formula in expressions)
            {
                var value = prepared[formula.SourceStart];
                if (value.Drawing is not { IsFrozen: true } drawing) throw new IOException("Native formula failed: " + formula.Text + " — " + value.Error);
                if (drawing.Width < 4 || drawing.Height < 4) throw new IOException("A formula produced an empty native drawing.");
                var filename = "math-" + results.Count + ".png"; File.WriteAllBytes(Path.Combine(output, filename), NativeSvgService.ToPng(drawing));
                results.Add(new { Source = formula.Text, Theme = dark ? "Dark" : "Light", Width = drawing.Width, Height = drawing.Height, File = filename });
            }
            var reused = service.Prepare(document, dark);
            if (!ReferenceEquals(prepared[expressions[0].SourceStart], reused[expressions[0].SourceStart])) throw new IOException("Repeated formula preparation missed its frozen cache.");
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(document, dark, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { },
                copyCode: copy => copied = copy, preparedFormulae: prepared);
            if (rendered.Warnings.Count != 0) throw new IOException("Valid native formulae produced preview warnings.");
            var readable = rendered.ReadText();
            if (!readable.Contains(@"Inline E=mc^2 and \sqrt{x} remain in prose.", StringComparison.Ordinal)
                || !readable.Contains(@"\begin{pmatrix}a & b \\ c & d\end{pmatrix}", StringComparison.Ordinal))
                throw new IOException("Plain preview copying lost formula source or neighbouring prose.");
            var firstInline = document.Syntax.Descendants<MathInline>().First();
            if (rendered.SourceElements[firstInline.Span.Start] is not InlineUIContainer { Child: Image nativeImage, Tag: SourceReference sourceSpan }
                || sourceSpan.Start != firstInline.Span.Start || sourceSpan.End != firstInline.Span.End
                || AutomationProperties.GetName(nativeImage) != "Formula: E=mc^2") throw new IOException("Formula accessibility or exact source mapping was lost.");
            ((MenuItem)nativeImage.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (copied is not { Text: "E=mc^2", Markdown: false }) throw new IOException("Formula context copy changed its original LaTeX.");
            var paginator = ((IDocumentPaginatorSource)rendered.Document).DocumentPaginator; paginator.PageSize = new Size(840, 1160);
            rendered.Document.Background = dark ? new SolidColorBrush(Color.FromRgb(32, 33, 36)) : Brushes.White;
            var page = paginator.GetPage(0); var target = new RenderTargetBitmap(840, 1160, 96, 96, PixelFormats.Pbgra32); target.Render(page.Visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(output, dark ? "native-maths-dark.png" : "native-maths-light.png")); encoder.Save(file);
        }
        var invalid = parser.Parse(@"Bad $\notARealCommand{x}$ and $\frac{x}{y$; source retained.");
        var invalidPrepared = service.Prepare(invalid, false);
        if (invalidPrepared.Count != 2 || invalidPrepared.Values.Any(value => value.Drawing is not null || value.Error is null)) throw new IOException("Invalid formulae did not retain explicit native fallbacks.");
        var invalidPreview = new NativeMarkdownRenderer(new NativeSvgService()).Render(invalid, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, preparedFormulae: invalidPrepared);
        var invalidText = new TextRange(invalidPreview.Document.ContentStart, invalidPreview.Document.ContentEnd).Text;
        if (!invalidText.Contains(@"\notARealCommand{x}", StringComparison.Ordinal) || !invalidText.Contains(@"\frac{x}{y", StringComparison.Ordinal)) throw new IOException("Invalid formula preview silently discarded source.");
        foreach (var invalidSource in new[] { new string('x', 4097), new string('{', 25) + "x" + new string('}', 25), string.Concat(Enumerable.Repeat(@"\alpha ", 129)), "x\0y" })
            if (NativeMathService.Validate(invalidSource) is null) throw new IOException("An oversized or unsafe formula bypassed preparation limits.");
        var many = parser.Parse(string.Join("\n\n", Enumerable.Repeat("$x$", 257)));
        if (service.Prepare(many, false).Values.Count(value => value.Drawing is not null) != 256) throw new IOException("The formula aggregate limit failed.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { service.Prepare(document, false, cancelled.Token); throw new IOException("Formula cancellation was ignored."); } catch (OperationCanceledException) { }
        var content = new ExportPreparationService(parser, new DiagramService(), new NativeSvgService()).Prepare(source, null, null, false);
        if (content.Formulae.Count != expressions.Length) throw new IOException("Maths export lost a native formula.");
        var html = new HtmlExporter().Export(content); File.WriteAllText(Path.Combine(output, "native-maths.html"), html);
        var clipboard = new ClipboardExporter().Export(content);
        if (!clipboard.PlainText.Contains(@"\frac{a_1}{b^2}", StringComparison.Ordinal)) throw new IOException("Clipboard fallback lost LaTeX source.");
        var word = new DocxExporter().Export(content); File.WriteAllBytes(Path.Combine(output, "native-maths.docx"), word);
        using (var package = WordprocessingDocument.Open(new MemoryStream(word), false))
        {
            if (new OpenXmlValidator().Validate(package).Any() || package.MainDocumentPart!.ImageParts.Count() != expressions.Length)
                throw new IOException("Word maths output lost images or became invalid.");
        }
        var pdf = new PdfExporter().Export(content); File.WriteAllBytes(Path.Combine(output, "native-maths.pdf"), pdf);
        using (var package = UglyToad.PdfPig.PdfDocument.Open(pdf))
            if (package.GetPages().Sum(page => page.NumberOfImages) != expressions.Length) throw new IOException("PDF maths output lost formula images.");
        if (document.Source != source) throw new IOException("Formula rendering changed the source document.");
        File.WriteAllText(Path.Combine(output, "native-maths.json"), JsonSerializer.Serialize(new { Passed = true, Cases = results,
            Checks = new[] { "Frozen browser-free drawings and repeat reuse", "Light/dark inline/display rendering", "Accessible source mapping and context copy", "Invalid/bounded/cancelled preparation preserves source", "Complete HTML/Word/PDF/clipboard exports" } }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
