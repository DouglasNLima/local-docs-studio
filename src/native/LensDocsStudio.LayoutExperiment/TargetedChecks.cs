using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal static class TargetedChecks
{
    public static async Task Run(ExperimentWindow window, string output, bool all = false, string? scenario = null)
    {
        var results = new List<object>(); var failed = false;
        if (scenario is not null && !TargetedFixtures.Cases.Any(value => value.Name == scenario) && !all) throw new ArgumentException("Unknown targeted parity scenario: " + scenario);
        var originals = all ? LensDocsStudio.Native.Validation.ContinuousPreviewFixtures.Create(output, "all").Select(value => new TargetedFixture(value.Name, value.Source, [value.Last])) : [];
        foreach (var fixture in TargetedFixtures.Cases.Concat(originals).Where(value => scenario is null || value.Name == scenario))
        {
            var destination = Path.Combine(output, fixture.Name); Directory.CreateDirectory(destination);
            try
            {
                if (all && !TargetedFixtures.Cases.Any(value => value.Name == fixture.Name)) _ = LensDocsStudio.Native.Validation.ContinuousPreviewFixtures.Create(destination, fixture.Name);
                var path = Path.Combine(destination, "fixture.md"); File.WriteAllText(path, fixture.Source);
                var parsed = new MarkdownService().Parse(fixture.Source); var prepared = await ExperimentWindow.Worker(() => StockPreparation.Create(parsed, path, destination));
                var stock = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, prepared.Diagrams, _ => { }, (_, _) => { },
                    preparedDrawings: prepared.Drawings, preparedImages: prepared.Images, codeSyntax: prepared.Syntax);
                var viewer = new FlowDocumentScrollViewer { Document = stock.Document, Width = 640, Height = 300, IsToolBarVisible = false };
                window.Content = viewer; window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var stockText = new TextRange(stock.Document.ContentStart, stock.Document.ContentEnd).Text;
                var scrolling = (ScrollViewer)viewer.Template.FindName("PART_ContentHost", viewer);
                var stockHeight = scrolling.ExtentHeight; Program.Capture(viewer, Path.Combine(destination, "stock.png"));
                var snapshot = await ExperimentWindow.Worker(() => CompleteLayout.Format(CompleteLayout.Parse(fixture.Source, CancellationToken.None, path, destination), 640, 1, "Light", CancellationToken.None));
                var checks = new List<string>();
                var expected = Regex.Replace(stockText, @"(?m)^\t*(?:•|\d+\.)\t", "");
                Program.Write(Path.Combine(destination, "raw-comparison.json"), new { StockText = stockText, CandidateText = snapshot.Model.Text,
                    StockHeight = stockHeight, CandidateHeight = snapshot.Height,
                    StockLiterals = stock.TextMap.Literals.Where(literal => literal.Start.HasValidLayout).Select(literal => new { literal.Source.Start, literal.Source.Length,
                        Text = literal.Run.Text.Substring(literal.RunOffset, literal.Source.Length), StartRect = literal.Start.GetCharacterRect(LogicalDirection.Forward).ToString(),
                        EndRect = literal.End.GetCharacterRect(LogicalDirection.Backward).ToString(), Family = literal.Run.FontFamily.Source, Size = literal.Run.FontSize,
                        NaturalMargin = literal.Run.FontFamily.LineSpacing * literal.Run.FontSize }),
                    Lines = snapshot.Lines.Select(line => new { line.Start, line.End, line.Item, line.Top, line.Height, line.Carets }) });
                var objects = snapshot.Model.Root.Descendants().Count(node => node.Kind is "Image" or "Media");
                Require(snapshot.Model.Text.Count(character => character == '\uFFFC') == objects, "Every embedded-object placeholder has a real semantic media node", checks);
                Require(snapshot.Model.Text.Replace('\uFFFC', ' ') == expected, "Implementation-independent logical text, table tabs and CRLF order", checks);
                foreach (var value in fixture.RequiredText) Require(snapshot.Model.Text.Contains(value, StringComparison.Ordinal), "Actual text: " + value, checks);
                Require(Math.Abs(snapshot.Height - stockHeight) <= .01, "Complete stock/candidate document extent within 0.01 DIP", checks);
                var geometry = new List<object>();
                var sourceIndex = snapshot.Model.SourceBoundaries.Select((source, logical) => (source, logical)).GroupBy(value => value.source).ToDictionary(group => group.Key, group => group.First().logical);
                foreach (var literal in stock.TextMap.Literals.Where(literal => literal.Start.HasValidLayout))
                {
                    var logical = sourceIndex.GetValueOrDefault(literal.Source.Start, -1);
                    if (logical < 0) throw new IOException("A stock literal lacks a candidate source boundary.");
                    var line = snapshot.Lines.First(value => logical >= value.Start && logical <= value.End);
                    var caret = line.Carets.MinBy(value => Math.Abs(value.Offset - logical))!;
                    var rectangle = literal.Start.GetCharacterRect(LogicalDirection.Forward);
                    var band = line.Clusters.FirstOrDefault(cluster => cluster.Start <= logical && cluster.End > logical)?.Bounds.FirstOrDefault() ?? new Rect(caret.X, line.Top, 0, line.Height);
                    geometry.Add(new { literal.Source.Start, Native = rectangle.ToString(), CandidateCaretX = caret.X, CandidateBand = band.ToString(), Text = literal.Run.Text });
                    Require(Math.Abs(caret.X - rectangle.X) <= .01 && Math.Abs(band.Top - rectangle.Top) <= .01, "Actual literal source/caret position: " + literal.Source.Start, checks);
                    var ending = logical + literal.Source.Length;
                    var lastLine = snapshot.Lines.First(value => ending > value.Start && ending <= value.End);
                    var lastCaret = lastLine.Carets.MinBy(value => Math.Abs(value.Offset - ending))!;
                    var nativeEnd = literal.End.GetCharacterRect(LogicalDirection.Backward);
                    Require(Math.Abs(lastCaret.X - nativeEnd.X) <= .01, "Actual wrapped literal ending caret: " + literal.Source.Start, checks);
                }
                Program.Write(Path.Combine(destination, "literal-geometry.json"), geometry);
                var logicalText = snapshot.Model.Text;
                var linesByItem = snapshot.Lines.GroupBy(line => line.Item).ToDictionary(group => group.Key, group => group.ToArray());
                for (var itemIndex = 0; itemIndex < snapshot.Model.Items.Length; itemIndex++)
                {
                    var item = snapshot.Model.Items[itemIndex];
                    var stops = StringInfo.ParseCombiningCharacters(logicalText.Substring(item.Start, item.Length)).Select(at => at + item.Start).Append(item.Start + item.Length).ToHashSet();
                    Require(linesByItem[itemIndex].SelectMany(line => line.Carets).All(caret => stops.Contains(caret.Offset)), "No surrogate/combining/ZWJ split", checks);
                }
                window.Content = window.Scroller; window.InstallMeasured(snapshot); window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Program.Capture(window.Scroller, Path.Combine(destination, "candidate.png"));
                foreach (var media in snapshot.Model.Root.Descendants().Where(node => node.Kind == "Media"))
                {
                    window.Surface.Reveal(media.Start); window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Program.Capture(window.Scroller, Path.Combine(destination, "candidate-media-" + media.Id + ".png"));
                    var controls = window.Surface.Children.OfType<Button>().Where(button => button.Tag is ControlBox box && box.Node == media.Id).ToArray();
                    Program.Write(Path.Combine(destination, "media-control-geometry.json"), controls.Select(button => new { Text = button.Content, Desired = button.DesiredSize.ToString(), Slot = ((ControlBox)button.Tag).Bounds.ToString(), button.ActualWidth, button.ActualHeight }));
                    Require(controls.Length == 4 && controls.All(button => Math.Abs(button.DesiredSize.Height - ((ControlBox)button.Tag).Bounds.Height) < .01 && Math.Abs(button.DesiredSize.Width - ((ControlBox)button.Tag).Bounds.Width) < .01), "Native media command desired sizes agree with eager worker geometry", checks);
                }
                using var stockRtf = new MemoryStream(); new TextRange(stock.Document.ContentStart, stock.Document.ContentEnd).Save(stockRtf, DataFormats.Rtf);
                var payload = await ExperimentWindow.Worker(() => ClipboardSerialisation.Prepare(snapshot, 0, snapshot.Model.Text.Length, CancellationToken.None));
                var stockDecoded = Decode(stockRtf.ToArray()); var candidateDecoded = Decode(Encoding.UTF8.GetBytes(payload.Rtf));
                Require(stockDecoded.Text == candidateDecoded.Text, "Decoded RTF logical text, list/table structure and line endings", checks);
                var stockStyles = Styles(stockDecoded.Document); var candidateStyles = Styles(candidateDecoded.Document);
                Program.Write(Path.Combine(destination, "decoded-rtf-styles.json"), new { Stock = stockStyles, Candidate = candidateStyles, payload.Serialisation });
                Require(System.Text.Json.JsonSerializer.Serialize(stockStyles) == System.Text.Json.JsonSerializer.Serialize(candidateStyles), "Decoded RTF actual font, size, weight, italic, colour, decorations and baseline styles", checks);
                using var stockXaml = new MemoryStream(); new TextRange(stock.Document.ContentStart, stock.Document.ContentEnd).Save(stockXaml, DataFormats.Xaml);
                var stockXamlDecoded = Decode(stockXaml.ToArray(), DataFormats.Xaml); var candidateXamlDecoded = Decode(Encoding.UTF8.GetBytes(payload.Xaml), DataFormats.Xaml);
                Require(stockXamlDecoded.Text == candidateXamlDecoded.Text, "Decoded Xaml logical text and structure", checks);
                var nativeXamlStyles = Styles(stockXamlDecoded.Document); var candidateXamlStyles = Styles(candidateXamlDecoded.Document);
                Program.Write(Path.Combine(destination, "decoded-xaml-styles.json"), new { Stock = nativeXamlStyles, Candidate = candidateXamlStyles });
                Require(System.Text.Json.JsonSerializer.Serialize(nativeXamlStyles) == System.Text.Json.JsonSerializer.Serialize(candidateXamlStyles), "Decoded Xaml actual styled text fidelity", checks);
                Program.Write(Path.Combine(destination, "logical-and-geometry.json"), new { Status = "PASS", Checks = checks, StockText = stockText, CandidateText = logicalText,
                    StockHeight = stockHeight, CandidateHeight = snapshot.Height, FixtureHash = Program.Hash(Encoding.UTF8.GetBytes(fixture.Source)) });
                results.Add(new { fixture.Name, Status = "PASS" }); viewer.Document = null;
            }
            catch (Exception error) { failed = true; Program.Write(Path.Combine(destination, "failure.json"), new { Status = "FAIL", Error = error.ToString() }); results.Add(new { fixture.Name, Status = "FAIL" }); }
        }
        Program.Write(Path.Combine(output, "targeted-parity.json"), new { Status = failed ? "FAIL" : "PASS", Results = results, TimingAssertions = false, FullAcceptance = "INCOMPLETE_PENDING_REMAINING_CHECKS" });
        if (failed) throw new IOException("Targeted parity failed; all failures are retained.");
    }
    private static void Require(bool condition, string check, List<string> checks) { if (!condition) throw new IOException(check); checks.Add(check); }
    private static (FlowDocument Document, string Text) Decode(byte[] bytes, string? format = null)
    {
        var document = new FlowDocument(); var range = new TextRange(document.ContentStart, document.ContentEnd); range.Load(new MemoryStream(bytes), format ?? DataFormats.Rtf);
        return (document, range.Text);
    }
    private static object[] Styles(FlowDocument document)
    {
        var output = new List<object>(); var pointer = document.ContentStart;
        while (pointer is not null && pointer.CompareTo(document.ContentEnd) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(LogicalDirection.Forward);
                var element = (TextElement)pointer.Parent;
                var decorations = new List<TextDecoration>();
                for (var ancestor = element; ancestor is not null; ancestor = ancestor.Parent as TextElement)
                    if (ancestor is Inline inline) decorations.AddRange(inline.TextDecorations);
                foreach (var character in text) output.Add(new { Character = (int)character,
                    Family = element.FontFamily.Source, Size = element.FontSize, Weight = element.FontWeight.ToOpenTypeWeight(),
                    Italic = element.FontStyle == FontStyles.Italic, Colour = element.Foreground.ToString(),
                    Baseline = element is Inline effective ? effective.BaselineAlignment.ToString() : "Baseline",
                    Background = element.Background?.ToString(),
                    Decorations = string.Join(";", decorations.Select(value => $"{value.Location}:{value.PenOffset}:{value.PenOffsetUnit}:{value.PenThicknessUnit}")) });
            }
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        return output.ToArray();
    }
}
