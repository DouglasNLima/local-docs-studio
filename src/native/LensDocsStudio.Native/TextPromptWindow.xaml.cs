using System.Windows;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class TextPromptWindow : Window
{
    public string Value => Input.Text.Trim();
    public TextPromptWindow(string title, string prompt, string initialValue, string action,
        string inputLabel = "Workspace relative path", string? hint = "Use a path relative to the open workspace, such as docs/guide.md.")
    {
        InitializeComponent(); Title = title; Prompt.Text = prompt; Input.Text = initialValue; AcceptButton.Content = action;
        System.Windows.Automation.AutomationProperties.SetName(Input, inputLabel);
        Hint.Text = hint ?? ""; Hint.Visibility = string.IsNullOrEmpty(hint) ? Visibility.Collapsed : Visibility.Visible;
        WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { Input.Focus(); Input.SelectAll(); };
    }
    private void Accept(object sender, RoutedEventArgs e) { if (Value.Length != 0) DialogResult = true; }
}
