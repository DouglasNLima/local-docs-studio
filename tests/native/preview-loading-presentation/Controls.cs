using System.Text.Json;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native;
using System.Windows;

var results = new List<object>();
var failed = 0;
foreach (var test in MainWindow.Controls())
{
    try { test.Run(); results.Add(new { test.Name, Status = "PASS" }); }
    catch (Exception error) { failed++; results.Add(new { test.Name, Status = "FAIL", Error = error.Message }); }
}
Console.WriteLine(JsonSerializer.Serialize(new { Scope = "Offline production-source-linked presentation controls; scripted clock and inert UI/timer facades; no WPF process, window, desktop input or OS appearance change", Failed = failed, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;

namespace LensDocsStudio.Native
{
    // Same-namespace resolution substitutes time only in this isolated test assembly.
    // The linked production file is compiled unchanged; no string replacement is used.
    public sealed class Stopwatch
    {
        private readonly Queue<TimeSpan> readings = new();
        private TimeSpan last;
        public int Reads { get; private set; }
        public static Stopwatch StartNew() => new();
        public TimeSpan Elapsed { get { Reads++; if (readings.TryDequeue(out var next)) last = next; return last; } }
        public void At(params double[] milliseconds)
        { readings.Clear(); Reads = 0; foreach (var value in milliseconds) readings.Enqueue(TimeSpan.FromMilliseconds(value)); }
    }

    public sealed class Document { public Guid Id { get; } = Guid.NewGuid(); }
    public sealed class Model { public Document? ActiveDocument { get; set; } = new(); public string Layout { get; set; } = "Split"; public string Theme { get; set; } = "Light"; }

    public partial class MainWindow
    {
        private readonly Model model = new();
        private readonly System.Windows.Threading.DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
        private readonly ProbeControl PreviewContentHost = new(), Preview = new(), PreviewStatusLabel = new();
        private readonly Rendering.PreviewLoadingView PreviewLoading = new();
        private bool closing = false;
        private long diagnosticSourceGeneration = 0;
        private Guid? renderedDocumentId = null;
        private object? rendered = null;
        private string CurrentDiagramTheme => "Auto";
        public event EventHandler? Closed;
        private Task UpdatePreviewAsync() => Task.CompletedTask;
        public MainWindow() => InitialisePreviewLoading();
        public void CloseForControl() => Closed?.Invoke(this, EventArgs.Empty);

        public static IEnumerable<(string Name, Action Run)> Controls()
        {
            yield return ("Crossing the visibility boundary cannot strand a held busy request", () =>
            {
                var w = Held();
                w.previewLoadingClock.At(199.9, 200.1, 200.2);
                w.PresentPreviewLoading();
                Assert(w.previewLoading.Busy && w.Preview.ItemStatus.StartsWith("Busy"), "Pending request lost truthful Busy status");
                Assert(w.PreviewLoading.Visibility == Visibility.Visible || w.previewLoadingDelay.IsEnabled,
                    $"Busy request stranded: visibility={w.PreviewLoading.Visibility}, timer={w.previewLoadingDelay.IsEnabled}, clockReads={w.previewLoadingClock.Reads}, phase={w.previewLoading.Phase}");
                w.previewLoadingClock.At(200.2); w.previewLoadingDelay.Fire();
                Assert(w.PreviewLoading.Visibility == Visibility.Visible && w.PreviewLoading.Ring.IsIndeterminate, "Held request never exposed its busy indicator");
            });
            yield return ("A pre-boundary tick rearms and a post-boundary tick presents", () =>
            {
                var w = Held(); w.previewLoadingClock.At(199.8); w.PresentPreviewLoading();
                Assert(w.PreviewLoading.Visibility == Visibility.Collapsed && w.previewLoadingDelay.IsEnabled, "Early tick must rearm");
                Assert(w.previewLoadingDelay.Interval > TimeSpan.Zero, "Presentation timer interval must remain positive");
                w.previewLoadingClock.At(200.1); w.previewLoadingDelay.Fire();
                Assert(w.PreviewLoading.Visibility == Visibility.Visible && !w.previewLoadingDelay.IsEnabled, "Elapsed delay must show the indicator and stop the timer");
            });
            yield return ("A tick exactly on the boundary presents busy content", () =>
            {
                var w = Held(); w.previewLoadingClock.At(200); w.PresentPreviewLoading();
                Assert(w.PreviewLoading.Visibility == Visibility.Visible && !w.previewLoadingDelay.IsEnabled, "Boundary visibility differs from the unchanged delay");
            });
            yield return ("Fast completed work remains Ready without a spinner", () =>
            {
                var w = Held(); w.previewLoadingClock.At(100); w.FinishPreviewLoading(w.previewLoading.Generation, PreviewLoadingPhase.Completed);
                Assert(!w.previewLoading.Busy && w.PreviewLoading.Visibility == Visibility.Collapsed && !w.previewLoadingDelay.IsEnabled && w.Preview.ItemStatus == "Ready", "Completed work retained busy presentation");
            });
            yield return ("Mermaid warnings complete without leaving a stale spinner", () =>
            {
                var w = Held(); w.previewLoadingClock.At(210); w.PresentPreviewLoading();
                w.FinishPreviewLoading(w.previewLoading.Generation, PreviewLoadingPhase.Completed, warnings: true);
                Assert(!w.previewLoading.Busy && w.PreviewLoading.Visibility == Visibility.Collapsed && w.Preview.ItemStatus == "Ready", "Completed warning result retained busy presentation");
                Assert(w.PreviewStatusLabel.ItemStatus == "Preview ready with rendering errors", "Warning announcement became false success");
            });
            yield return ("Failed work shows its error without a spinning ring", () =>
            {
                var w = Held(); w.previewLoadingClock.At(210); w.FinishPreviewLoading(w.previewLoading.Generation, PreviewLoadingPhase.Failed);
                Assert(!w.previewLoading.Busy && w.PreviewLoading.Visibility == Visibility.Visible && !w.PreviewLoading.Ring.IsIndeterminate && w.Preview.ItemStatus == "Preview unavailable", "Failed preview lost truthful error feedback");
            });
            yield return ("Cancellation stops busy presentation", () =>
            {
                var w = Held(); w.previewLoadingClock.At(210); w.PresentPreviewLoading();
                w.FinishPreviewLoading(w.previewLoading.Generation, PreviewLoadingPhase.Cancelled);
                Assert(!w.previewLoading.Busy && w.PreviewLoading.Visibility == Visibility.Collapsed && !w.previewLoadingDelay.IsEnabled, "Cancellation retained a stale spinner");
            });
            yield return ("Superseded cancellation cannot stop a replacement indicator", () =>
            {
                var w = Held(); var old = w.previewLoading.Generation;
                w.previewLoading.Queue(w.previewLoading.Context! with { Revision = 2 }, true, TimeSpan.FromMilliseconds(150));
                w.previewLoadingClock.At(199.9, 200.1, 200.2); w.PresentPreviewLoading();
                var replacement = w.previewLoading.Generation;
                w.FinishPreviewLoading(old, PreviewLoadingPhase.Cancelled);
                Assert(w.previewLoading.Generation == replacement && w.previewLoading.Busy, "Old request cancelled its replacement");
                Assert(w.PreviewLoading.Visibility == Visibility.Visible || w.previewLoadingDelay.IsEnabled, "Replacement lost its indicator timer");
            });
            yield return ("Appearance replacement retains its own pending presentation", () =>
            {
                var w = Held(); var old = w.previewLoading.Generation;
                w.previewLoading.Queue(w.previewLoading.Context! with { Appearance = "Dark/True/False/Auto" }, true, TimeSpan.FromMilliseconds(150));
                w.previewLoadingClock.At(199.9, 200.1, 200.2); w.PresentPreviewLoading();
                w.FinishPreviewLoading(old, PreviewLoadingPhase.Completed);
                Assert(w.previewLoading.Busy && w.previewLoading.Context!.Appearance.StartsWith("Dark"), "Superseded appearance published completion");
                Assert(w.PreviewLoading.Visibility == Visibility.Visible || w.previewLoadingDelay.IsEnabled, "Appearance replacement lost its indicator timer");
            });
            yield return ("A new document masks old content during its visibility delay", () =>
            {
                var w = Held(); var old = w.previewLoading.Generation;
                w.model.ActiveDocument = new(); w.previewLoadingClock.At(500);
                w.QueuePreviewLoading(start: true); w.FinishPreviewLoading(old, PreviewLoadingPhase.Completed);
                Assert(w.previewLoading.Context!.Document == w.model.ActiveDocument.Id && w.previewLoading.Busy && w.PreviewContentHost.Visibility == Visibility.Hidden, "Old document completion exposed outgoing content");
                Assert(w.previewLoadingDelay.IsEnabled, "New document lost its delayed indicator");
            });
            yield return ("Editor layout resets presentation and its timer", () =>
            {
                var w = Held(); w.previewLoadingClock.At(100); w.PresentPreviewLoading();
                w.model.Layout = "Editor"; w.QueuePreviewLoading();
                Assert(!w.previewLoading.Busy && w.previewLoading.Context is null && w.PreviewLoading.Visibility == Visibility.Collapsed && !w.previewLoadingDelay.IsEnabled, "Editor layout retained stale loading");
            });
        }

        private static MainWindow Held()
        {
            var w = new MainWindow();
            w.previewLoading.Start(new(w.model.ActiveDocument!.Id, 1, "Light/False/False/Auto", "Split"), false, TimeSpan.Zero);
            return w;
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}

namespace LensDocsStudio.Native.Rendering
{
    public partial class PreviewLoadingView
    {
        public readonly ProbeControl Ring = new(), Container = new(), ContentPanel = new(), Status = new();
        private void InitializeComponent() { }
    }
}
namespace LensDocsStudio.Native.Platform { public static class ThemeService { public static bool IsDark(string value) => value == "Dark"; } }
namespace System.Windows
{
    public enum Visibility { Visible, Hidden, Collapsed }
    public enum HorizontalAlignment { Right, Center }
    public enum VerticalAlignment { Top, Center }
    public sealed record Thickness(double Left, double Top, double Right, double Bottom);
    public sealed class RoutedEventArgs : EventArgs { }
    public static class SystemParameters { public static bool HighContrast => false; }
    public class ProbeControl
    {
        public Visibility Visibility { get; set; }
        public bool IsIndeterminate { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; }
        public VerticalAlignment VerticalAlignment { get; set; }
        public Controls.Orientation Orientation { get; set; }
        public Thickness? Margin { get; set; }
        public string Text { get; set; } = "";
        public string ItemStatus { get; set; } = "";
    }
}
namespace System.Windows.Controls { public enum Orientation { Horizontal, Vertical } public class UserControl : ProbeControl { } }
namespace System.Windows.Threading
{
    public enum DispatcherPriority { Background }
    public sealed class DispatcherTimer
    {
        public DispatcherTimer() { }
        public DispatcherTimer(DispatcherPriority priority) { }
        public bool IsEnabled { get; private set; }
        public TimeSpan Interval { get; set; }
        public event EventHandler? Tick;
        public void Start() => IsEnabled = true;
        public void Stop() => IsEnabled = false;
        public void Fire() { if (IsEnabled) Tick?.Invoke(this, EventArgs.Empty); }
    }
}
namespace System.Windows.Automation
{
    public enum AutomationLiveSetting { Polite }
    public enum AutomationNotificationKind { Other }
    public enum AutomationNotificationProcessing { MostRecent }
    public static class AutomationProperties
    {
        public static void SetItemStatus(ProbeControl control, string text) => control.ItemStatus = text;
        public static void SetLiveSetting(ProbeControl control, AutomationLiveSetting setting) { }
    }
}
namespace System.Windows.Automation.Peers
{
    public sealed class UIElementAutomationPeer
    {
        public static UIElementAutomationPeer? FromElement(ProbeControl control) => null;
        public void RaiseNotificationEvent(Automation.AutomationNotificationKind kind, Automation.AutomationNotificationProcessing processing, string text, string id) { }
    }
}
