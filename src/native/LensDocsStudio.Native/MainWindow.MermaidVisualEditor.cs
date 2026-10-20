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
    private void InsertFlowchart(object sender, RoutedEventArgs e)
        => InsertMermaidVisually(false);
    private void InsertSequence(object sender, RoutedEventArgs e)
        => InsertMermaidVisually(true);
    private void InsertState(object sender, RoutedEventArgs e)
        => InsertMermaidVisually(false, true);
    private void InsertEr(object sender, RoutedEventArgs e)
        => InsertMermaidVisually(false, er: true);
    private void InsertClass(object sender, RoutedEventArgs e)
        => InsertMermaidVisually(false, classDiagram: true);
    private void InsertPie(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, pie: true);
    private void InsertMindmap(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, mindmap: true);
    private void InsertGantt(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, gantt: true);
    private void InsertQuadrant(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, quadrant: true);
    private void InsertTimeline(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, timeline: true);
    private void InsertJourney(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, journey: true);
    private void InsertXy(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, xy: true);
    private void InsertRadar(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, radar: true);
    private void InsertSankey(object sender, RoutedEventArgs e) => InsertMermaidVisually(false, sankey: true);
    private void InsertMermaidVisually(bool sequence, bool state = false, bool er = false, bool classDiagram = false, bool pie = false, bool mindmap = false, bool gantt = false, bool quadrant = false, bool timeline = false, bool journey = false, bool xy = false, bool radar = false, bool sankey = false)
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        if (tab.EditorDocument.TextLength > MermaidDocumentEditSession.MaximumDocumentCharacters)
        { model.Status = "Visual authoring supports documents up to 4 million characters. Insert Mermaid through the source editor for this document."; return; }
        if (IsStandaloneMermaid(tab) && !string.IsNullOrWhiteSpace(tab.EditorDocument.Text))
        {
            OpenMermaidVisualEditor(tab, markdown.Parse(tab.EditorDocument.Text, true), tab.EditorDocument.Version, 0); return;
        }
        var source = tab.EditorDocument.Text; var standalone = IsStandaloneMermaid(tab);
        var start = standalone ? 0 : Editor.SelectionStart; var length = standalone ? source.Length : Editor.SelectionLength;
        var target = new MermaidBlockSource(new(start, length), new(start, length), source.Substring(start, length), standalone) { IsInsertion = true };
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var session = new MermaidDocumentEditSession(tab, target, standalone ? null : (draft, current, span) =>
        {
            var prefix = span.Start == 0 || current[..span.Start].EndsWith(newline + newline, StringComparison.Ordinal) ? "" : current[..span.Start].EndsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
            var suffix = span.End == current.Length || current[span.End..].StartsWith(newline + newline, StringComparison.Ordinal) ? "" : current[span.End..].StartsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
            return prefix + "```mermaid" + newline + draft.TrimEnd('\r', '\n') + newline + "```" + suffix;
        });
        OpenVisualDraft(tab, session, xy ? XyVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : radar ? RadarVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : sankey ? SankeyVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : journey ? JourneyVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : timeline ? TimelineVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : quadrant ? QuadrantVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : gantt ? GanttVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : mindmap ? MindmapVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : pie ? PieVisualDraft.Starter.Replace("\n", newline, StringComparison.Ordinal) : classDiagram ? ClassDiagramSourceParser.Starter.Replace("\n", newline, StringComparison.Ordinal) : er ? string.Join(newline, "erDiagram", "    CUSTOMER {", "        string customer_id PK", "        string email UK", "    }", "    ORDER {", "        string order_id PK", "        string customer_id FK", "    }", "    CUSTOMER ||--o{ ORDER : places", "") : state ? string.Join(newline, "stateDiagram-v2", "    state \"Draft\" as Draft", "    state \"Review\" as Review", "    [*] --> Draft", "    Draft --> Review : Submit", "    Review --> Draft : Request changes", "    Review --> [*]", "") : sequence ? string.Join(newline, "sequenceDiagram", "    participant Author", "    participant Studio", "    Author->>Studio: Open documentation", "    Studio-->>Author: Display preview", "") : "flowchart TD" + newline, null);
    }
    private static bool IsStandaloneMermaid(DocumentViewModel tab) => Path.GetExtension(tab.EffectivePath ?? tab.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
    private void OpenVisualDraft(DocumentViewModel tab, MermaidDocumentEditSession session, string source, Action? editSource)
    {
        if ((XyVisualDraft.IsXy(source) || RadarVisualDraft.IsRadar(source) || SankeyVisualDraft.IsSankey(source) || GanttVisualDraft.IsGantt(source) || MindmapVisualDraft.IsMindmap(source) || PieVisualDraft.IsPie(source) || QuadrantVisualDraft.IsQuadrant(source) || TimelineVisualDraft.IsTimeline(source) || JourneyVisualDraft.IsJourney(source))
            && (source.Length > 100_000 || source.Count(character => character == '\n') + (source.Length > 0 && !source.EndsWith('\n') ? 1 : 0) > 1500))
        { model.Status = "This diagram exceeds the 100,000-character or 1,500-line visual source budget. Use Edit source; the document is unchanged."; return; }
        async Task<string?> ApplyDraft(string draft, CancellationToken cancellationToken)
        {
            try
            {
                if (model.ActiveDocument == tab && Editor.IsReadOnly) return "The owning document is read-only.";
                var edit = await session.PrepareAsync(draft, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested(); session.Commit(edit, model.Documents.Contains(tab));
                model.Status = edit.Expected == edit.Replacement ? "The Mermaid source is unchanged; no document Undo step was needed." : "Mermaid source applied as one undoable change. Save the document to persist it.";
                if (model.ActiveDocument == tab) QueuePreview();
                return null;
            }
            catch (ArgumentException error) { return error.Message; }
        }
        Window window;
        try { window = XyVisualDraft.IsXy(source)
            ? new XyVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : RadarVisualDraft.IsRadar(source)
            ? new RadarVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : SankeyVisualDraft.IsSankey(source)
            ? new SankeyVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : JourneyVisualDraft.IsJourney(source)
            ? new JourneyVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : TimelineVisualDraft.IsTimeline(source)
            ? new TimelineVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : QuadrantVisualDraft.IsQuadrant(source)
            ? new QuadrantVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : GanttVisualDraft.IsGantt(source)
            ? new GanttVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : MindmapVisualDraft.IsMindmap(source)
            ? new MindmapVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : PieVisualDraft.IsPie(source)
            ? new PieVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : ClassDiagramSourceParser.IsClass(source)
            ? new ClassVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : ErDiagramSourceParser.IsEr(source)
            ? new ErVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : StateDiagramSourceParser.IsState(source)
            ? new StateVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : SequenceSourceParser.IsSequence(source)
            ? new SequenceVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, Editor.FontFamily, Editor.FontSize)
            : new MermaidVisualEditorWindow(source, diagrams, ApplyDraft, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, ExportDiagram, sourceFont: Editor.FontFamily, sourceFontSize: Editor.FontSize); }
        catch (Exception error) when (error is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { model.Status = "Visual authoring could not safely inspect this source. The document is unchanged; use Edit source. " + error.Message; return; }
        window.Owner = this;
        window.Show();
    }
}
