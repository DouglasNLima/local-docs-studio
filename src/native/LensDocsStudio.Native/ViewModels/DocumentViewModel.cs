using CommunityToolkit.Mvvm.ComponentModel;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.ViewModels;

public partial class DocumentViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public TextDocument EditorDocument { get; }
    public DocumentSnapshot? SavedSnapshot { get; private set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title))] private string? filePath;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title))] private bool isDirty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title))] private bool externalChange;
    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public string Title => (Path.GetFileName(FilePath) ?? "Untitled.md") + (IsDirty ? " •" : "") + (ExternalChange ? " ⚠" : "");
    public string? WorkspaceRoot { get; set; }
    public event EventHandler? Edited;

    public DocumentViewModel(string text, DocumentSnapshot? snapshot = null)
    {
        SavedSnapshot = snapshot;
        FilePath = snapshot?.Path;
        EditorDocument = new TextDocument(text);
        IsDirty = snapshot is null ? text.Length != 0 : text != snapshot.Text;
        EditorDocument.TextChanged += (_, _) => { IsDirty = SavedSnapshot is null || EditorDocument.Text != SavedSnapshot.Text; Edited?.Invoke(this, EventArgs.Empty); };
    }

    public void MarkSaved(DocumentSnapshot snapshot)
    {
        SavedSnapshot = snapshot;
        FilePath = snapshot.Path;
        IsDirty = EditorDocument.Text != snapshot.Text;
        ExternalChange = false;
    }

    public RecoveredDocument Recovery() => new(Id, FilePath, EditorDocument.Text, SavedSnapshot?.Fingerprint,
        SavedSnapshot?.Utf8Bom ?? false, CaretOffset, VerticalOffset, SavedSnapshot?.Text);
}
