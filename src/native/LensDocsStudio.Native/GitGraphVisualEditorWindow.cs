using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.GitGraphDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class GitGraphVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly GitGraphVisualDraft git;
    private readonly ComboBox branch;
    public GitGraphVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("GitGraph", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        git = new(source, diagrams); git.ValidatePresentation = (t, _, token) => ValidateNative(t, token);
        ActionButton("Append commit", "AddCommit", () => Append("commit"));
        ActionButton("Append new branch", "AddBranch", () => { var n = 1; while (git.Model.Branches.Contains("branch" + n)) n++; return Append("branch", "branch" + n); });
        branch = new ComboBox { Style = (System.Windows.Style)FindResource("Form.ComboBox") }; Identify(branch, "Branch", "Existing branch destination"); InspectorFields.Children.Add(new Label { Content = "Existing branch destination", Target = branch }); InspectorFields.Children.Add(branch);
        ActionButton("Append checkout", "Checkout", () => Append("checkout", branch.SelectedItem as string ?? ""), InspectorFields);
        ActionButton("Append merge", "Merge", () => Append("merge", branch.SelectedItem as string ?? ""), InspectorFields);
        Start(git);
    }
    private async Task<SourceAuthoringResult> Append(string kind, string reference = "")
    { var r = await git.AppendAsync(kind, reference, token: Lifetime.Token); if (r.Success) git.Select(git.Model.Commands[^1].Identity); return r; }
    private GitSourceCommand? Command => git.Model.Commands.FirstOrDefault(c => c.Identity == git.Selection);
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(git.Model.HeaderIdentity, "History · current branch " + git.Model.CurrentBranch);
        foreach (var (c, i) in git.Model.Commands.Select((c, i) => (c, i))) yield return new(c.Identity, "Command " + (i + 1) + " · " + c.Kind + " · " + (c.Mark ? c.Label ?? "unlabelled merge" : c.Reference) + " · branch " + c.BranchBefore + (c.Simple ? "" : " · protected"));
    }
    protected override void ShowProperties()
    {
        var c = Command; var editable = c is { Simple: true, Mark: true } && c.Kind != "cherry-pick";
        Captions("Explicit commit ID (blank keeps anonymous numbering)", "Tag", "Commit type (NORMAL, HIGHLIGHT, REVERSE)");
        Properties(c?.Value("id") ?? "", c?.Value("tag") ?? "", c?.Value("type") ?? "", editLabel: editable, editIdentifier: editable, editValue: editable);
        branch.ItemsSource = git.Model.Branches.ToArray(); branch.SelectedIndex = 0;
        Describe(c is null ? "GitGraph · current branch " + git.Model.CurrentBranch : c.Kind + " · branch before: " + c.BranchBefore + " · after: " + c.BranchAfter + (c.Label is null ? "" : " · native label: " + c.Label),
            "Actual native history; this editor writes Mermaid text. Commands without visible marks remain in the occurrence list. Branch creation changes current branch. Anonymous numbering includes merges; appending/editing never inserts IDs into untouched commands. New merges require distinct non-empty branch heads. Only final uninterrupted simple commits can move or be removed. Orientation, lane order, repeated branches, unknown references and cherry-pick protect stream operations; source is retained exactly. Branch rename and implicit main rename are outside this slice.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => git.UpdateAsync(git.Selection ?? "", Label.Text, Identifier.Text, Value.Text, Lifetime.Token);
    protected override Task<SourceAuthoringResult> DeleteSelection() => git.DeleteAsync(git.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => git.MoveAsync(git.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    { if (source != git.Source) return []; var root = XDocument.Parse(render.Svg!).Root!; return GitGraphVisualDraft.Correspondence(git.Model, render.Svg!).Select(p => Hit(p.Command.Identity, "Commit · " + p.Command.Label, p.Shape, root)).OfType<NativeSourceHit>().ToArray(); }
}
