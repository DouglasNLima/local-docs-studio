using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal static class RangeOracleChecks
{
    internal const string Source = "## Heading\n\nPlain **bold** *italic* and `code`.\n\nLatin العربيّة end é 😀.\n\n";
    public static async Task Run(ExperimentWindow window, string output)
    {
        var source = Source + "| Cell A | Cell B | Cell C |\n|---|---|---|\n| Value D | Value E | Value F |\n| Value G | Value H | Value I |\n\n" + TargetedFixtures.Cases.Single(value => value.Name == "table-spans").Source + "\n\n" + string.Join("\n\n", Enumerable.Range(0, 40).Select(index => "Wrapped paragraph " + index + " " + string.Join(' ', Enumerable.Repeat("measured words", 15)))) + "\n\nFinal marker.\n";
        var path = Path.Combine(output, "fixture.md"); File.WriteAllText(path, source);
        var gaps = new List<double>(); var timeline = new List<object>(); var candidateGaps = new List<double>(); var last = Stopwatch.GetTimestamp(); var phase = "setup";
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = Stopwatch.GetTimestamp(); var gap = (at - last) * 1000d / Stopwatch.Frequency; gaps.Add(gap);
            timeline.Add(new { Phase = phase, StartTimestamp = last, EndTimestamp = at, Milliseconds = gap }); if (phase.StartsWith("candidate", StringComparison.Ordinal)) candidateGaps.Add(gap); last = at; }; timer.Start();
        try
        {
            foreach (var side in new[] { "stock", "candidate" })
            {
                var folder = Path.Combine(output, side); Directory.CreateDirectory(folder);
                phase = side + "-setup";
                if (side == "stock")
                {
                    var parsed = new MarkdownService().Parse(source);
                    var prepared = await ExperimentWindow.Worker(() => StockPreparation.Create(parsed, path, output));
                    var stock = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, prepared.Diagrams, _ => { }, (_, _) => { }, preparedDrawings: prepared.Drawings, preparedImages: prepared.Images, codeSyntax: prepared.Syntax);
                    AutomationProperties.SetAutomationId(stock.Document, "RangeOracleDocument");
                    window.Content = new FlowDocumentScrollViewer { Document = stock.Document, Width = 640, Height = 300, IsToolBarVisible = false, Focusable = true };
                }
                else { window.Content = window.Scroller; window.BeginDocument(null); await window.Request(source); }
                window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                phase = side + "-interaction";
                using var acknowledgement = new InputAcknowledgement(window);
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { "--range-client", folder, Environment.ProcessId.ToString(), side, acknowledgement.Name }) start.ArgumentList.Add(argument);
                using var client = (await Task.Run(() => Process.Start(start)))!;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120)); await client.WaitForExitAsync(deadline.Token);
                Program.Write(Path.Combine(folder, "input-acknowledgements.json"), acknowledgement.Receipts);
                if (client.ExitCode != 0) throw new IOException("Independent range oracle failed: " + side);
            }
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input); phase = "diagnostic-comparison";
            var stockRows = File.ReadAllText(Path.Combine(output, "stock", "operations.json"));
            var candidateRows = File.ReadAllText(Path.Combine(output, "candidate", "operations.json"));
            var matched = stockRows == candidateRows;
            Program.Write(Path.Combine(output, "range-parity.json"), new { Status = matched ? "PASS" : "FAIL", OperationsEqual = matched,
                InputProbe = "range-phase-v2: uninterrupted timer; candidate setup/interaction row separate from stock; held-modifier Input acknowledgements",
                MaximumInputGapMilliseconds = candidateGaps.DefaultIfEmpty().Max(), CandidateInputGapsMilliseconds = candidateGaps,
                WholeStockAndCandidateMaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), WholeScenarioInputGapsMilliseconds = gaps, Timeline = timeline });
            if (!matched) throw new IOException("Actual stock/candidate range or visual-direction keyboard operations differ; all rows retained.");
        }
        finally { timer.Stop(); }
    }
    public static int Client(string output, int processId, string side, string acknowledgementName)
    {
        IndependentUiaClient.InitialiseOwnedInput(output);
        using var acknowledgement = new InputAcknowledgement(acknowledgementName);
        var rows = new List<object>();
        var clipboardReads = new List<object>();
        try
        {
            using var clipboard = new IndependentUiaClient.ClipboardOwner();
            using var process = Process.GetProcessById(processId); var window = AutomationElement.FromHandle(process.MainWindowHandle);
            AutomationElement? document = null;
            IndependentUiaClient.Wait(() => (document = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, side == "stock" ? "RangeOracleDocument" : "ExperimentalDocument"))) is not null);
            var text = (TextPattern)document!.GetCurrentPattern(TextPattern.Pattern);
            var spans = text.DocumentRange.FindText("AAAAA", false, false)!;
            var spanEnding = text.DocumentRange.FindText("After spanned table.", false, false)!;
            spans.MoveEndpointByRange(TextPatternRangeEndpoint.End, spanEnding, TextPatternRangeEndpoint.Start);
            foreach (var (first, last, scope) in new[] { ("Cell A", "Cell B", text.DocumentRange), ("Cell B", "Value E", text.DocumentRange), ("Cell B", "Value D", text.DocumentRange), ("Cell A", "Cell A", text.DocumentRange),
                ("B B B", "E", spans), ("B B B", "D D D", spans), ("AAAAA", "B B B", spans), ("D D D", "CCCCC", spans) })
            {
                var range = scope.FindText(first, false, false)!; var ending = scope.FindText(last, false, false)!;
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, 1); ending.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -1);
                range.MoveEndpointByRange(TextPatternRangeEndpoint.End, ending, TextPatternRangeEndpoint.End);
                var before = range.GetText(-1); range.Select(); var selected = text.GetSelection();
                rows.Add(new { Operation = "actual-table-selection", First = first, Last = last, Before = before, Text = selected.Select(value => value.GetText(-1)).ToArray() });
                document.SetFocus(); var sentinel = "Table copy sentinel " + first + last; clipboard.SetSentinel(sentinel);
                IndependentUiaClient.ChordObserved(acknowledgement, process.MainWindowHandle, 0x11, 0x43);
                string? plain = null, rtf = null, xaml = null; string[] formats = [];
                IndependentUiaClient.Wait(() =>
                {
                    // Keep the OLE object and all reads on the independent
                    // client's pumped owning STA; transfer only immutable data.
                    var value = clipboard.Read(() =>
                    {
                        var data = Clipboard.GetDataObject();
                        return (Plain: data?.GetData(DataFormats.UnicodeText, false) as string,
                            Rtf: data?.GetData(DataFormats.Rtf), Xaml: data?.GetData(DataFormats.Xaml), Formats: data?.GetFormats(false) ?? []);
                    });
                    plain = value.Plain; rtf = value.Rtf as string; xaml = value.Xaml as string; formats = value.Formats;
                    clipboardReads.Add(new { First = first, Last = last, At = Stopwatch.GetTimestamp(), Sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence,
                        RtfType = value.Rtf?.GetType().FullName, RtfLength = rtf?.Length, XamlType = value.Xaml?.GetType().FullName, XamlLength = xaml?.Length });
                    return plain is not null && plain != sentinel && !string.IsNullOrEmpty(rtf) && !string.IsNullOrEmpty(xaml);
                });
                File.WriteAllText(Path.Combine(output, "table-copy-" + first.Replace(' ', '-') + "-" + last.Replace(' ', '-') + ".rtf"), rtf);
                File.WriteAllText(Path.Combine(output, "table-copy-" + first.Replace(' ', '-') + "-" + last.Replace(' ', '-') + ".xaml"), xaml);
                var decodedRtf = InterchangeChecks.Decode(System.Text.Encoding.UTF8.GetBytes(rtf!), DataFormats.Rtf);
                var decodedXaml = InterchangeChecks.Decode(System.Text.Encoding.UTF8.GetBytes(xaml!), DataFormats.Xaml);
                rows.Add(new { Operation = "actual-table-copy", First = first, Last = last, Text = plain,
                    Formats = formats.Order(StringComparer.Ordinal).ToArray(), RtfText = new TextRange(decodedRtf.ContentStart, decodedRtf.ContentEnd).Text,
                    RtfStyles = TargetedChecks.Styles(decodedRtf), XamlText = new TextRange(decodedXaml.ContentStart, decodedXaml.ContentEnd).Text, XamlStyles = TargetedChecks.Styles(decodedXaml) });
                if (scope == spans)
                {
                    selected.Single().Clone().Select();
                    rows.Add(new { Operation = "selected-span-clone-select", First = first, Last = last, Text = text.GetSelection().Select(value => value.GetText(-1)).ToArray() });
                    var cloneSequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
                    IndependentUiaClient.ChordObserved(acknowledgement, process.MainWindowHandle, 0x11, 0x43);
                    string? cloned = null;
                    IndependentUiaClient.Wait(() => LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != cloneSequence && (cloned = clipboard.Read(() => Clipboard.GetDataObject()?.GetData(DataFormats.UnicodeText, false) as string)) is not null);
                    rows.Add(new { Operation = "selected-span-clone-copy", First = first, Last = last, Text = cloned });
                }
            }
            foreach (var target in new[] { "bold", "العربيّة", "Final marker." })
            foreach (var unit in Enum.GetValues<TextUnit>())
            {
                var found = text.DocumentRange.FindText(target, false, false) ?? throw new IOException("Missing target " + target);
                var expanded = found.Clone(); expanded.ExpandToEnclosingUnit(unit);
                rows.Add(new { Target = target, Unit = unit.ToString(), Operation = "expand", Text = expanded.GetText(-1) });
                foreach (var count in new[] { -2, -1, 0, 1, 2 })
                foreach (var collapsed in new[] { false, true })
                {
                    TextPatternRange Range() { var range = found.Clone(); if (collapsed) range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start); return range; }
                    var moving = Range(); var moved = moving.Move(unit, count);
                    rows.Add(new { Target = target, Unit = unit.ToString(), Operation = "move", Count = count, Collapsed = collapsed, Moved = moved, Text = moving.GetText(-1), Degenerate = moving.CompareEndpoints(TextPatternRangeEndpoint.Start, moving, TextPatternRangeEndpoint.End) == 0 });
                    foreach (var endpoint in Enum.GetValues<TextPatternRangeEndpoint>())
                    {
                        moving = Range(); moved = moving.MoveEndpointByUnit(endpoint, unit, count);
                        rows.Add(new { Target = target, Unit = unit.ToString(), Operation = "endpoint-" + endpoint, Count = count, Collapsed = collapsed, Moved = moved, Text = moving.GetText(-1), Degenerate = moving.CompareEndpoints(TextPatternRangeEndpoint.Start, moving, TextPatternRangeEndpoint.End) == 0 });
                    }
                }
            }
            foreach (var target in new[] { "bold", "code", "العربيّة", "Final marker." })
            {
                var range = text.DocumentRange.FindText(target, false, false)!;
                rows.Add(new { Operation = "clone-compare", Target = target, Equal = range.Compare(range.Clone()) });
                foreach (var operation in new[] { "add-selection", "remove-selection" })
                {
                    var status = "Succeeded";
                    try { if (operation == "add-selection") range.AddToSelection(); else range.RemoveFromSelection(); }
                    catch (InvalidOperationException) { status = "InvalidOperationException"; }
                    rows.Add(new { Operation = operation, Target = target, Status = status });
                }
                foreach (var attribute in new[] { TextPattern.IsReadOnlyAttribute, TextPattern.IsHiddenAttribute, TextPattern.CultureAttribute, TextPattern.FontNameAttribute, TextPattern.FontSizeAttribute,
                    TextPattern.FontWeightAttribute, TextPattern.IsItalicAttribute, TextPattern.IsSubscriptAttribute, TextPattern.IsSuperscriptAttribute,
                    TextPattern.UnderlineStyleAttribute, TextPattern.StrikethroughStyleAttribute, TextPattern.ForegroundColorAttribute, TextPattern.BackgroundColorAttribute })
                {
                    var value = range.GetAttributeValue(attribute);
                    var label = value == AutomationElement.NotSupported ? "NotSupported" : value == TextPattern.MixedAttributeValue ? "Mixed" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                    rows.Add(new { Operation = "attribute", Target = target, Attribute = attribute.ProgrammaticName, Value = label });
                    if (value == AutomationElement.NotSupported || value == TextPattern.MixedAttributeValue) continue;
                    foreach (var backward in new[] { false, true })
                    {
                        var found = range.FindAttribute(attribute, value, backward);
                        rows.Add(new { Operation = "find-attribute", Target = target, Attribute = attribute.ProgrammaticName, Backward = backward, Text = found?.GetText(-1) });
                    }
                }
            }
            foreach (var shift in new[] { false, true })
            {
                var caret = text.DocumentRange.FindText("Latin", false, false)!;
                caret.ScrollIntoView(true); caret.MoveEndpointByRange(TextPatternRangeEndpoint.End, caret, TextPatternRangeEndpoint.Start); caret.Select(); document.SetFocus();
                foreach (var key in new byte[] { 0x27, 0x25 })
                for (var step = 0; step < 27; step++)
                {
                    if (shift) IndependentUiaClient.ChordObserved(acknowledgement, process.MainWindowHandle, 0x10, key);
                    else IndependentUiaClient.ChordObserved(acknowledgement, process.MainWindowHandle, key);
                    var selection = text.GetSelection().Single(); var prefix = text.DocumentRange.Clone(); prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, selection, TextPatternRangeEndpoint.End);
                    rows.Add(new { Operation = shift ? "actual-shift-arrow" : "actual-arrow", Key = key, Step = step, Text = selection.GetText(-1), EndPrefix = prefix.GetText(-1) });
                }
            }
            foreach (var keys in new byte[][] { [0x24], [0x23], [0x26], [0x28], [0x21], [0x22], [0x11,0x24], [0x11,0x23], [0x11,0x25], [0x11,0x27],
                [0x10,0x24], [0x10,0x23], [0x10,0x26], [0x10,0x28], [0x11,0x10,0x25], [0x11,0x10,0x27] })
            {
                var caret = text.DocumentRange.FindText("Latin", false, false)!; caret.ScrollIntoView(true);
                caret.MoveEndpointByRange(TextPatternRangeEndpoint.End, caret, TextPatternRangeEndpoint.Start); caret.Select(); document.SetFocus();
                IndependentUiaClient.ChordObserved(acknowledgement, process.MainWindowHandle, keys);
                var selection = text.GetSelection().Single(); var prefix = text.DocumentRange.Clone(); prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, selection, TextPatternRangeEndpoint.End);
                rows.Add(new { Operation = "actual-key-vector", Keys = string.Join('+', keys.Select(key => key.ToString("X2"))), Text = selection.GetText(-1), EndPrefix = prefix.GetText(-1) });
            }
            Program.Write(Path.Combine(output, "operations.json"), rows); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "operations.json"), rows); Program.Write(Path.Combine(output, "failure.json"), new { Error = error.ToString() }); return 1; }
        finally { Program.Write(Path.Combine(output, "clipboard-readiness.json"), clipboardReads); IndependentUiaClient.ReleaseOwnedInput(); }
    }
}
