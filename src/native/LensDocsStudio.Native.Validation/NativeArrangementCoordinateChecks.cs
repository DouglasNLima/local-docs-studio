using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

/// <summary>Coordinate and WPF scaling regressions. These do not certify operating-system DPI transitions.</summary>
internal static class NativeArrangementCoordinateChecks
{
    public static void Run(string output)
    {
        if (Directory.Exists(output)) throw new IOException("Use a fresh coordinate evidence directory.");
        Directory.CreateDirectory(output); NativeArrangementLatencyChecks.Initialise();
        var checks = new List<string>(); var samples = new List<object>(); var passed = false;
        try
        {
            foreach (var (family, source) in Sources())
            {
                var folder = System.IO.Path.Combine(output, family); Directory.CreateDirectory(folder);
                var sourcePath = System.IO.Path.Combine(folder, "diagram.mmd"); File.WriteAllText(sourcePath, source);
                var sourceBytes = File.ReadAllBytes(sourcePath);
                using var view = new NativeArrangementLatencyChecks.Observation(source, folder);
                var window = view.Window; window.Show(); NativeArrangementLatencyChecks.Until(() => view.Ready);
                var dpi = VisualTreeHelper.GetDpi(window); var actualDpi = view.Dpi();
                Check(!Directory.Exists(System.IO.Path.Combine(folder, ".lens-docs")), "Opening coordinate acceptance creates no workspace metadata: " + family);
                var engine = (ArrangementEngine)NativeArrangementLatencyChecks.Field(window, "engine")!;
                var canonicalSource = engine.Canonical.Render.Source; var canonicalSvg = engine.Canonical.Render.Svg;
                var id = engine.Canonical.State.Positions.Keys.First(); var position = engine.Canonical.State.Positions[id];
                var moved = engine.Canonical.State.Move(id, engine.Boxes.Values.Max(b => b.Right) + 100, engine.Boxes.Values.Max(b => b.Bottom) + 100).TogglePin(id);
                NativeArrangementLatencyChecks.Await((Task)Call(window, "ApplyState", moved)!);
                Check(view.Session.Current.State.Positions[id].Pinned, "Pinned moved state is accepted before coordinate transformations: " + family);
                var accepted = view.Session.Current; var prepared = new NativeSvgService().Prepare(accepted.Render.Svg!);
                var png = NativeSvgService.ToExportPng(view.Inspection.Drawing);
                var overlay = (DiagramInspectionOverlay)window.FindName("Overlay");
                foreach (var simulated in new[] { 1d, 1.25, 1.5, 2d })
                foreach (var zoomMode in new[] { "Fit", "100%", "200%" })
                {
                    // Scaling the viewport deliberately exercises WPF's transform chain,
                    // while leaving the OS DPI unchanged and explicitly reported above.
                    view.Viewport.LayoutTransform = new ScaleTransform(simulated, simulated);
                    window.UpdateLayout();
                    if (zoomMode == "Fit") Call(window, "Fit", window, new RoutedEventArgs());
                    else view.Zoom.Value = zoomMode == "100%" ? 100 : 200;
                    window.UpdateLayout();
                    var zoom = view.Zoom.Value / 100; var node = view.Inspection.Entities.Single(n => n.Identity.EndsWith(":" + id, StringComparison.Ordinal));
                    var centre = new Point(node.Shape.Bounds.X + node.Shape.Bounds.Width / 2, node.Shape.Bounds.Y + node.Shape.Bounds.Height / 2);
                    foreach (var scroll in new[] { "origin", "node", "end" })
                    {
                        var x = scroll switch { "origin" => 0, "node" => Math.Max(0, centre.X * zoom - view.Viewport.ViewportWidth / 2), _ => view.Viewport.ScrollableWidth };
                        var y = scroll switch { "origin" => 0, "node" => Math.Max(0, centre.Y * zoom - view.Viewport.ViewportHeight / 2), _ => view.Viewport.ScrollableHeight };
                        view.Viewport.ScrollToHorizontalOffset(x); view.Viewport.ScrollToVerticalOffset(y); window.UpdateLayout();
                        var screen = view.Drawing.PointToScreen(centre); var recovered = view.Drawing.PointFromScreen(screen);
                        samples.Add(new { Kind = "round-trip-observation", Family = family, Simulated = simulated, Zoom = zoom, Scroll = scroll,
                            CentreX = centre.X, CentreY = centre.Y, ScreenX = screen.X, ScreenY = screen.Y, RecoveredX = recovered.X, RecoveredY = recovered.Y,
                            PhysicalErrorX = Math.Abs(centre.X - recovered.X) * zoom * simulated * dpi.DpiScaleX,
                            PhysicalErrorY = Math.Abs(centre.Y - recovered.Y) * zoom * simulated * dpi.DpiScaleY });
                        // PointToScreen crosses the integer Win32 POINT boundary. Its
                        // nearest-pixel rounding can magnify in drawing units at Fit.
                        Check(Math.Abs(centre.X - recovered.X) * zoom * simulated * dpi.DpiScaleX <= .50001
                            && Math.Abs(centre.Y - recovered.Y) * zoom * simulated * dpi.DpiScaleY <= .50001,
                            "Screen round-trip stays within the derived half-physical-pixel quantisation bound: " + family);
                        Check(view.Inspection.Hit(recovered, zoom) == node.Identity, "Visible bounds retain the exact moved node hit identity: " + family);
                        var nextPhysical = screen + new Vector(19, 13);
                        var layoutDelta = view.Drawing.PointFromScreen(nextPhysical) - recovered;
                        var viewportDelta = view.Viewport.PointFromScreen(nextPhysical) - view.Viewport.PointFromScreen(screen);
                        Check(Math.Abs(layoutDelta.X - viewportDelta.X / zoom) < .00001 && Math.Abs(layoutDelta.Y - viewportDelta.Y / zoom) < .00001,
                            "Viewport delta applies zoom once and does not double-apply DPI: " + family);
                        var expectedGhost = centre + new Vector(viewportDelta.X / zoom, viewportDelta.Y / zoom);
                        var toWindow = view.Drawing.TransformToAncestor(window);
                        var unquantisedGhost = toWindow.Transform(expectedGhost) - toWindow.Transform(centre);
                        var ghostScreen = view.Drawing.PointToScreen(expectedGhost);
                        samples.Add(new { Kind = "ghost-vector-observation", Family = family, Simulated = simulated, Zoom = zoom, Scroll = scroll,
                            ExpectedScreenX = nextPhysical.X, ExpectedScreenY = nextPhysical.Y, ActualScreenX = ghostScreen.X, ActualScreenY = ghostScreen.Y,
                            ExpectedPhysicalDeltaX = 19, ExpectedPhysicalDeltaY = 13,
                            UnquantisedPhysicalDeltaX = unquantisedGhost.X * dpi.DpiScaleX, UnquantisedPhysicalDeltaY = unquantisedGhost.Y * dpi.DpiScaleY,
                            ScreenEndpointErrorX = Math.Abs(ghostScreen.X - nextPhysical.X), ScreenEndpointErrorY = Math.Abs(ghostScreen.Y - nextPhysical.Y) });
                        Check(Math.Abs(unquantisedGhost.X * dpi.DpiScaleX - 19) < .00001 && Math.Abs(unquantisedGhost.Y * dpi.DpiScaleY - 13) < .00001,
                            "The unquantised drag-ghost equation produces the exact physical movement vector: " + family);
                        // Both source and destination cross Win32's integer pixel boundary.
                        // Their independent half-pixel roundings can differ by one pixel.
                        Check(Math.Abs(ghostScreen.X - nextPhysical.X) <= 1.00001 && Math.Abs(ghostScreen.Y - nextPhysical.Y) <= 1.00001,
                            "The ghost screen endpoints stay within their derived one-pixel combined quantisation bound: " + family);
                        var oneUnit = toWindow.Transform(centre + new Vector(1, 1)) - toWindow.Transform(centre);
                        Check(Math.Abs(oneUnit.X * dpi.DpiScaleX - dpi.DpiScaleX * simulated * zoom) < .00001
                            && Math.Abs(oneUnit.Y * dpi.DpiScaleY - dpi.DpiScaleY * simulated * zoom) < .00001,
                            "The actual WPF transform contains each DPI, simulated scale and zoom factor once: " + family);
                        var viewportPoint = view.Viewport.PointFromScreen(screen);
                        var visibleCentre = viewportPoint.X >= 0 && viewportPoint.X <= view.Viewport.ActualWidth && viewportPoint.Y >= 0 && viewportPoint.Y <= view.Viewport.ActualHeight;
                        samples.Add(new { Family = family, SimulationScalePercent = simulated * 100, ZoomMode = zoomMode, ActualZoomPercent = view.Zoom.Value,
                            Scroll = scroll, HorizontalOffset = view.Viewport.HorizontalOffset, VerticalOffset = view.Viewport.VerticalOffset,
                            NodeCentreInsideViewport = visibleCentre, RoundTrip = true, SingleDpiAndZoomApplication = true, PhysicalScaleChanged = false });
                    }
                    foreach (var edge in view.Inspection.Connections.Where(e => e.LabelBounds.HasValue))
                    {
                        var bounds = edge.LabelBounds!.Value; var centreLabel = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                        var recoveredLabel = view.Drawing.PointFromScreen(view.Drawing.PointToScreen(centreLabel));
                        Check(view.Inspection.Hit(recoveredLabel, zoom) == edge.Identity, "Moved relationship labels retain exact occurrence hit identity: " + family);
                    }
                    Check(ReferenceEquals(accepted, view.Session.Current) && view.Session.Current.State.Positions[id].Pinned,
                        "Viewport scaling, scrolling and zoom cannot mutate accepted positions or pins: " + family);
                    Check(new NativeSvgService().Prepare(view.Session.Current.Render.Svg!) == prepared, "Vector export is independent of viewport transformation: " + family);
                    Check(overlay.Inspection == view.Inspection, "The overlay and drawing retain the same accepted inspection snapshot: " + family);
                }
                var transformedPng = NativeSvgService.ToExportPng(view.Inspection.Drawing);
                Check(png.SequenceEqual(transformedPng), "PNG export is independent of simulated scale, zoom and scroll: " + family);
                Check(engine.Canonical.Render.Source == canonicalSource && engine.Canonical.Render.Svg == canonicalSvg && File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes),
                    "Coordinate tests preserve exact Mermaid and canonical SVG: " + family);
                samples.Add(new { Family = family, ActualOsDpi = actualDpi, ActualPhysicalDpiConfigurations = new[] { dpi.PixelsPerInchX / 96 * 100 },
                    PhysicalMonitorTransitions = "NOT VERIFIED", SimulationPercentages = new[] { 100, 125, 150, 200 }, KeyboardAndCapture = "Actual input covered separately by the hosted pointer harness; these checks exercise WPF transforms and coordinate mathematics only." });
            }
            passed = true;
        }
        finally
        {
            File.WriteAllText(System.IO.Path.Combine(output, "coordinates.json"), JsonSerializer.Serialize(new
            {
                Status = passed ? "PASS" : "FAIL", Scope = "Shown WPF coordinate mathematics and explicit viewport ScaleTransform simulation; no simulated mouse event is presented as actual pointer input or physical DPI certification. Integer Win32 PointToScreen round trips permit their derived half-physical-pixel quantisation and two independently rounded screen endpoints permit at most one physical pixel combined error; the independent unquantised WPF movement/transform equations retain 1e-5 tolerances.",
                Checks = checks, Samples = samples
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
    }
    private static object? Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
    private static IEnumerable<(string Family, string Source)> Sources()
    {
        var er = new StringBuilder("erDiagram\n A ||--o{ B : owns\n A ||--o{ B : reviews\n B ||--o{ B : revisits\n");
        foreach (var entity in new[] { "A", "B" }) { er.Append(entity + " {\n"); for (var i = 0; i < 18; i++) er.Append($" string attribute_{i:D2}\n"); er.Append("}\n"); }
        yield return ("ER", er.ToString());
        yield return ("Flowchart", "flowchart LR\n A[Start] -->|next| B{Decision}\n A -->|review| B\n B -->|retry| B\n B -->|finish| C[Finish]\n");
    }
}
