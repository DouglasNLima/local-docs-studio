using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

// Passive, opt-in evidence. No dispatcher blocking, product-state mutation,
// forced rendering, authored text or file writes in lifecycle callbacks.
internal sealed class CompletionTrace : EventListener
{
    private readonly MainWindow window;
    private readonly WorkspaceViewModel model;
    private readonly DocumentPreview primary, secondary;
    private readonly List<object> records = [];
    private readonly List<(DependencyObject Owner, DependencyPropertyDescriptor Property, EventHandler Handler)> subscriptions = [];
    private readonly List<(EventInfo Event, EventHandler Handler)> mediaEvents = [];
    private readonly Dictionary<int, Operation> operations = [];
    private readonly HwndSource hwnd;
    private readonly object mediaContext;
    private TaskCompletionSource<Operation>? armed;
    private TaskCompletionSource<long>? commitWaiter;
    private long commitMinimum;
    private long lastMutation, lastVisualMutation, lastCommit;
    private long mutationEpoch;
    private readonly Dictionary<string, long> observedEpochs = [];
    private readonly Dictionary<string, int> unchangedEvents = [];
    private string target = "", action = "";
    private Operation? offered;
    private int staleReady;
    private bool overflow;
    private readonly List<Delivery> deliveries = [];
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    internal sealed class Operation(int id, string reader, CancellationTokenSource? cancellation, long start, long generation)
    {
        internal int Id { get; } = id;
        internal string Reader { get; } = reader;
        internal CancellationTokenSource? Cancellation { get; } = cancellation;
        internal long Start { get; } = start;
        internal long Generation { get; } = generation;
        internal long? End, Cancelled;
        internal string? Outcome;
        internal CancellationTokenRegistration Registration;
    }
    private sealed record Delivery(long Tick, int Operation, string Reader, string Action, bool PendingTask,
        bool SameCancellation, bool CancellationRequested, string Boundary, string InputTarget);

