using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly EditorSlashCommandService slashCommands;
    private bool SlashCommandsPermitted() => model.ActiveDocument is { IsSaving: false }
        && Path.GetExtension(model.ActiveDocument.EffectivePath)?.ToLowerInvariant() is not (".mmd" or ".mermaid");
    private string? SlashCommandUnavailable(string command) => command == "document-link" && model.ActiveDocument?.FilePath is null
        ? "Save the document before choosing a workspace link" : null;
    private void ExecuteSlashCommand(string command, SlashContext context)
    {
        if (!SlashCommandsPermitted() || Editor.IsReadOnly || SlashCommandUnavailable(command) is not null) return;
        if (command is "document-link" or "image" or "snippet")
        {
            var document = Editor.Document; var version = document.Version;
            Editor.Select(context.Start, context.Length);
            if (command == "document-link") InsertDocumentLink(this, new RoutedEventArgs());
            else if (command == "image") InsertImage(this, new RoutedEventArgs());
            else BrowseContentCatalogue(new Button { Tag = "snippet" }, new RoutedEventArgs());
            // Each existing dialog captures its insertion range before awaiting.
            // Cancellation therefore leaves the original caret, not a new selection.
            if (ReferenceEquals(Editor.Document, document) && document.Version.CompareAge(version) == 0)
                Editor.Select(context.Start + context.Length, 0);
        }
        else if (SlashCommandService.Plan(Editor.Text, Editor.CaretOffset, command) is { } edit) EditorFormattingService.Apply(Editor, edit);
    }
}
