using System.Windows;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private HelpWindow? helpWindow;
    private void OpenHelp(object sender, RoutedEventArgs e)
    {
        if (helpWindow is not null) { helpWindow.Activate(); return; }
        helpWindow = new(model.Theme) { Owner = this };
        helpWindow.Closed += (_, _) => helpWindow = null;
        helpWindow.Show();
    }
}
