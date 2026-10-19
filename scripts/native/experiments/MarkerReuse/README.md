# Diagnostic native marker reuse

This tool is an isolated framework experiment, outside the product solution and
package path. It reads an installed WPF assembly and writes a distinct workspace
copy. It never edits the installed runtime. The output is modified and is not a
Microsoft-signed or trusted runtime; no installer or distributable is produced.

Stock `ListParaClient.ValidateVisual` creates one `ListMarkerLine`, then formats,
draws and disposes a `TextLine` for each marker. The experiment retains one line
for `Disc` markers within that existing validation scope, draws it at each item's
existing position and disposes it before the existing marker handle. Numbered
markers retain their original path. Native `List`, `ListItem`, paragraphs, runs,
content, source mapping, selection and automation remain present.

The tool recognises the expected framework IL shape before writing a copy. It
records original/modified hashes and method instructions. A scalar counter proves
reuse executed. There is no cross-scope, document, appearance or dispatcher cache.

`Test-WpfMarkerReuseExperiment.ps1` requires disposable hosted Windows, creates
two private runtime folders and executes one compiled harness against both. It
disables ReadyToRun equally because rewriting a ReadyToRun image produces IL.
This is a distinct experimental temperature, never pooled with stock-runtime
acceptance. The six fixture bytes, original clock and 250 ms threshold remain.
Content, structure, styled text, selection/navigation and rasters are compared
outside every timed sample; raw failed samples are retained.

The dedicated workflow independently repeats the fixed 66-sample policy with
reversed initial pair order. A successful experiment would only inform a decision
about a framework fix or a different native layout architecture. It cannot certify
the application, packages or the unchanged stock-runtime acceptance path.

Reference implementations: [WPF list visual validation](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListParaClient.cs),
[marker line formatting](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListMarkerLine.cs).
