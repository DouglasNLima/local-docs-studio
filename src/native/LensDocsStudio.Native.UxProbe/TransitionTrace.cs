using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

// Opt-in test instrumentation only. No authored text, paths, forced layout or repair.
// The same compiled probe runs without these subscriptions in the uninstrumented cohort.
internal sealed class TransitionTrace : IDisposable
{
    private readonly MainWindow window;
    private readonly WorkspaceViewModel model;
    private readonly DocumentPreview preview;
    private readonly TextEditor editor;
    private readonly StreamWriter writer;
    private readonly DependencyPropertyDescriptor documentProperty;
    private readonly Dictionary<object, int> identities = new(ReferenceEqualityComparer.Instance);
    private string? previous;
    private string action = "startup";
    private int count;
    private bool writing;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal TransitionTrace(MainWindow window, WorkspaceViewModel model, string output)
    {
        this.window = window; this.model = model;
        preview = (DocumentPreview)window.FindName("Preview"); editor = (TextEditor)window.FindName("Editor");
        writer = new(Path.Combine(output, "transitions.jsonl"), false) { AutoFlush = true };
        documentProperty = DependencyPropertyDescriptor.FromProperty(FlowDocumentReader.DocumentProperty, typeof(DocumentPreview));
        documentProperty.AddValueChanged(preview, DocumentChanged);
        preview.LayoutUpdated += LayoutUpdated;
        preview.IsVisibleChanged += VisibilityChanged;
        preview.SizeChanged += SizeChanged;
        model.PropertyChanged += ModelChanged;
        Snapshot("subscribed");
    }

    private T? Field<T>(string name) => (T?)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);
    private int Id(object? value) { if (value is null) return 0; if (!identities.TryGetValue(value, out var id)) identities[value] = id = identities.Count + 1; return id; }
    internal void Boundary(string name) { action = name; Snapshot(name); }
    internal void Stage(string stage, long request)
    {
        // Worker markers carry their own request identity. Never pretend a later UI snapshot
        // describes the exact worker boundary, or synchronously marshal a worker to the UI.
        if (window.Dispatcher.CheckAccess() && (stage.StartsWith("request/", StringComparison.Ordinal) || stage.StartsWith("shell/", StringComparison.Ordinal)))
            Snapshot("stage/" + stage, request: request);
    }
    private void DocumentChanged(object? sender, EventArgs e) => Snapshot("document-property");
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Snapshot("visibility");
    private void SizeChanged(object sender, SizeChangedEventArgs e) => Snapshot("size");
    private void LayoutUpdated(object? sender, EventArgs e) => Snapshot("layout", changedOnly: true);
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName is "Layout" or "PreviewStatus" or "ActiveDocument") Snapshot("model/" + e.PropertyName); }

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Tree(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private object Geometry(FrameworkElement element)
    {
        var scroll = element as IScrollInfo;
        Rect? bounds = null;
        try { if (element.IsVisible && PresentationSource.FromVisual(element) is not null) bounds = element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize)); }
        catch (InvalidOperationException) { }
        return new { id = Id(element), type = element.GetType().FullName, element.Name,
            parent = Id(VisualTreeHelper.GetParent(element)), children = VisualTreeHelper.GetChildrenCount(element),
            element.IsLoaded, element.IsVisible, visibility = element.Visibility.ToString(), element.Opacity,
            element.ActualWidth, element.ActualHeight, desired = element.DesiredSize.ToString(), render = element.RenderSize.ToString(),
            element.IsMeasureValid, element.IsArrangeValid, attached = PresentationSource.FromVisual(element) is not null,
            bounds = bounds?.ToString(), clip = element.Clip?.Bounds.ToString(), element.ClipToBounds,
            document = Id(element is FlowDocumentReader r ? r.Document : element is FlowDocumentScrollViewer s ? s.Document : element is FlowDocumentPageViewer p ? p.Document : null),
            offset = scroll is null ? null : new { scroll.HorizontalOffset, scroll.VerticalOffset, scroll.ExtentWidth, scroll.ExtentHeight, scroll.ViewportWidth, scroll.ViewportHeight },
            scrolling = element is ScrollViewer v ? new { v.HorizontalOffset, v.VerticalOffset, v.ExtentWidth, v.ExtentHeight, v.ViewportWidth, v.ViewportHeight, v.ScrollableHeight } : null };
    }
    private void Snapshot(string boundary, bool changedOnly = false, long? request = null)
    {
        if (writing) return;
        if (count >= 4000) throw new IOException("Transition trace exceeded its predeclared 4,000-snapshot bound.");
        writing = true;
        try
        {
            var result = Field<PreviewResult>("rendered");
            var version = Field<ITextSourceVersion>("renderedVersion");
            var active = model.ActiveDocument;
            var nodes = Tree(preview).OfType<FrameworkElement>().Where(e => e == preview || e is ScrollViewer or FlowDocumentScrollViewer or FlowDocumentPageViewer or Decorator or ScrollContentPresenter
                || e.GetType().Name.Contains("Document", StringComparison.Ordinal)).Take(100).Select(Geometry).ToArray();
            var data = new { action, layout = model.Layout, status = model.PreviewStatus,
                active = active?.Id, renderedOwner = Field<Guid?>("renderedDocumentId"),
                sourceGeneration = Field<long>("diagnosticSourceGeneration"), acceptedGeneration = Field<long>("diagnosticAcceptedGeneration"),
                request = Field<int>("diagnosticRequest"), phase = Field<string>("diagnosticPhase"),
                sourceVersion = Id(active?.EditorDocument.Version), renderedVersion = Id(version),
                versionMatches = version is not null && active is not null && version.BelongsToSameDocumentAs(active.EditorDocument.Version) && version.CompareAge(active.EditorDocument.Version) == 0,
                editorOwnerMatches = active?.EditorDocument == editor.Document, sourceLength = active?.EditorDocument.TextLength,
                rendered = Id(result?.Document), attached = Id(preview.Document), blocks = preview.Document?.Blocks.Count,
                contentStartLayout = preview.Document?.ContentStart.HasValidLayout,
                contentEndLayout = preview.Document?.ContentEnd.HasValidLayout,
                pendingViewport = Field<Guid?>("pendingViewportDocument"), viewportGeneration = Field<long>("viewportGeneration"),
                syncGeneration = Field<long>("syncGeneration"), previewOffset = active?.PreviewSourceOffset,
                contentHost = window.FindName("PreviewContentHost") is FrameworkElement host ? Geometry(host) : null,
                itemStatus = AutomationProperties.GetItemStatus(preview), mode = preview.ViewingMode.ToString(), preview.Zoom,
                nodes };
            var payload = JsonSerializer.Serialize(data, Json);
            if (changedOnly && payload == previous) return;
            previous = payload; count++;
            writer.WriteLine(JsonSerializer.Serialize(new { tick = Stopwatch.GetTimestamp(), boundary, eventRequest = request, state = data }, Json));
            if (writer.BaseStream.Position > 16 * 1024 * 1024) throw new IOException("Transition trace exceeded its 16 MiB bound.");
        }
        finally { writing = false; }
    }
    public void Dispose()
    {
        documentProperty.RemoveValueChanged(preview, DocumentChanged);
        preview.LayoutUpdated -= LayoutUpdated; preview.IsVisibleChanged -= VisibilityChanged; preview.SizeChanged -= SizeChanged;
        model.PropertyChanged -= ModelChanged; writer.Dispose();
    }
}
