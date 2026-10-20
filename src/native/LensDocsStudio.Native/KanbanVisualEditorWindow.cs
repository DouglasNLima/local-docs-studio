using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.KanbanDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class KanbanVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly KanbanVisualDraft kanban;
    private readonly TextBox assigned, ticket, priority;
    private readonly ComboBox destination;
    protected override string Values => base.Values + "\u001f" + assigned?.Text + "\u001f" + ticket?.Text + "\u001f" + priority?.Text;
    public KanbanVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Kanban", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        kanban = new(source, diagrams); kanban.ValidatePresentation = (t, _, token) => ValidateNative(t, token);
        assigned = Field("Assigned", "Assigned"); ticket = Field("Ticket", "Ticket"); priority = Field("Priority (Very high, High, Medium, Low, Very low)", "Priority");
        destination = new ComboBox { Style = (Style)FindResource("Form.ComboBox"), DisplayMemberPath = "Description" }; Identify(destination, "Destination", "Destination column");
        InspectorFields.Children.Add(new Label { Content = "Destination column", Target = destination }); InspectorFields.Children.Add(destination);
        ActionButton("Move card to selected column", "MoveCard", () => kanban.MoveCardAsync(kanban.Selection ?? "", (destination.SelectedItem as Choice)?.Identity ?? "", Lifetime.Token), InspectorFields);
        ActionButton("Remove column and all cards", "RemoveCompleteColumn", async () =>
        {
            if (MessageBox.Show(this, "Remove this column and every card in its complete source group? Private Undo can restore them.", "Remove complete column", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return Protected("Complete column removal cancelled.");
            return await kanban.DeleteAsync(kanban.Selection ?? "", true, Lifetime.Token);
        }, InspectorFields);
        ActionButton("Add column", "AddColumn", async () => { var n = Next(); var r = await kanban.AddColumnAsync("column" + n, "New column " + n, Lifetime.Token); if (r.Success) kanban.Select(kanban.Model.Columns[^1].Identity); return r; });
        ActionButton("Add card", "AddCard", async () =>
        { var column = Row?.Column ?? Row?.Identity ?? kanban.Model.Columns.LastOrDefault()?.Identity; if (column is null) return Protected("Add or select a column first."); var n = Next(); var r = await kanban.AddCardAsync(column, "card" + n, "New card " + n, Lifetime.Token); if (r.Success) kanban.Select(kanban.Model.Cards.Last(c => c.Column == column).Identity); return r; });
        Start(kanban);
    }
    private int Next() { var n = 1; while (kanban.Model.Rows.Any(r => r.Id == "column" + n || r.Id == "card" + n)) n++; return n; }
    private KanbanRow? Row => kanban.Model.Rows.FirstOrDefault(r => r.Identity == kanban.Selection);
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(kanban.Model.HeaderIdentity, "Board title · " + kanban.Model.Title?.Value);
        foreach (var row in kanban.Model.Rows) yield return new(row.Identity, (row.Column is null ? "Column · " : "Card · ") + row.Label + " · ID: " + row.Id);
        foreach (var key in kanban.Model.Protected) yield return new(key, "Protected source · " + kanban.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var row = Row; var header = kanban.Selection == kanban.Model.HeaderIdentity;
        Captions("Title", "Source ID (coupled to title for bare forms)"); Properties(row?.Label ?? (header ? kanban.Model.Title?.Value ?? "" : ""), row?.Id ?? "", editLabel: row is not null || header, editIdentifier: row is not null);
        assigned.Text = row?.Meta("assigned") ?? ""; ticket.Text = row?.Meta("ticket") ?? ""; priority.Text = row?.Meta("priority") ?? "";
        assigned.IsEnabled = ticket.IsEnabled = priority.IsEnabled = row?.Column is not null && row.MetadataSafe;
        destination.ItemsSource = kanban.Model.Columns.Select(c => new Choice(c.Identity, c.Label + " · ID: " + c.Id)).ToArray(); destination.SelectedIndex = 0; destination.IsEnabled = row?.Column is not null;
        Describe(row is null ? "Kanban · effective title" : (row.Column is null ? "Column" : "Card") + " · " + row.Form + " source form", "Actual canonical board. Bare/bracket forms couple ID and title. Metadata is case-insensitive and last-wins; unknown keys stay exact. Cards are a flat list, including deeper indentation. Mixed column indentation or ambiguous comments protect structural changes. Remove connected cards first or explicitly remove the complete column. Use the occurrence list for duplicate IDs.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Row is { } row ? kanban.UpdateAsync(row.Identity, Identifier.Text, Label.Text, assigned.Text, ticket.Text, priority.Text, Lifetime.Token) : kanban.SetTitleAsync(Label.Text, Lifetime.Token);
    protected override Task<SourceAuthoringResult> DeleteSelection() => kanban.DeleteAsync(kanban.Selection ?? "", token: Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => kanban.MoveAsync(kanban.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    { if (source != kanban.Source) return []; var root = XDocument.Parse(render.Svg!).Root!; return KanbanVisualDraft.Correspondence(kanban.Model, render.Svg!).Select(p => Hit(p.Row.Identity, (p.Row.Column is null ? "Column · " : "Card · ") + p.Row.Label, p.Shape, root)).OfType<NativeSourceHit>().ToArray(); }
}
