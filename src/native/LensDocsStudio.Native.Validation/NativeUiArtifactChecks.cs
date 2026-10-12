using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Imports;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static (string Root, string Document, string Source) CheckArtifactWorkflow(Window owner, AutomationElement editor,
        UIA3Automation automation, int process, string runRoot, string output, Action<string, string> open, Action<string, string> save)
    {
        var input = Path.Combine(runRoot, "artefact-input.zip"); var parent = Path.Combine(runRoot, "artefact-imports");
        Directory.CreateDirectory(parent); NativeArtifactChecks.CreateFixtureArchive(input, Directory.GetCurrentDirectory());
        var originalArchive = SHA256.HashData(File.ReadAllBytes(input));
        Find(owner, "FileMenu").Click(); Find(owner, "ImportMarkdownBundle").Patterns.Invoke.Pattern.Invoke();
        open(input, "Import Markdown bundle");
        const string folderTitle = "Open documentation workspace"; Window? picker = null;
        Wait(() => (picker = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName(folderTitle)))?.AsWindow()) is not null,
            "The bundle destination folder dialogue did not open.");
        FocusWindow(picker!);
        try
        {
            var folder = (picker!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName("Folder:")))
                ?? throw new IOException("The labelled destination folder field is unavailable.")).AsTextBox();
            folder.Focus(); Wait(() => folder.Properties.HasKeyboardFocus.Value, "The destination folder field did not acquire keyboard focus.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(parent);
            Wait(() => folder.Text == parent, "The destination folder field did not accept the selected path.");
            var choose = picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Select Folder")))
                .Single(element => element.AutomationId != "DropDown").AsButton();
            choose.Invoke();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName(folderTitle))) is null,
                "The bundle destination folder dialogue did not close.");
        }
        catch
        {
            try
            {
                picker!.CaptureToFile(Path.Combine(output, "native-artefact-folder-failure.png"));
                File.WriteAllLines(Path.Combine(output, "native-artefact-folder-controls.txt"),
                    picker.FindAllDescendants().Select(element => $"{element.ControlType}: {element.AutomationId} — {element.Name}"));
            }
            catch (Exception) { /* Preserve the original workflow failure. */ }
            throw;
        }
        Wait(() => Find(owner, "Status").Name.StartsWith("Imported ", StringComparison.Ordinal)
            && Find(owner, "Status").Name.Contains("Artefact bundle", StringComparison.Ordinal), "The artefact bundle did not import.");
        var root = Directory.GetDirectories(parent, "artefact-input-import-*").Single();
        var home = File.ReadAllText(Path.Combine(root, "README.md"));
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == home, "The imported entry document did not open.");
        if (File.Exists(Path.Combine(root, ArtifactBundleManifest.Filename))) throw new IOException("ZIP metadata was persisted as workspace content.");
        Find(owner, "ViewMenu").Click(); Find(owner, "OpenArtifactReader").Patterns.Invoke.Pattern.Invoke();
        Window? reader = null;
        Wait(() => (reader = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Artefact bundle")))?.AsWindow()) is not null,
            "The native artefact reader did not open.");
        if (!Find(reader!, "ArtifactEvidenceNotes").Name.Contains("Candidate findings remain candidate findings.", StringComparison.Ordinal)
            || !Find(reader!, "ArtifactWarnings").Name.Contains("MANUAL_REVIEW_REQUIRED", StringComparison.Ordinal))
            throw new IOException("The native reader lost supplied evidence notes or producer warnings.");
        var evidence = Find(reader!, "ArtifactEvidence").AsComboBox(); evidence.Select("candidate finding"); evidence.Collapse();
        Wait(() => Find(reader!, "ArtifactStatus").Name.StartsWith("1 of ", StringComparison.Ordinal)
            && Find(reader!, "ArtifactItems").FindFirstDescendant(cf => cf.ByName("Candidate Risk").And(cf.ByControlType(ControlType.ListItem))) is not null,
            "The native reader did not filter its grouped candidate item.");
        reader!.CaptureToFile(Path.Combine(output, "native-artefact-reader.png"));
        var group = Find(reader, "ArtifactGroup").AsComboBox(); group.Select("Diagrams"); group.Collapse();
        Wait(() => !Find(reader, "OpenArtifactDocument").IsEnabled && Find(reader, "ArtifactStatus").Name.StartsWith("No matching items.", StringComparison.Ordinal),
            "An empty artefact filter retained an openable item.");
        Find(reader, "ArtifactReset").AsButton().Invoke();
        Find(reader, "ArtifactQuery").AsTextBox().Text = "candidate-risk";
        Wait(() => Find(reader, "OpenArtifactDocument").IsEnabled && Find(reader, "ArtifactStatus").Name.StartsWith("1 of ", StringComparison.Ordinal),
            "Artefact path search did not select the candidate document.");
        Find(reader, "OpenArtifactDocument").AsButton().Invoke();
        var document = Path.Combine(root, "findings", "candidate-risk.md"); var source = File.ReadAllText(document);
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == source, "The selected artefact document did not open.");
        var previous = NativeUiClipboard.GetDataObject(); uint owned = 0;
        const string draft = "\n\nReviewer draft remains a candidate.\n";
        try
        {
            NativeUiClipboard.SetText(draft); owned = GetClipboardSequenceNumber();
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == source + draft, "The unsaved artefact review edit did not reach the editor.");
            var review = Path.Combine(runRoot, "actual-artefact-review.zip");
            Find(owner, "ExportMenu").Click(); Find(owner, "ExportArtifactReviewPack").Patterns.Invoke.Pattern.Invoke();
            save(review, "Export artefact review pack");
            Wait(() => File.Exists(review) && Find(owner, "Status").Name.StartsWith("Artefact review pack exported", StringComparison.Ordinal),
                "The artefact review pack did not finish exporting.");
            using (var zip = ZipFile.OpenRead(review))
            {
                using var text = new StreamReader(zip.GetEntry("findings/candidate-risk.md")!.Open());
                if (text.ReadToEnd() != source + draft) throw new IOException("Review pack export lost unsaved source.");
                using var manifest = JsonDocument.Parse(zip.GetEntry(ArtifactBundleManifest.Filename)!.Open());
                var candidate = manifest.RootElement.GetProperty("documents").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "findings/candidate-risk.md");
                var confirmed = manifest.RootElement.GetProperty("documents").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "findings/confirmed-control.md");
                if (candidate.GetProperty("evidenceLevel").GetString() != "candidate finding" || confirmed.GetProperty("evidenceLevel").GetString() != "confirmed finding")
                    throw new IOException("Review pack export changed supplied evidence labels.");
            }
            var generic = Path.Combine(runRoot, "actual-generic-review.zip");
            Find(owner, "ExportMenu").Click(); Find(owner, "ExportMarkdownBundle").Patterns.Invoke.Pattern.Invoke();
            save(generic, "Export Markdown bundle");
            Wait(() => File.Exists(generic) && Find(owner, "Status").Name.StartsWith("Markdown bundle exported", StringComparison.Ordinal),
                "Generic Markdown bundle export did not finish.");
            using (var zip = ZipFile.OpenRead(generic))
            {
                if (zip.GetEntry(ArtifactBundleManifest.Filename) is not null) throw new IOException("Generic Markdown export leaked optional metadata.");
                using var text = new StreamReader(zip.GetEntry("findings/candidate-risk.md")!.Open());
                if (text.ReadToEnd() != source + draft) throw new IOException("Generic Markdown export lost unsaved source.");
            }
            if (File.ReadAllText(document) != source || !originalArchive.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))))
                throw new IOException("Artefact review or export changed the saved source or original archive.");
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == source, "One Undo did not restore the complete artefact review insertion.");
        }
        finally { RestoreClipboard(previous, owned); }
        return (root, document, source);
    }

    private static void CheckArtifactContextCleared(Window owner, string root, string document, string source)
    {
        Find(owner, "ViewMenu").Click();
        if (Find(owner, "OpenArtifactReader").IsEnabled) throw new IOException("Restart unexpectedly restored optional artefact context.");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Find(owner, "ExportMenu").Click();
        if (Find(owner, "ExportArtifactReviewPack").IsEnabled) throw new IOException("Restart enabled review-pack export without an imported manifest.");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        if (File.Exists(Path.Combine(root, ArtifactBundleManifest.Filename)) || File.ReadAllText(document) != source)
            throw new IOException("Session restoration persisted artefact metadata or changed imported source.");
    }
}
