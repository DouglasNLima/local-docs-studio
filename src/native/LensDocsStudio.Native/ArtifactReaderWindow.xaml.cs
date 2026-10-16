using System.Windows;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class ArtifactReaderWindow : Window
{
    public ArtifactEntry? SelectedEntry { get; private set; }
    public ArtifactReaderWindow(ArtifactReaderViewModel model)
    {
        InitializeComponent(); DataContext = model; WindowStateService.FitInitialWindow(this);
        model.OpenRequested += entry => { SelectedEntry = entry; DialogResult = true; };
        Loaded += (_, _) => Query.Focus();
    }
}
