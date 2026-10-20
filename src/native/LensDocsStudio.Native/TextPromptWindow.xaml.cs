using System.Windows;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class TextPromptWindow : Window
{
    private readonly Func<string, string?>? validation;
    private readonly bool trimInput;
    public string Value => trimInput ? Input.Text.Trim() : Input.Text;
    public TextPromptWindow(string title, string prompt, string initialValue, string action,
        string inputLabel = "Workspace relative path", string? hint = "Use a path relative to the open workspace, such as docs/guide.md.",
        Func<string, string?>? validation = null, bool trimInput = true)
    {
        InitializeComponent(); Title = title; Prompt.Text = prompt; Input.Text = initialValue; AcceptButton.Content = action; this.validation = validation; this.trimInput = trimInput;
        System.Windows.Automation.AutomationProperties.SetName(Input, inputLabel);
        Hint.Text = hint ?? ""; Hint.Visibility = string.IsNullOrEmpty(hint) ? Visibility.Collapsed : Visibility.Visible;
        WindowStateService.FitInitialWindow(this);
        Loaded += (_, _) => { Input.Focus(); Input.SelectAll(); };
    }
    private void Accept(object sender, RoutedEventArgs e)
    {
        if (validation?.Invoke(Value) is { } error)
        { Hint.Text = error; Hint.Visibility = Visibility.Visible; Input.Focus(); return; }
        if (Value.Length != 0) DialogResult = true;
    }
}
