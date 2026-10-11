using System.Windows;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class DocsSiteWindow : Window
{
    public DocsSiteOptions Options => new(SiteTitle.Text.Trim(), SiteDescription.Text.Trim(), SiteTheme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" });
    public DocsSiteWindow(string title, LensDocsStudio.Core.State.LocalDocsSiteProfile? defaults = null)
    {
        InitializeComponent(); SiteTitle.Text = defaults?.Title ?? title; SiteDescription.Text = defaults?.Description ?? "";
        SiteTheme.SelectedIndex = defaults?.Theme switch { "light" => 1, "dark" => 2, _ => 0 }; WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { SiteTitle.Focus(); SiteTitle.SelectAll(); };
    }
    private void Accept(object sender, RoutedEventArgs e)
    {
        if (Options.Title.Length == 0) { ValidationMessage.Text = "Enter a title for this Docs Site."; SiteTitle.Focus(); return; }
        DialogResult = true;
    }
}
