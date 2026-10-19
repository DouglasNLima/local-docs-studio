using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal static class InterchangeChecks
{
    internal static FlowDocument Decode(byte[] bytes, string format)
    {
        var document = new FlowDocument(); new TextRange(document.ContentStart, document.ContentEnd).Load(new MemoryStream(bytes), format); return document;
    }
    internal static object[] Media(FlowDocument document)
    {
        var output = new List<object>(); var visited = new HashSet<DependencyObject>(); Visit(document); return output.ToArray();
        void Visit(DependencyObject value)
        {
            if (!visited.Add(value)) return;
            if (value is Image { Source: { } image })
            {
                var encoded = NativeSvgService.ToPng(image, 1);
                var decoder = BitmapDecoder.Create(new MemoryStream(encoded), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Pbgra32, null, 0);
                var stride = bitmap.PixelWidth * 4; var pixels = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(pixels, stride, 0);
                output.Add(new { bitmap.PixelWidth, bitmap.PixelHeight, PixelsSha256 = Program.Hash(pixels) });
            }
            foreach (var child in LogicalTreeHelper.GetChildren(value).OfType<DependencyObject>()) Visit(child);
        }
    }
    public static async Task Compare(PreviewResult stock, LayoutSnapshot snapshot, string output)
    {
        var observations = new List<object>();
        var first = snapshot.Model.Items.FirstOrDefault(item => item.Length > 3);
        if (first is not null && stock.TextMap.TryPreviewRange(snapshot.Model.SourceBoundaries[first.Start + 1], 1, out var from, out var to))
            await Selection("within-run", first.Start + 1, first.Start + 2, from!, to!);
        var list = snapshot.Model.Root.Descendants().FirstOrDefault(node => node.Kind == "List" && node.Children.Length > 1);
        if (list is not null)
        {
            var paragraphs = list.Children.Select(item => snapshot.Model.Items.First(paragraph => paragraph.Start >= item.Start && paragraph.Start < item.End)).Take(2).ToArray();
            var begin = paragraphs[0].Start + 1; var end = paragraphs[1].Start + Math.Min(3, paragraphs[1].Length);
            var sourceStart = snapshot.Model.SourceBoundaries[begin]; var sourceEnd = snapshot.Model.SourceEnds[end - 1];
            if (!stock.TextMap.TryPreviewRange(sourceStart, sourceEnd - sourceStart, out from, out to)) throw new IOException("Partial list source oracle missing.");
            await Selection("partial-cross-item", begin, end, from!, to!);
            begin = paragraphs[0].Start; end = begin + paragraphs[0].Length;
            sourceStart = snapshot.Model.SourceBoundaries[begin]; sourceEnd = snapshot.Model.SourceEnds[end - 1];
            if (!stock.TextMap.TryPreviewRange(sourceStart, sourceEnd - sourceStart, out from, out to)) throw new IOException("List item source oracle missing.");
            await Selection("first-item-without-break", begin, end, from!, to!);
        }
        if (snapshot.Model.Root.Descendants().Any(node => node.Kind is "Image" or "Media"))
        {
            using var package = new MemoryStream(); new TextRange(stock.Document.ContentStart, stock.Document.ContentEnd).Save(package, DataFormats.XamlPackage);
            var payload = await ExperimentWindow.Worker(() => ClipboardSerialisation.Prepare(snapshot, 0, snapshot.Model.Text.Length, CancellationToken.None));
            var stockMedia = Media(Decode(package.ToArray(), DataFormats.XamlPackage)); var candidateMedia = Media(Decode(payload.XamlPackage.ToArray(), DataFormats.XamlPackage));
            File.WriteAllBytes(Path.Combine(output, "stock.xamlpackage"), package.ToArray()); File.WriteAllBytes(Path.Combine(output, "candidate.xamlpackage"), payload.XamlPackage.ToArray());
            if (stockMedia.Length == 0 || System.Text.Json.JsonSerializer.Serialize(stockMedia) != System.Text.Json.JsonSerializer.Serialize(candidateMedia)) throw new IOException("Decoded XamlPackage media dimensions/pixels differ.");
            observations.Add(new { Case = "whole-document-media-package", Stock = stockMedia, Candidate = candidateMedia, payload.Serialisation });
        }
        Program.Write(Path.Combine(output, "interchange-ranges.json"), new { Status = "PASS", Observations = observations });
        async Task Selection(string name, int start, int end, TextPointer from, TextPointer to)
        {
            var expected = new TextRange(from, to);
            var payload = await ExperimentWindow.Worker(() => ClipboardSerialisation.Prepare(snapshot, start, end, CancellationToken.None));
            var formats = new List<object>();
            foreach (var format in new[] { DataFormats.Rtf, DataFormats.Xaml })
            {
                using var stream = new MemoryStream(); expected.Save(stream, format);
                var native = Decode(stream.ToArray(), format); var candidate = Decode(Encoding.UTF8.GetBytes(format == DataFormats.Rtf ? payload.Rtf : payload.Xaml), format);
                var nativeText = new TextRange(native.ContentStart, native.ContentEnd).Text; var candidateText = new TextRange(candidate.ContentStart, candidate.ContentEnd).Text;
                var nativeStyles = TargetedChecks.Styles(native); var candidateStyles = TargetedChecks.Styles(candidate);
                formats.Add(new { Format = format, Stock = nativeText, Candidate = candidateText, StockStyles = nativeStyles, CandidateStyles = candidateStyles });
                if (nativeText != candidateText || System.Text.Json.JsonSerializer.Serialize(nativeStyles) != System.Text.Json.JsonSerializer.Serialize(candidateStyles))
                { Program.Write(Path.Combine(output, name + "-failure.json"), formats); throw new IOException("Partial " + name + " " + format + " differs from stock."); }
            }
            observations.Add(new { Case = name, Start = start, End = end, StockText = expected.Text, CandidateText = payload.Text, Formats = formats, payload.Serialisation });
            if (expected.Text != payload.Text) { Program.Write(Path.Combine(output, name + "-failure.json"), observations); throw new IOException("Partial marker/tab/plain text differs from stock."); }
        }
    }
}