    internal CompletionTrace(MainWindow window, WorkspaceViewModel model)
    {
        this.window = window; this.model = model;
        primary = (DocumentPreview)window.FindName("Preview"); secondary = Field<DocumentPreview>("comparisonPreview")!;
        hwnd = (HwndSource)PresentationSource.FromVisual(window);
        hwnd.AddHook(NativeInput);
        ComponentDispatcher.ThreadFilterMessage += NativeKeyboard;
        InputManager.Current.PreProcessInput += Input;
        model.PropertyChanged += ModelChanged;
        Watch((TextBlock)window.FindName("PreviewStatusLabel"), TextBlock.TextProperty, "status-text");
        foreach (var reader in new[] { primary, secondary })
        {
            var name = reader == primary ? "primary" : "secondary";
            Watch(reader, AutomationProperties.ItemStatusProperty, name + "/item-status");
            Watch(reader, FlowDocumentReader.PageNumberProperty, name + "/page-number");
            Watch(reader, FlowDocumentReader.PageCountProperty, name + "/page-count");
            var native = Tree(reader).OfType<FlowDocumentPageViewer>().FirstOrDefault();
            if (native is not null)
            {
                Watch(native, DocumentViewerBase.MasterPageNumberProperty, name + "/native-page-number");
                // Background pagination updates native PageCount for each page.
                // Sample that count at the meaningful boundaries below; retain
                // every public counter and bound text change without per-page noise.
            }
            var page = reader.Template.FindName("PageNumber", reader) as Label;
            if (page is not null) { Watch(page, ContentControl.ContentProperty, name + "/page-text"); Watch(page, AutomationProperties.NameProperty, name + "/page-name"); }
        }
        // Subscribe once, before inputs. These internal events observe natural
        // WPF work. Do not subscribe to Rendering: even its internal event keeps
        // WPF's frame clock active. No SyncFlush or RenderTargetBitmap is invoked.
        var type = typeof(Visual).Assembly.GetType("System.Windows.Media.MediaContext", throwOnError: true)!;
        mediaContext = type.GetMethod("From", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [window.Dispatcher])!;
        foreach (var name in new[] { "CommittingBatch", "RenderComplete" })
        {
            var info = type.GetEvent(name, Private) ?? throw new MissingMemberException(type.FullName, name);
            EventHandler handler = (_, _) => Media(name);
            info.GetAddMethod(true)!.Invoke(mediaContext, [handler]); mediaEvents.Add((info, handler));
        }
        window.Dispatcher.Hooks.OperationPosted += Posted;
        window.Dispatcher.Hooks.OperationStarted += Started;
        window.Dispatcher.Hooks.OperationCompleted += Completed;
        Snapshot("subscribed");
    }
    protected override void OnEventSourceCreated(EventSource source)
    { if (source.Name == "LensDocsStudio-Native-PreviewRestoration") EnableEvents(source, EventLevel.Verbose); }
    protected override void OnEventWritten(EventWrittenEventArgs args)
    {
        if (window is null || !window.Dispatcher.CheckAccess() || args.EventId != 1 || args.Payload is not { Count: 3 }) return;
        var phase = (string)args.Payload[0]!; var readerId = (int)args.Payload[1]!; var id = (int)args.Payload[2]!;
        var reader = readerId == RuntimeHelpers.GetHashCode(primary) ? "primary"
            : readerId == RuntimeHelpers.GetHashCode(secondary) ? "secondary" : "unknown";
        var tick = Stopwatch.GetTimestamp();
        mutationEpoch++;
        if (phase == "started")
        {
            var cancellation = Field<CancellationTokenSource>(reader == "primary" ? "previewLayoutCancellation" : "comparisonLayoutCancellation");
            var operation = new Operation(id, reader, cancellation, tick, Field<long>(reader == "primary" ? "syncGeneration" : "comparisonGeneration")); operations.Add(id, operation);
            if (cancellation is not null) operation.Registration = cancellation.Token.Register(() =>
            { operation.Cancelled = Stopwatch.GetTimestamp(); Add(new { tick = operation.Cancelled, boundary = "token-cancelled", operation = id, reader }); });
            Snapshot("anchor/started", new { operation = id, reader });
            if (reader == target && armed is not null && !armed.Task.IsCompleted && cancellation is not null)
            { offered = operation; armed.TrySetResult(operation); }
        }
        else if (operations.TryGetValue(id, out var operation))
        { operation.End = tick; operation.Outcome = phase; Snapshot("anchor/" + phase, new { operation = id, reader }); }
    }
    private T? Field<T>(string name) => typeof(MainWindow).GetField(name, Private)?.GetValue(window) is T value ? value : default;
    internal static DocumentViewModel? ExpectedOwner(MainWindow window, WorkspaceViewModel model, bool secondary)
    {
        var slots = typeof(MainWindow).GetField("comparisonSlots", Private)?.GetValue(window) as Array;
        if (slots is null) return secondary ? null : model.ActiveDocument;
        var active = (int)typeof(MainWindow).GetField("comparisonActive", Private)!.GetValue(window)!;
        var slot = slots.GetValue(secondary ? 1 - active : active);
        return slot?.GetType().GetField("Document")?.GetValue(slot) as DocumentViewModel;
    }
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Tree(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private object Reader(DocumentPreview reader, bool second)
    {
        var owner = ExpectedOwner(window, model, second);
        var result = Field<PreviewResult>(second ? "comparisonPreviewResult" : "rendered");
        var version = Field<ITextSourceVersion>(second ? "comparisonRenderedVersion" : "renderedVersion");
        var page = reader.Template?.FindName("PageNumber", reader) as Label;
        var native = Tree(reader).OfType<FlowDocumentPageViewer>().FirstOrDefault();
        var current = owner is not null && version is not null && version.BelongsToSameDocumentAs(owner.EditorDocument.Version)
            && version.CompareAge(owner.EditorDocument.Version) == 0 && result?.Document == reader.Document
            && Field<Guid?>(second ? "comparisonRenderedId" : "renderedDocumentId") == owner.Id && (second || owner == model.ActiveDocument);
        var status = AutomationProperties.GetItemStatus(reader);
        var cancellation = Field<CancellationTokenSource>(second ? "comparisonLayoutCancellation" : "previewLayoutCancellation");
        return new { owner = owner?.Id, current, attached = reader.Document is null ? 0 : RuntimeHelpers.GetHashCode(reader.Document),
            currentVersion = owner is null ? 0 : RuntimeHelpers.GetHashCode(owner.EditorDocument.Version),
            renderedVersion = version is null ? 0 : RuntimeHelpers.GetHashCode(version),
            status, reader.IsVisible, reader.IsMeasureValid, reader.IsArrangeValid, reader.ActualWidth, reader.ActualHeight,
            mode = reader.ViewingMode.ToString(), reader.PageNumber, reader.PageCount,
            nativePage = native?.MasterPageNumber, nativeCount = native?.PageCount,
            pageText = page?.Content?.ToString(), pageName = page is null ? null : AutomationProperties.GetName(page),
            pageVisible = page?.IsVisible, pending = cancellation is not null,
            cancellation = cancellation is null ? 0 : RuntimeHelpers.GetHashCode(cancellation), cancelled = cancellation?.IsCancellationRequested,
            currentOperation = cancellation is null ? null : operations.Values.LastOrDefault(o => o.Cancellation == cancellation)?.Id };
    }
    internal object Snapshot(string boundary, object? detail = null)
    {
        var tick = Stopwatch.GetTimestamp();
        var state = new { tick, boundary, action, detail, active = model.ActiveDocument?.Id,
            status = model.PreviewStatus, statusText = ((TextBlock)window.FindName("PreviewStatusLabel")).Text,
            syncGeneration = Field<long>("syncGeneration"), comparisonGeneration = Field<long>("comparisonGeneration"),
            primary = Reader(primary, false), secondary = Reader(secondary, true) };
        Add(state); return state;
    }
    private void Add(object record)
    {
        lock (records) { if (records.Count >= 4000) { overflow = true; return; } records.Add(record); }
    }
    private void Watch(DependencyObject owner, DependencyProperty property, string name)
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(property, owner.GetType());
        EventHandler handler = (_, _) =>
        {
            lastMutation = Stopwatch.GetTimestamp();
            if (property == TextBlock.TextProperty || property == ContentControl.ContentProperty)
                lastVisualMutation = lastMutation;
            mutationEpoch++;
            if (property == AutomationProperties.ItemStatusProperty && owner is DocumentPreview reader
                && reader.IsVisible && AutomationProperties.GetItemStatus(reader) == "Ready")
            {
                var second = reader == secondary;
                var document = ExpectedOwner(window, model, second);
                var result = Field<PreviewResult>(second ? "comparisonPreviewResult" : "rendered");
                var version = Field<ITextSourceVersion>(second ? "comparisonRenderedVersion" : "renderedVersion");
                var token = Field<CancellationTokenSource>(second ? "comparisonLayoutCancellation" : "previewLayoutCancellation");
                var operation = token is null ? null : operations.Values.LastOrDefault(o => o.Cancellation == token);
                if (document is null || result?.Document != reader.Document || version is null
                    || !version.BelongsToSameDocumentAs(document.EditorDocument.Version) || version.CompareAge(document.EditorDocument.Version) != 0
                    || Field<Guid?>(second ? "comparisonRenderedId" : "renderedDocumentId") != document.Id || !second && document != model.ActiveDocument
                    || token?.IsCancellationRequested == true || operation is not null && (operation.End is null || operation.Outcome != "restored"
                        || operation.Generation != Field<long>(second ? "comparisonGeneration" : "syncGeneration"))) staleReady++;
            }
            Snapshot("wpf/" + name);
        };
        descriptor.AddValueChanged(owner, handler); subscriptions.Add((owner, descriptor, handler));
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName is "PreviewStatus" or "ActiveDocument") { lastMutation = Stopwatch.GetTimestamp(); mutationEpoch++; Snapshot("model/" + args.PropertyName); } }
    private bool FirstSinceMutation(string boundary)
    {
        // OperationPosted can run on the posting thread rather than the UI thread.
        lock (records)
        {
            var currentEpoch = Volatile.Read(ref mutationEpoch);
            if (observedEpochs.TryGetValue(boundary, out var epoch) && epoch == currentEpoch)
            { unchangedEvents[boundary] = unchangedEvents.GetValueOrDefault(boundary) + 1; return false; }
            observedEpochs[boundary] = currentEpoch; return true;
        }
    }
    private void Media(string boundary)
    {
        var tick = Stopwatch.GetTimestamp();
        if (FirstSinceMutation("media/" + boundary)) Snapshot("media/" + boundary);
        if (boundary != "RenderComplete") return;
        lastCommit = tick;
        if (tick >= commitMinimum) commitWaiter?.TrySetResult(tick);
    }
    private void DispatcherEvent(string boundary, DispatcherHookEventArgs args)
    { if (args.Operation.Priority == DispatcherPriority.Render && FirstSinceMutation(boundary)) Add(new { tick = Stopwatch.GetTimestamp(), boundary, operation = RuntimeHelpers.GetHashCode(args.Operation) }); }
    private void Posted(object? sender, DispatcherHookEventArgs args) => DispatcherEvent("render-posted", args);
    private void Started(object? sender, DispatcherHookEventArgs args) => DispatcherEvent("render-started", args);
    private void Completed(object? sender, DispatcherHookEventArgs args) => DispatcherEvent("render-completed", args);
    internal Task<Operation> Arm(string reader, string interruption)
    { target = reader; action = interruption; offered = null; armed = new(TaskCreationOptions.RunContinuationsAsynchronously); return armed.Task; }
    internal void Offered(Operation operation, long tick) => Add(new { tick, boundary = "send-input", operation = operation.Id, reader = operation.Reader, action });
    private IntPtr NativeInput(IntPtr handle, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message is 0x100 or 0x101 or 0x104 or 0x105 or 0x201 or 0x202)
            Add(new { tick = Stopwatch.GetTimestamp(), boundary = "native-input", source = "hwnd-hook", handle = handle.ToInt64(), message, key = wparam.ToInt64(), offered = offered?.Id });
        return IntPtr.Zero;
    }
    private void NativeKeyboard(ref MSG message, ref bool handled)
    {
        // WPF's keyboard sink can consume key-down before the HWND hook. Observe
        // the message pump first, without changing its handled flag or contents.
        if (message.hwnd == hwnd.Handle && message.message is 0x100 or 0x101 or 0x104 or 0x105)
            Add(new { tick = Stopwatch.GetTimestamp(), boundary = "native-input", source = "thread-filter", handle = message.hwnd.ToInt64(),
                message = message.message, key = message.wParam.ToInt64(), offered = offered?.Id });
    }
    private static string InputTarget(DependencyObject? node)
    {
        while (node is not null)
        {
            var id = AutomationProperties.GetAutomationId(node); if (id.Length > 0) return id;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return "";
    }
    private void Input(object? sender, PreProcessInputEventArgs args)
    {
        if (offered is not { } operation) return;
        var input = args.StagingItem.Input;
        var expected = input is KeyEventArgs key && key.RoutedEvent == Keyboard.PreviewKeyDownEvent
                && (action == "switch" && key.Key == Key.Tab || action == "find" && key.Key == Key.Escape)
            || action == "close" && input is MouseButtonEventArgs mouse && mouse.ChangedButton == MouseButton.Left && mouse.RoutedEvent == Mouse.PreviewMouseUpEvent;
        if (!expected) return;
        var token = Field<CancellationTokenSource>(operation.Reader == "primary" ? "previewLayoutCancellation" : "comparisonLayoutCancellation");
        var delivery = new Delivery(Stopwatch.GetTimestamp(), operation.Id, operation.Reader, action, operation.End is null,
            token == operation.Cancellation, token?.IsCancellationRequested != false, "wpf-input-delivery",
            InputTarget(input.OriginalSource as DependencyObject ?? (action == "close" ? Mouse.DirectlyOver as DependencyObject : null)));
        deliveries.Add(delivery); Add(delivery);
        Snapshot("input-delivered", new { operation = operation.Id });
    }
    internal Task<long> CommitAfterCurrentChanges()
    {
        // ItemStatus and UIA names can change after the last visual commit and
        // schedule no new rendering. Fence actual bound text mutations only;
        // ownership and semantic publications remain separately recorded/audited.
        commitMinimum = lastVisualMutation;
        Snapshot("commit-fence", new { lastMutation, lastVisualMutation, lastCommit });
        if (lastCommit >= commitMinimum) return Task.FromResult(lastCommit);
        commitWaiter = new(TaskCreationOptions.RunContinuationsAsynchronously); return commitWaiter.Task;
    }
    internal object Verdict()
    {
        var qualified = deliveries.Where(d => d.PendingTask && d.SameCancellation && !d.CancellationRequested
            && (d.Action != "close" || d.InputTarget == "CloseComparison")).ToArray();
        var cancelled = qualified.All(d => operations[d.Operation].Cancelled >= d.Tick && operations[d.Operation].Outcome == "rejected");
        return new { pendingInterruption = qualified.Length == 0 ? "INCONCLUSIVE" : cancelled && staleReady == 0 ? "PASS" : "FAIL",
            qualifiedDeliveries = qualified.Length, cancellationsVerified = cancelled && qualified.Length > 0, staleReady,
            offeredOperation = offered?.Id, deliveries,
            operations = operations.Values.Select(o => new { o.Id, o.Reader, o.Start, o.End, o.Cancelled, o.Outcome, o.Generation }).ToArray() };
    }
    internal void Save(string output)
    {
        // Retain bounded partial evidence before reporting an overflow failure.
        object[] snapshot; Dictionary<string, int> unchanged;
        lock (records) { snapshot = records.ToArray(); unchanged = new(unchangedEvents); }
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        long bytes = 0; var written = 0; var byteOverflow = false;
        using (var stream = new StreamWriter(Path.Combine(output, "completion-trace.jsonl"), false, new UTF8Encoding(false)))
            foreach (var record in snapshot)
            {
                var line = JsonSerializer.Serialize(record, options);
                var length = Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                if (bytes + length > 16 * 1024 * 1024) { byteOverflow = true; break; }
                stream.WriteLine(line); bytes += length; written++;
            }
        Program.Write(output, "completion-trace-metadata.json", new { recordOverflow = overflow, byteOverflow, written, bytes, unchangedEvents = unchanged, verdict = Verdict(),
            scope = "All subscribed mutations and input events; first render boundary per mutation epoch, with unchanged render events counted separately" });
        if (overflow || byteOverflow) throw new IOException("Completion trace exceeded its 4,000-record or 16 MiB bound; partial evidence retained, acceptance inconclusive.");
    }
    public override void Dispose()
    {
        hwnd.RemoveHook(NativeInput); ComponentDispatcher.ThreadFilterMessage -= NativeKeyboard;
        InputManager.Current.PreProcessInput -= Input; model.PropertyChanged -= ModelChanged;
        foreach (var (owner, property, handler) in subscriptions) property.RemoveValueChanged(owner, handler);
        foreach (var (info, handler) in mediaEvents) info.GetRemoveMethod(true)!.Invoke(mediaContext, [handler]);
        foreach (var operation in operations.Values) operation.Registration.Dispose();
        window.Dispatcher.Hooks.OperationPosted -= Posted; window.Dispatcher.Hooks.OperationStarted -= Started; window.Dispatcher.Hooks.OperationCompleted -= Completed;
        base.Dispose();
    }
}
