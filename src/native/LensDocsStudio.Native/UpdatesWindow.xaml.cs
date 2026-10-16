using System.Windows;
using LensDocsStudio.Native.ViewModels;
namespace LensDocsStudio.Native;
public partial class UpdatesWindow : Window
{
    public UpdatesWindow(UpdatesViewModel model) { InitializeComponent(); DataContext = model; }
    private void Postpone(object sender, RoutedEventArgs e) => Close();
}
