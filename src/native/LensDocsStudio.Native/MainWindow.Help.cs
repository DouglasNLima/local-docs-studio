using System.Windows;
using System.Reflection;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void About(object sender, RoutedEventArgs e)
    {
        var identity = (typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Not recorded").Split('+', 2);
        var source = identity.ElementAtOrDefault(1) ?? "Not recorded";
        MessageBox.Show(this, $"Lens Docs Studio — Native Preview\nLocal Markdown, Mermaid, and documentation studio.\n\nVersion: {identity[0]}\nSource: {source}\n\nInternal preview for controlled evaluation.\nSee PREVIEW-GUIDE.md for restrictions and Help → Updates for update availability and trust.", "About Lens Docs Studio");
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
