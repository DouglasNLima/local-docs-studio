using Microsoft.Win32;
using System.Windows;

namespace LensDocsStudio.Native.Platform;

public sealed class DesktopDialogs
{
    public Window? Owner { get; set; }
    public string? OpenDocument()
    {
        var dialog = new OpenFileDialog { Filter = "Markdown and text|*.md;*.markdown;*.mmd;*.mermaid;*.txt|All files|*.*" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
    public string? OpenFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Open documentation workspace" };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }
    public string? SaveDocument(string name)
    {
        var dialog = new SaveFileDialog { Title = "Save document", FileName = name, Filter = "Markdown|*.md|Mermaid|*.mmd|Text|*.txt", AddExtension = true, DefaultExt = ".md" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
    public string? OpenImage()
    {
        var dialog = new OpenFileDialog { Title = "Insert local image", Filter = "Raster images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
    public MessageBoxResult SaveChanges(string name) => MessageBox.Show(Owner!, $"Save changes to {name} before closing?",
        "Unsaved document", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
    public bool Confirm(string message, string title) => MessageBox.Show(Owner!, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
    public void Error(string message) => MessageBox.Show(Owner!, message, "Lens Docs Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
}
