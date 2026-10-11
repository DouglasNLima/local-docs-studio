using System.Windows;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class TextPromptWindow : Window
{
    public string Value => Input.Text.Trim();
    public TextPromptWindow(string title, string prompt, string initialValue, string action)
    {
        InitializeComponent(); Title = title; Prompt.Text = prompt; Input.Text = initialValue; AcceptButton.Content = action;
        WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { Input.Focus(); Input.SelectAll(); };
    }
    private void Accept(object sender, RoutedEventArgs e) { if (Value.Length != 0) DialogResult = true; }
}
