using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

/// <summary>Compiled WPF/geometry regressions. Visible process acceptance is recorded separately.</summary>
internal static class NativeDesktopUxChecks
{
    public static void Run(string output, string root)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Directory.CreateDirectory(output);
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(app.Resources, "Dark");
        var checks = new List<string>(); var performance = new List<object>();
        var document = new FlowDocument();
        var paragraph = new Paragraph(); paragraph.Inlines.Add(new Run("con")); paragraph.Inlines.Add(new Bold(new Run("tact")));
        paragraph.Inlines.Add(new Run(" Contact "));
        var formula = new InlineUIContainer(new TextBlock { Text = "formula" }); paragraph.Inlines.Add(formula);
        document.Blocks.Add(paragraph); document.Blocks.Add(new Paragraph(new Run("after")));
        var index = Await(PreviewFindIndex.CreateAsync(document, [new("x_contact", formula)], default));
        var matches = FindMatches.Collect(index.Text, "contact");
        Check(matches.Offsets.Count == 3, "Preview includes adjacent formatted runs and formula source once; controls are excluded");
        var range = index.Range(matches.Offsets[0], 7);
        Check(new TextRange(range.Start, range.End).Text == "contact", "Cross-run range maps to exact visible text");
        Check(index.Range(matches.Offsets[2], 7).Formula, "Formula source match maps to its rendered container");
        Check(FindMatches.Collect(index.Text, "contactafter").Offsets.Count == 0, "Block and formula boundaries cannot manufacture a match");
        using (var cancellation = new CancellationTokenSource())
        { cancellation.Cancel(); try { Await(PreviewFindIndex.CreateAsync(document, [], cancellation.Token)); throw new IOException("Cancelled index completed"); } catch (OperationCanceledException) { checks.Add("Cancelled preview indexing does not publish results"); } }

        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(output, "writes")), new SessionStore(Path.Combine(output, "state")), new WorkspaceService(), dialogs);
        var main = new MainWindow(model, dialogs, []);
        try
        {
            var editor = (TextEditor)main.FindName("Editor"); var query = (TextBox)main.FindName("FindText");
            var bar = (FrameworkElement)main.FindName("FindBar"); var counter = (TextBlock)main.FindName("FindCounter");
            bar.Visibility = Visibility.Visible; editor.Text = "contact Contact contact"; query.Text = "contact";
            Await((Task<bool>)Call(main, "RefreshFindAsync")!);
            Check(counter.Text == "1 of 3" && editor.SelectionStart == 0, "Initial editor query selects first occurrence with accurate counter");
            Await((Task)Call(main, "NavigateFindAsync", -1)!);
            Check(counter.Text == "3 of 3" && editor.SelectionStart == 16, "Previous wraps to last occurrence");
            Await((Task)Call(main, "NavigateFindAsync", 1)!);
            Check(counter.Text == "1 of 3", "Next wraps to first occurrence");
            editor.Document.Replace(0, 7, "changed");
            Check(counter.Text == "0 of 0", "Source edit synchronously invalidates stale matches");
            Await((Task<bool>)Call(main, "RefreshFindAsync")!);
            Check(counter.Text == "1 of 2" && editor.SelectionStart == 8, "Edited source recalculates actual results");
            editor.Document = new("no matches"); Call(main, "InvalidateFind"); Await((Task<bool>)Call(main, "RefreshFindAsync")!);
            Check(counter.Text == "0 of 0", "Document replacement cannot retain outgoing matches");
            query.Text = "missing"; Await((Task<bool>)Call(main, "RefreshFindAsync")!); Check(counter.Text == "0 of 0", "No results have no invalid current index");
            var preview = (DocumentPreview)main.FindName("Preview"); preview.Document = document;
            preview.MinZoom = 50; preview.MaxZoom = 200;
            Field(main, "searchPreview", true); query.Text = "contact"; Await((Task<bool>)Call(main, "RefreshFindAsync")!);
            Check(counter.Text == "1 of 2", "Preview counts its FlowDocument text independently of source; formula source requires metadata");
            foreach (var zoom in new[] { 50d, 125d, 200d, 100d })
            {
                preview.Zoom = zoom; Pump();
                var label = (Button)main.FindName("PreviewZoomLabel");
                Check(Convert.ToDouble(label.Content) == zoom && AutomationProperties.GetName(label).Contains(zoom.ToString("0"), StringComparison.Ordinal), "Preview zoom binding and accessible name follow " + zoom);
            }
        }
        finally { Call(main, "CompleteClose"); }

        foreach (var (file, entities, connections) in new[] { ("inspection.mmd", 3, 4), ("er-22-68.mmd", 22, 68), ("er-13-46.mmd", 13, 46) })
        {
            var source = File.ReadAllText(Path.Combine(root, "tests/fixtures/native-desktop-ux", file));
            var watch = Stopwatch.StartNew(); var render = new DiagramService().Render(source, true); var renderMs = watch.Elapsed.TotalMilliseconds;
            Check(render.Success, file + ": canonical render succeeded: " + render.Error);
            var svg = new NativeSvgService(); watch.Restart(); var view = DiagramInspection.Create(render, svg)!; var metadataMs = watch.Elapsed.TotalMilliseconds;
            Check(view.Entities.Count == entities && view.Connections.Count == connections, file + ": verified every entity and relationship occurrence");
            var originalSvg = render.Svg; var originalPng = NativeSvgService.ToPng(view.Drawing); var sha = Convert.ToHexString(SHA256.HashData(originalPng));
            Check(sha == Convert.ToHexString(SHA256.HashData(NativeSvgService.ToPng(svg.Render(render.Svg!)))), file + ": inspection drawing preserves canonical preview/export pixels");
            var chosen = view.Entities[0]; var bounds = chosen.Shape.Bounds; var centre = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            Check(view.Hit(centre, 1) == chosen.Identity, file + ": exact entity geometry hit");
            foreach (var edge in view.Connections.Take(8))
            {
                var path = edge.Path.GetFlattenedPathGeometry();
                path.GetPointAtFractionLength(.35, out var point, out _);
                var hit = view.Hit(point, 1);
                if (hit is null || view.Entities.Any(entity => entity.Identity == hit)) continue;
                var identified = view.Connections.Single(item => item.Identity == hit);
                Check(identified.LabelBounds?.Contains(point) == true || identified.Path.StrokeContains(new Pen(Brushes.Black, 11), point), file + ": precomputed hit agrees with canonical WPF geometry");
            }
            var connected = view.Connected(chosen.Identity);
            Check(connected.Count > 0 && connected.All(edge => edge.First == chosen.Identity || edge.Second == chosen.Identity), file + ": selection includes only directly connected occurrences");
            Check(view.Connected(view.Connections[0].Identity).Count == 1 && view.Connected(null).Count == 0, file + ": exact relationship selection and clear selection");
            var window = new DiagramWindow(view.Drawing, _ => { }, view);
            try
            {
                Call(window, "SelectInspection", chosen.Identity);
                var selector = (ComboBox)window.FindName("InspectionChoices");
                Check((string?)selector.SelectedValue == chosen.Identity && selector.Items.Count == 1 + entities + connections, file + ": accessible selector has every verified occurrence");
                var slider = (Slider)window.FindName("Zoom");
                foreach (var zoom in new[] { 1d, 25d, 100d, 250d })
                {
                    slider.Value = zoom;
                    Check(((TextBlock)window.FindName("ZoomLabel")).Text == zoom + "%" && (string?)selector.SelectedValue == chosen.Identity, file + ": selection survives zoom " + zoom);
                }
                Check(Convert.ToHexString(SHA256.HashData(NativeSvgService.ToPng(view.Drawing))) == sha && render.Svg == originalSvg, file + ": highlight state does not change canonical PNG pixels or SVG bytes");
                Call(window, "SelectInspection", (object?)null);
                Check(((TextBlock)window.FindName("DiagramStatus")).Text.StartsWith("Click an entity", StringComparison.Ordinal), file + ": clearing selection restores inspection guidance");
            }
            finally { window.Close(); }
            var hitWidth = Math.Max(1, view.Drawing.Width); var hitHeight = Math.Max(1, view.Drawing.Height);
            watch.Restart(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) _ = view.Hit(new Point(i % hitWidth, i % hitHeight), .25);
            performance.Add(new { file, renderMs, metadataMs, ThousandHitsMs = watch.Elapsed.TotalMilliseconds, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated, ExportPngSha256 = sha });
        }
        var flow = new DiagramService().Render("flowchart LR\nA[Application] -->|owns| B[Contact]\nA -->|reviews| B\nB --> B\nB --> C[Operation]\n", true);
        var flowView = DiagramInspection.Create(flow, new())!;
        Check(flowView.Entities.Count == 3 && flowView.Connections.Count == 4 && flowView.Connected("node:B").Count == 4, "Canonical Flowchart parallel and self connections retain verified direct identities");
        Check(Convert.ToHexString(SHA256.HashData(NativeSvgService.ToPng(flowView.Drawing))) == Convert.ToHexString(SHA256.HashData(NativeSvgService.ToPng(new NativeSvgService().Render(flow.Svg!)))), "Flowchart inspection preserves canonical output pixels");
        Check(DiagramInspection.Create(new DiagramResult(flow.Source, flow.Svg, null), new()) is null, "Arbitrary SVG without canonical metadata grants no interaction authority");
        var unsupported = new DiagramService().Render("pie\n \"One\" : 1\n");
        Check(DiagramInspection.Create(unsupported, new()) is null, "Unsupported family renders without misleading inspection metadata");
        File.WriteAllText(Path.Combine(output, "desktop-ux.json"), JsonSerializer.Serialize(new { status = "PASS", Scope = "Compiled WPF and exact canonical geometry; actual UI acceptance is separate", checks, performance }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string description) { if (!condition) throw new IOException(description); checks.Add(description); }
    }
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private static T Await<T>(Task<T> task) { Await((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Await(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(15)) { Pump(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Desktop UX regression exceeded its bounded wait.");
        task.GetAwaiter().GetResult();
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
