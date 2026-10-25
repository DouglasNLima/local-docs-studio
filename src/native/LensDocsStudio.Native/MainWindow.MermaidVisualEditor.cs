using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Core.StateDiagrams;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.Mindmaps;
using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.QuadrantDiagrams;
using LensDocsStudio.Core.TimelineDiagrams;
using LensDocsStudio.Core.JourneyDiagrams;
using LensDocsStudio.Core.XyCharts;
using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.KanbanDiagrams;
using LensDocsStudio.Core.RequirementDiagrams;
using LensDocsStudio.Core.GitGraphDiagrams;
using LensDocsStudio.Core.ArchitectureDiagrams;
using LensDocsStudio.Core.C4Diagrams;
using LensDocsStudio.Core.BlockDiagrams;
using LensDocsStudio.Core.PacketDiagrams;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Core.VennDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void OpenMermaidVisualEditor(DocumentViewModel tab, ParsedDocument parsedDocument, ITextSourceVersion version, int offset)
    {
        if (model.ActiveDocument != tab || Editor.Document != tab.EditorDocument || Editor.IsReadOnly || tab.IsSaving
            || tab.EditorDocument.Text != parsedDocument.Source || version.CompareAge(tab.EditorDocument.Version) != 0)
        { model.Status = "The document changed. Wait for its current preview before editing visually."; return; }
        if (parsedDocument.Source.Length > MermaidDocumentEditSession.MaximumDocumentCharacters)
        { model.Status = "Visual authoring supports documents up to 4 million characters. Use Edit source for this document."; return; }
        var standalone = IsStandaloneMermaid(tab);
        if (standalone != parsedDocument.StandaloneMermaid) { model.Status = "The file format changed. Wait for the current preview before editing visually."; return; }
        var target = MermaidBlockSource.Find(parsedDocument, standalone).SingleOrDefault(block => offset >= block.Container.Start && offset < block.Container.End);
        if (target is null) { model.Status = "This Mermaid container cannot be mapped safely. Use Edit source."; return; }
        var session = new MermaidDocumentEditSession(tab, target);
        OpenVisualDraft(tab, session, target.Source, () =>
        {
            try { EditDiagramSource(tab, tab.EditorDocument.Text, session.SourceOffset()); }
            catch (ArgumentException error) { model.Status = error.Message; }
        });
    }
    private void InsertFlowchart(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Flowchart);
    private void InsertSequence(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Sequence);
    private void InsertState(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.State);
    private void InsertEr(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Er);
    private void InsertClass(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Class);
    private void InsertPie(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Pie);
    private void InsertMindmap(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Mindmap);
    private void InsertGantt(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Gantt);
    private void InsertQuadrant(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Quadrant);
    private void InsertTimeline(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Timeline);
    private void InsertJourney(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Journey);
    private void InsertXy(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Xy);
    private void InsertRadar(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Radar);
    private void InsertSankey(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Sankey);
    private void InsertKanban(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Kanban);
    private void InsertRequirement(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Requirement);
    private void InsertGitGraph(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.GitGraph);
    private void InsertArchitecture(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Architecture);
    private void InsertC4(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.C4);
    private void InsertBlock(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Block);
    private void InsertPacket(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Packet);
    private void InsertTreeView(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.TreeView);
    private void InsertTreemap(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Treemap);
    private void InsertVenn(object sender, RoutedEventArgs e) => InsertMermaidFamily(MermaidDiagramFamily.Venn);
    private static bool IsStandaloneMermaid(DocumentViewModel tab) => Path.GetExtension(tab.EffectivePath ?? tab.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
    private void OpenVisualDraft(DocumentViewModel tab, MermaidDocumentEditSession session, string source, Action? editSource, Func<string?>? canApply = null, Action? onClosed = null)
    {
        if (ExpansionViSource.IsFamily(source))
        {
            if (!PacketVisualDraft.IsPacket(source) && !TreeViewVisualDraft.IsTreeView(source) && !TreemapVisualDraft.IsTreemap(source) && !VennVisualDraft.IsVenn(source)) { model.Status = "This native header variant remains protected source-only. Use Edit source."; editSource?.Invoke(); return; }
            if (source.Length > 100_000 || source.Count(character => character == '\n') + (source.Length > 0 && !source.EndsWith('\n') ? 1 : 0) > 1500)
            { model.Status = "This diagram exceeds the 100,000-character or 1,500-line visual source budget. Use Edit source; the document is unchanged."; return; }
        }
        if (TopologySource.IsTopology(source))
        {
            if (!ArchitectureVisualDraft.IsArchitecture(source) && !C4VisualDraft.IsC4(source) && !BlockVisualDraft.IsBlock(source))
            { model.Status = "This native topology variant remains protected source-only. C4 authoring covers Context, Container and Component."; editSource?.Invoke(); return; }
            if (source.Length > 100_000 || source.Count(character => character == '\n') + (source.Length > 0 && !source.EndsWith('\n') ? 1 : 0) > 1500)
            { model.Status = "This diagram exceeds the 100,000-character or 1,500-line visual source budget. Use Edit source; the document is unchanged."; return; }
        }
        if ((KanbanVisualDraft.IsKanban(source) || RequirementVisualDraft.IsRequirement(source) || GitGraphVisualDraft.IsGitGraph(source) || XyVisualDraft.IsXy(source) || RadarVisualDraft.IsRadar(source) || SankeyVisualDraft.IsSankey(source) || GanttVisualDraft.IsGantt(source) || MindmapVisualDraft.IsMindmap(source) || PieVisualDraft.IsPie(source) || QuadrantVisualDraft.IsQuadrant(source) || TimelineVisualDraft.IsTimeline(source) || JourneyVisualDraft.IsJourney(source))
            && (source.Length > 100_000 || source.Count(character => character == '\n') + (source.Length > 0 && !source.EndsWith('\n') ? 1 : 0) > 1500))
        { model.Status = "This diagram exceeds the 100,000-character or 1,500-line visual source budget. Use Edit source; the document is unchanged."; return; }
        async Task<string?> ApplyDraft(string draft, CancellationToken cancellationToken)
        {
            try
            {
                if (canApply?.Invoke() is { } problem) return problem;
                if (model.ActiveDocument == tab && Editor.IsReadOnly) return "The owning document is read-only.";
                var edit = await session.PrepareAsync(draft, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (canApply?.Invoke() is { } changed) return changed;
                session.Commit(edit, model.Documents.Contains(tab));
                model.Status = edit.Expected == edit.Replacement ? "The Mermaid source is unchanged; no document Undo step was needed." : "Mermaid source applied as one undoable change. Save the document to persist it.";
                if (model.ActiveDocument == tab) QueuePreview();
                return null;
            }
            catch (ArgumentException error) { return error.Message; }
        }
        var definition = MermaidDiagramCatalogue.Recognise(source);
        if (definition is null) { model.Status = "This diagram family or header variant remains source-only. Use Edit source."; editSource?.Invoke(); return; }
        Window window;
        try { window = definition.Family switch
        {
            MermaidDiagramFamily.Flowchart => new MermaidVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, ExportDiagram, sourceFont: Editor.FontFamily, sourceFontSize: Editor.FontSize),
            MermaidDiagramFamily.Sequence => new SequenceVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.State => new StateVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Er => new ErVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Class => new ClassVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Pie => new PieVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Mindmap => new MindmapVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Gantt => new GanttVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Quadrant => new QuadrantVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Timeline => new TimelineVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Journey => new JourneyVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Xy => new XyVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Radar => new RadarVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Sankey => new SankeyVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Kanban => new KanbanVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Requirement => new RequirementVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.GitGraph => new GitGraphVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Architecture => new ArchitectureVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.C4 => new C4VisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Block => new BlockVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Packet => new PacketVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.TreeView => new TreeViewVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Treemap => new TreemapVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            MermaidDiagramFamily.Venn => new VennVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize),
            _ => throw new ArgumentException("This visual diagram family is unavailable; use Edit source.")
        }; }
        catch (Exception error) when (error is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { model.Status = "Visual authoring could not safely inspect this source. The document is unchanged; use Edit source. " + error.Message; return; }
        window.Owner = this;
        if (onClosed is not null) window.Closed += (_, _) => onClosed();
        window.Show();
    }
}
