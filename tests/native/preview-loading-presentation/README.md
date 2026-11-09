# Offline preview-loading presentation controls

Run from the repository root with .NET 10:

```powershell
dotnet run --project tests/native/preview-loading-presentation/PreviewLoadingControls.csproj -c Release --no-launch-profile
```

This isolated console project compiles the actual production `MainWindow.PreviewLoading.cs`, `PreviewLoadingView.xaml.cs` and `PreviewLoadingState.cs` source files. Small inert UI/timer facades and a scripted monotonic clock replace platform dependencies inside this test assembly only. There is no WPF runtime, native application launch, window, UI Automation, desktop input, OS appearance change, sleep or timing measurement. No external package is required.

The boundary control supplies 199.9 ms for presentation and 200.1 ms for a subsequent clock read. The original presenter collapses the indicator, then decides that the delay has already elapsed and leaves the presentation timer stopped. A held current request stays Busy but cannot display its indicator. The corrected presenter uses one observation for both decisions and rearms the timer; an explicit later tick exposes the busy indicator.

The remaining controls cover stable times before/at/after the same boundary, warning/error completion, cancellation, supersession, appearance replacement, incoming-document masking and Editor layout. They assert observable presentation ownership and progress, not a benchmark threshold or a count of clock reads. The 200 ms visibility delay, 25-second native harness wait and separate 250 ms benchmark are unchanged.

For a retained baseline source directory containing those three files, supply `-p:ProductRoot=<absolute-directory>`. The original source is expected to fail the boundary, superseded-request and appearance-replacement controls. That failure is a negative control, not a live replay or a candidate acceptance failure.

These controls demonstrate the presentation race under deterministic input. They do not establish that the original hosted timeout took this path: that artefact has no terminal loading/timer snapshot. They do not replace the existing shown-window lifecycle/UIA check or qualify physical presentation, Windows broadcasts, screen readers or timing performance.
