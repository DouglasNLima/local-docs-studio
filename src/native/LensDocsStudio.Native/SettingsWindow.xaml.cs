using System.Windows;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel model)
    {
        InitializeComponent(); DataContext = model; WindowStateService.FitInitialWindow(this);
        var applied = false;
        model.Applied += () => { applied = true; DialogResult = true; };
        Closing += (_, args) => args.Cancel = model.IsSaving && !applied;
        Loaded += (_, _) => Theme.Focus();
    }
}
