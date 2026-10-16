using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeArtifactChecks
{
    public static void Run(string output, string? repository = null)
    {
        var root = Path.Combine(output, "artefact-validation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "review.zip");
        CreateFixtureArchive(archivePath, repository ?? Directory.GetCurrentDirectory());
        var imported = new MarkdownBundleService().ImportAsync(archivePath, Path.Combine(root, "imported")).GetAwaiter().GetResult();
        var bundle = imported.ArtifactBundle ?? throw new IOException("The native artefact fixture lost metadata.");
        var original = bundle.Entries.ToArray();
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(940, 780), new Size(700, 560) })
        {
            var model = new ArtifactReaderViewModel(bundle); var window = new ArtifactReaderWindow(model);
            var content = (Grid)window.Content; window.Content = null; content.DataContext = model;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native artefact reader validation") { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface;
            void Layout() { surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout(); }
            Layout();
            var query = (TextBox)window.FindName("Query"); var evidence = (ComboBox)window.FindName("Evidence"); var group = (ComboBox)window.FindName("Group"); var items = (ListBox)window.FindName("Items");
            evidence.SelectedItem = "candidate finding"; Layout();
            if (items.Items.Count != 1 || model.SelectedItem?.Path != "findings/candidate-risk.md" || !model.CanOpen || items.ActualHeight < 80
                || !model.Notes.Contains("Candidate findings remain candidate findings.", StringComparison.Ordinal))
                throw new IOException("The native artefact reader lost candidate filtering, safe path selection or usable controls.");
            foreach (var control in new FrameworkElement[] { query, evidence, group, items })
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))) throw new IOException("An artefact reader control has no accessible name.");
            var peer = new System.Windows.Automation.Peers.ListBoxAutomationPeer(items);
            if (!peer.GetChildren().Any(child => child.GetName().Contains("Findings", StringComparison.Ordinal) || child.GetName().Contains("Candidate", StringComparison.Ordinal)))
                throw new IOException("The artefact reader lost accessible group/item names.");
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var image = File.Create(Path.Combine(output, "native-artefacts-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(image);
            group.SelectedItem = "Diagrams"; Layout();
            if (items.Items.Count != 0 || model.CanOpen || model.SelectedItem is not null) throw new IOException("Empty artefact filters retained an openable document.");
            model.ResetCommand.Execute(null); query.Text = "runbook"; Layout();
            if (items.Items.Count != 1 || model.SelectedItem?.Group != "Documentation") throw new IOException("Artefact path/title search lost its original group.");
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        if (!original.SequenceEqual(bundle.Entries)) throw new IOException("Reader filters changed supplied metadata.");
        var openModel = new ArtifactReaderViewModel(bundle) { Evidence = "candidate finding" }; ArtifactEntry? requested = null;
        openModel.OpenRequested += entry => requested = entry; openModel.OpenCommand.Execute(null);
        if (requested?.Path != "findings/candidate-risk.md" || requested.EvidenceLevel != "candidate finding") throw new IOException("Reader opening changed the selected path or evidence label.");

        var homePath = Path.Combine(imported.Root, "README.md"); var saved = File.ReadAllText(homePath);
        var drafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [homePath] = "---\ntitle: Reviewer home\norder: 99\nnavGroup: Local review\n---\n" + saved };
        var site = new DocsSiteExportService().ExportAsync(imported.Root, drafts, Path.Combine(root, "state"), new("Artefact review site"), artifactBundle: bundle).GetAwaiter().GetResult();
        var siteZip = Path.Combine(output, "native-artefact-site.zip"); File.WriteAllBytes(siteZip, site.Bytes);
        var extracted = Path.Combine(output, "artefact-site"); Directory.CreateDirectory(extracted); ZipFile.ExtractToDirectory(siteZip, extracted, true);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(extracted, "assets", "search-index.json")));
        var home = index.RootElement.GetProperty("pages").EnumerateArray().Single(page => page.GetProperty("path").GetString() == "README.md");
        if (home.GetProperty("title").GetString() != "Reviewer home" || home.GetProperty("navGroup").GetString() != "Local review"
            || !index.RootElement.GetProperty("artifactBundle").GetProperty("candidateFindings").GetBoolean()
            || File.ReadAllText(homePath) != saved || File.Exists(Path.Combine(imported.Root, ArtifactBundleManifest.Filename)))
            throw new IOException("Native artefact export lost front-matter precedence, candidate wording or source/metadata isolation.");
        File.WriteAllText(Path.Combine(output, "native-artefacts.json"), JsonSerializer.Serialize(new { Passed = true, Checks = new[] {
            "Native grouped reader filters supplied evidence, paths and titles without changing labels",
            "Empty filters disable opening and native controls remain accessible at both sizes in light/dark themes",
            "Selection opens only the supplied imported document path",
            "Actual Docs Site export retains current source, front-matter priority and candidate notes without persisting raw metadata" }, IndexPath = Path.Combine(extracted, "index.html") }));
    }
    internal static void CreateFixtureArchive(string archivePath, string repository)
    {
        var fixture = Path.Combine(repository, "tests", "fixtures", "artifact-bundles", "valid-rich");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var path in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixture, path).Replace('\\', '/');
            using var target = archive.CreateEntry(relative).Open();
            if (relative == "assets/tiny.png")
            {
                // The source fixture is a textual placeholder; native rendering needs actual PNG pixels.
                var pixels = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
                    new byte[] { 0x50, 0x88, 0xff, 0xff, 0x66, 0x66, 0x66, 0xff, 0x88, 0xaa, 0xcc, 0xff, 0x50, 0x88, 0xff, 0xff }, 8);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels));
                using var encoded = new MemoryStream(); encoder.Save(encoded); encoded.Position = 0; encoded.CopyTo(target);
            }
            else { using var source = File.OpenRead(path); source.CopyTo(target); }
        }
    }
}
