param([Parameter(Mandatory)][string]$SourceRoot, [Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference = 'Stop'
$validation = Join-Path $SourceRoot 'src/native/LensDocsStudio.Native.Validation'
$target = Join-Path $validation 'NativePreviewLoadingChecks.cs'
$original = [IO.File]::ReadAllText($target).Replace("`r`n", "`n")
$text = $original
function Replace-Exact([string]$Before, [string]$After) {
    if ($script:text.Split($Before, [StringSplitOptions]::None).Count -ne 2) { throw "Overlay anchor is not unique: $Before" }
    $script:text = $script:text.Replace($Before, $After)
}
Replace-Exact '    public static void Run(' "    private static Action<string>? diagnosticBoundary;`n    public static void Run("
Replace-Exact '        var gateHeld = false;' @'
        var gateHeld = false;
        using var lifecycle = new NativePreviewLifecycleRecorder();
        lifecycle.OnDispatcher = () => window.Dispatcher.CheckAccess();
        var posted = 0L; var completedOperations = 0L; var aborted = 0L;
        var pending = new System.Collections.Concurrent.ConcurrentDictionary<DispatcherOperation, byte>();
        void Posted(object? sender, DispatcherHookEventArgs e) { Interlocked.Increment(ref posted); pending.TryAdd(e.Operation, 0); }
        void Completed(object? sender, DispatcherHookEventArgs e) { Interlocked.Increment(ref completedOperations); pending.TryRemove(e.Operation, out _); }
        void Aborted(object? sender, DispatcherHookEventArgs e) { Interlocked.Increment(ref aborted); pending.TryRemove(e.Operation, out _); }
        window.Dispatcher.Hooks.OperationPosted += Posted;
        window.Dispatcher.Hooks.OperationCompleted += Completed;
        window.Dispatcher.Hooks.OperationAborted += Aborted;
        Task? capturedRender = null;
        object? capturedRequest = null;
        object Element(FrameworkElement item) => new { Visibility = item.Visibility.ToString(), item.IsVisible,
            item.IsLoaded, item.IsMeasureValid, item.IsArrangeValid, item.ActualWidth, item.ActualHeight,
            ParentVisibility = (item.Parent as UIElement)?.Visibility.ToString(),
            ScreenOrigin = item.IsVisible ? item.PointToScreen(new Point()).ToString() : "Unsupported while hidden",
            Dpi = VisualTreeHelper.GetDpi(item).ToString(),
            PeerOffscreen = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(item)?.IsOffscreen() };
        lifecycle.Observe = () => new {
            Observation = "Direct synchronous WPF/reflection observations; not an atomic external UIA snapshot",
            Window = new { Handle = new WindowInteropHelper(window).Handle.ToInt64(), Identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(window),
                window.IsActive, window.IsVisible, window.ActualWidth, window.ActualHeight },
            ActiveDocument = model.ActiveDocument?.Id, SelectedDocument = (window.FindName("DocumentTabs") as System.Windows.Controls.Primitives.Selector)?.SelectedItem is DocumentViewModel selected ? selected.Id : (Guid?)null,
            DisplayedDocument = (Field("displayedTab") as DocumentViewModel)?.Id,
            SourceGeneration = Field("diagnosticSourceGeneration"), AcceptedGeneration = Field("diagnosticAcceptedGeneration"),
            SourceVersion = model.ActiveDocument?.EditorDocument.Version.GetHashCode(), SourceLength = model.ActiveDocument?.EditorDocument.TextLength,
            model.Theme, ReadingMode = model.Preferences.Preview?.ReadingMode, model.Layout,
            SystemParameters.HighContrast, Request = Field("diagnosticRequest"), Phase = Field("diagnosticPhase"), Error = Field("diagnosticError"),
            CapturedTaskStatus = capturedRender?.Status.ToString(), CapturedRequest = capturedRequest,
            CurrentTaskStatus = "Unsupported: shell does not retain its timer task; request events identify its phases",
            Cancelled = (Field("previewCancellation") as CancellationTokenSource)?.IsCancellationRequested,
            Loading = new { loading.Busy, loading.Refresh, loading.Generation, loading.Context, Phase = loading.Phase.ToString(),
                loading.PendingSince, VisibleByClock = loading.IsVisible(((Stopwatch)Field("previewLoadingClock")!).Elapsed) },
            View = Element(view), Host = Element(host), Ring = Element(ring), Preview = Element(preview), Pane = Element((FrameworkElement)window.FindName("PreviewPane")),
            ring.IsIndeterminate, RingStatus = AutomationProperties.GetItemStatus(ring), PreviewStatus = AutomationProperties.GetItemStatus(preview), status.Text,
            GateCapacity = gate.CurrentCount, GateHeldByControl = gateHeld, TimerEnabled = queuedPreview.IsEnabled,
            LoadingDelayEnabled = ((DispatcherTimer)Field("previewLoadingDelay")!).IsEnabled,
            CurrentAcceptedSource = CurrentAcceptedSource(), RenderedDocument = Field("renderedDocumentId"), Closing = Field("closing"),
            Dispatcher = new { Posted = posted, Completed = completedOperations, Aborted = aborted,
                PendingCount = pending.Count, Pending = pending.Keys.Take(64).Select(p => new { Priority = p.Priority.ToString(), Status = p.Status.ToString() }).ToArray(),
                Fitting = typeof(DocumentPreview).GetField("fitting", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(preview) is DispatcherOperation fit ? fit.Status.ToString() : null },
            ActualExternalUiaAtBoundary = "Unsupported synchronously on provider dispatcher; ordinary shown check records actual UIA separately"
        };
        void ObserveBoundary(string name)
        {
            try
            {
                lifecycle.Record(name, new { SourceSha256 = model.ActiveDocument is { } active
                    ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(active.EditorDocument.Text))) : null });
                if (name.Contains("timeout", StringComparison.Ordinal))
                {
                    lifecycle.Flush(Path.Combine(output, "lifecycle-timeout-before-cleanup.json"));
                    try { Capture("timeout-before-cleanup"); } catch (Exception error) { lifecycle.Record("capture-failed", error.ToString()); }
                }
            }
            catch (Exception error) { lifecycle.Record("boundary-observation-failed", new { Boundary = name, Error = error.ToString() }); }
        }
        diagnosticBoundary = ObserveBoundary;
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        { try { if (e.PropertyName is "ActiveDocument" or "Theme" or "Layout") lifecycle.Record("model/" + e.PropertyName); } catch (Exception error) { lifecycle.Record("model-observation-failed", error.ToString()); } }
        model.PropertyChanged += Changed;
        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        { try { lifecycle.Record("visibility/" + ((FrameworkElement)sender).Name, new { Before = e.OldValue.ToString(), After = e.NewValue.ToString() }); } catch (Exception error) { lifecycle.Record("visibility-observation-failed", error.ToString()); } }
        view.IsVisibleChanged += VisibilityChanged; ring.IsVisibleChanged += VisibilityChanged;
'@
Replace-Exact '                model.Theme = theme; Call("ApplyTheme");' @'
                lifecycle.Scenario = theme + "/" + mode;
                ObserveBoundary("scenario-before-setup");
                model.Theme = theme; Call("ApplyTheme");
'@
Replace-Exact '                Hold(); var first = Render();' @'
                ObserveBoundary("incoming-before-hold");
                Hold(); var first = Render();
'@
Replace-Exact '                var capturedBeforeRelease = RequestObservation(first);' @'
                if (Environment.GetEnvironmentVariable("LENS_PREVIEW_LIFECYCLE_CONTROL") == "never")
                {
                    ObserveBoundary("never-control-before-wait");
                    Until(() => !loading.Busy && !queuedPreview.IsEnabled);
                    throw new IOException("Never-completing control unexpectedly completed while its gate was held.");
                }
                var capturedBeforeRelease = RequestObservation(first);
'@
Replace-Exact '                        if (found is null || found.IsOffscreen' @'
                        try { lifecycle.Record("actual-uia-ring", found is null ? new { Found = false } : (object)new { Found = true, found.IsOffscreen, Bounds = found.BoundingRectangle.ToString(), ItemStatus = found.Properties.ItemStatus.Value }); }
                        catch (Exception error) { lifecycle.Record("actual-uia-observation-failed", error.ToString()); }
                        if (found is null || found.IsOffscreen
'@
Replace-Exact "        finally`n        {`n            if (gateHeld) gate.Release(); Call(`"CompleteClose`");" @'
        catch (Exception error)
        {
            lifecycle.Record("original-exception-before-cleanup", error.ToString());
            lifecycle.Flush(Path.Combine(output, "lifecycle-failure-before-cleanup.json"));
            throw;
        }
        finally
        {
            ObserveBoundary("finally-before-cleanup");
            lifecycle.Flush(Path.Combine(output, "lifecycle.json"));
            diagnosticBoundary = null;
            model.PropertyChanged -= Changed;
            view.IsVisibleChanged -= VisibilityChanged; ring.IsVisibleChanged -= VisibilityChanged;
            window.Dispatcher.Hooks.OperationPosted -= Posted;
            window.Dispatcher.Hooks.OperationCompleted -= Completed;
            window.Dispatcher.Hooks.OperationAborted -= Aborted;
            if (gateHeld) gate.Release(); Call("CompleteClose");
'@
Replace-Exact '        void Hold() { gate.Wait(); gateHeld = true; }' '        void Hold() { gate.Wait(); gateHeld = true; ObserveBoundary("after-hold"); }'
Replace-Exact '        void Release() { gate.Release(); gateHeld = false; }' '        void Release() { ObserveBoundary("before-release"); gate.Release(); gateHeld = false; ObserveBoundary("after-release"); }'
Replace-Exact '        Task Render() => (Task)Call("UpdatePreviewAsync", false)!;' @'
        Task Render() { ObserveBoundary("before-render"); capturedRender = (Task)Call("UpdatePreviewAsync", false)!; capturedRequest = Field("diagnosticRequest"); ObserveBoundary("after-render"); return capturedRender; }
'@
Replace-Exact '        if (!ready()) throw new TimeoutException("Preview lifecycle check exceeded its bounded wait.");' @'
        if (!ready()) { diagnosticBoundary?.Invoke("timeout-before-cleanup"); throw new TimeoutException("Preview lifecycle check exceeded its bounded wait."); }
'@
[IO.File]::WriteAllText($target, $text)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NativePreviewLifecycleRecorder.cs') -Destination $validation
New-Item -ItemType Directory -Force $Evidence | Out-Null
git -C $SourceRoot diff -- src/native/LensDocsStudio.Native.Validation/NativePreviewLoadingChecks.cs | Set-Content (Join-Path $Evidence 'overlay.patch')
Get-FileHash $target,(Join-Path $validation 'NativePreviewLifecycleRecorder.cs') -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content (Join-Path $Evidence 'overlay-hashes.json')
