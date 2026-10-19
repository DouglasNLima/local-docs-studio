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
        var source = Source + string.Join("\n\n", Enumerable.Range(0, 40).Select(index => "Wrapped paragraph " + index + " " + string.Join(' ', Enumerable.Repeat("measured words", 15)))) + "\n\nFinal marker.\n";
        var path = Path.Combine(output, "fixture.md"); File.WriteAllText(path, source);
        var gaps = new List<double>(); var last = Stopwatch.GetTimestamp();
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = Stopwatch.GetTimestamp(); gaps.Add((at - last) * 1000d / Stopwatch.Frequency); last = at; }; timer.Start();
        try
        {
            foreach (var side in new[] { "stock", "candidate" })
            {
                var folder = Path.Combine(output, side); Directory.CreateDirectory(folder);
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
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { "--range-client", folder, Environment.ProcessId.ToString(), side }) start.ArgumentList.Add(argument);
                using var client = (await Task.Run(() => Process.Start(start)))!;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120)); await client.WaitForExitAsync(deadline.Token);
                if (client.ExitCode != 0) throw new IOException("Independent range oracle failed: " + side);
            }
            var stockRows = File.ReadAllText(Path.Combine(output, "stock", "operations.json"));
            var candidateRows = File.ReadAllText(Path.Combine(output, "candidate", "operations.json"));
            var matched = stockRows == candidateRows;
            Program.Write(Path.Combine(output, "range-parity.json"), new { Status = matched ? "PASS" : "FAIL", OperationsEqual = matched, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps });
            if (!matched) throw new IOException("Actual stock/candidate range or visual-direction keyboard operations differ; all rows retained.");
        }
        finally { timer.Stop(); }
    }
    public static int Client(string output, int processId, string side)
    {
        var rows = new List<object>();
        try
        {
            using var process = Process.GetProcessById(processId); var window = AutomationElement.FromHandle(process.MainWindowHandle);
            AutomationElement? document = null;
            IndependentUiaClient.Wait(() => (document = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, side == "stock" ? "RangeOracleDocument" : "ExperimentalDocument"))) is not null);
            var text = (TextPattern)document!.GetCurrentPattern(TextPattern.Pattern);
            foreach (var target in new[] { "bold", "العربيّة", "Final marker." })
            foreach (var unit in Enum.GetValues<TextUnit>())
            {
                var found = text.DocumentRange.FindText(target, false, false) ?? throw new IOException("Missing target " + target);
                var expanded = found.Clone(); expanded.ExpandToEnclosingUnit(unit);
                rows.Add(new { Target = target, Unit = unit.ToString(), Operation = "expand", Text = expanded.GetText(-1) });
                foreach (var count in new[] { -1, 0, 1 })
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
            foreach (var shift in new[] { false, true })
            {
                var caret = text.DocumentRange.FindText("Latin", false, false)!;
                caret.ScrollIntoView(true); caret.MoveEndpointByRange(TextPatternRangeEndpoint.End, caret, TextPatternRangeEndpoint.Start); caret.Select(); document.SetFocus();
                foreach (var key in new byte[] { 0x27, 0x25 })
                for (var step = 0; step < 27; step++)
                {
                    if (shift) IndependentUiaClient.Chord(0x10, key); else IndependentUiaClient.Chord(key);
                    var selection = text.GetSelection().Single(); var prefix = text.DocumentRange.Clone(); prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, selection, TextPatternRangeEndpoint.End);
                    rows.Add(new { Operation = shift ? "actual-shift-arrow" : "actual-arrow", Key = key, Step = step, Text = selection.GetText(-1), EndPrefix = prefix.GetText(-1) });
                }
            }
            Program.Write(Path.Combine(output, "operations.json"), rows); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "operations.json"), rows); Program.Write(Path.Combine(output, "failure.json"), new { Error = error.ToString() }); return 1; }
    }
}
