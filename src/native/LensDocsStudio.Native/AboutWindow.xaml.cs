using System.Windows;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class AboutWindow : Window
{
    public AboutWindow(string version, string revision)
    {
        InitializeComponent(); Identity.Text = $"Version: {version}\nSource: {revision}";
        WindowStateService.FitInitialWindow(this);
    }
}
