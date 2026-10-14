using System.Windows;
using System.Reflection;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void About(object sender, RoutedEventArgs e)
    {
        var identity = (typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Not recorded").Split('+', 2);
        var source = identity.ElementAtOrDefault(1) ?? "Not recorded";
        new AboutWindow(identity[0], source) { Owner = this }.ShowDialog();
    }
    private HelpWindow? helpWindow;
    private void OpenHelp(object sender, RoutedEventArgs e)
    {
        if (helpWindow is not null) { helpWindow.Activate(); return; }
        helpWindow = new(model.Theme) { Owner = this };
        helpWindow.Closed += (_, _) => helpWindow = null;
        helpWindow.Show();
    }
}
