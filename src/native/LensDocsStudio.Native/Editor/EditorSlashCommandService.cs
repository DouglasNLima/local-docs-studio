using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Native.Editor;

/// <summary>A caret popup that leaves keyboard focus and editing ownership in AvalonEdit.</summary>
public sealed class EditorSlashCommandService : IDisposable
{
    private readonly TextEditor editor;
    private readonly Func<bool> permitted;
    private readonly Func<string, string?> unavailable;
    private readonly Action<string, SlashContext> execute;
    private readonly Action closeOther;
    private readonly Popup popup;
    private readonly ListBox list = new() { MinWidth = 270, MaxWidth = 420, MaxHeight = 280, Focusable = false };
    private SlashContext? context;
    private bool composing;
    private bool dismissed;
    public bool IsOpen => popup.IsOpen;
    public ListBox CommandList => list;

    public EditorSlashCommandService(TextEditor editor, Func<bool> permitted, Func<string, string?> unavailable,
        Action<string, SlashContext> execute, Action closeOther)
    {
        this.editor = editor; this.permitted = permitted; this.unavailable = unavailable; this.execute = execute; this.closeOther = closeOther;
        list.SetResourceReference(Control.BackgroundProperty, "PanelBrush"); list.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        list.SetResourceReference(Control.BorderBrushProperty, "BorderBrush"); list.BorderThickness = new Thickness(1);
        AutomationProperties.SetName(list, "Markdown slash commands"); AutomationProperties.SetAutomationId(list, "SlashCommands");
        AutomationProperties.SetLiveSetting(list, AutomationLiveSetting.Polite);
        popup = new() { Child = list, PlacementTarget = editor.TextArea.TextView, Placement = PlacementMode.Custom, StaysOpen = true,
            CustomPopupPlacementCallback = (size, _, _) =>
            {
                var caret = editor.TextArea.Caret.CalculateCaretRectangle(); var scroll = editor.TextArea.TextView.ScrollOffset;
                return [new(new Point(caret.Left - scroll.X, caret.Bottom - scroll.Y), PopupPrimaryAxis.Horizontal),
                    new(new Point(caret.Left - scroll.X, caret.Top - scroll.Y - size.Height), PopupPrimaryAxis.Horizontal)];
            } };
        editor.TextArea.TextEntered += TextEntered; editor.DocumentChanged += DocumentChanged;
        editor.TextArea.TextView.ScrollOffsetChanged += Scrolled; editor.LostKeyboardFocus += LostFocus;
        editor.PreviewMouseDown += EditorMouseDown;
        editor.PreviewKeyUp += KeyReleased;
        TextCompositionManager.AddPreviewTextInputStartHandler(editor, CompositionStarted);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(editor, CompositionStarted);
        list.PreviewMouseLeftButtonDown += ChooseWithMouse;
    }
    public void Refresh()
    {
        if (dismissed || composing || !permitted() || editor.IsReadOnly || !editor.IsEnabled || editor.SelectionLength != 0)
        { Close(); return; }
        context = SlashCommandService.Query(editor.Text, editor.CaretOffset, context);
        if (context is null) { Close(); return; }
        closeOther(); list.Items.Clear();
        foreach (var item in SlashCommandService.Catalogue.Where(item => item.Label.Contains(context.Query, StringComparison.OrdinalIgnoreCase)))
        {
            var reason = unavailable(item.Id);
            var row = new ListBoxItem { Content = item.Label + (reason is null ? "" : " — " + reason), Tag = item, Focusable = false,
                IsEnabled = reason is null, Padding = new Thickness(8, 5, 8, 5) };
            AutomationProperties.SetName(row, Convert.ToString(row.Content)); list.Items.Add(row);
        }
        list.SelectedIndex = list.Items.Count == 0 ? -1 : 0; popup.IsOpen = true; Announce();
    }
    private void Announce()
    {
        var text = list.SelectedItem is ListBoxItem row ? Convert.ToString(row.Content) : "No matching commands. Escape keeps the text.";
        AutomationProperties.SetName(list, "Markdown slash commands. " + text);
        if (UIElementAutomationPeer.CreatePeerForElement(list) is { } peer && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        if (list.SelectedItem is { } selected) list.ScrollIntoView(selected);
    }
    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsOpen || composing || Keyboard.Modifiers != ModifierKeys.None) return false;
        if (e.Key == Key.Escape) { dismissed = true; Close(); }
        else if (e.Key is Key.Down or Key.Up)
        {
            if (list.Items.Count > 0) list.SelectedIndex = (list.SelectedIndex + (e.Key == Key.Down ? 1 : -1) + list.Items.Count) % list.Items.Count;
            Announce();
        }
        else if (e.Key == Key.Enter) Accept();
        else { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageDown or Key.PageUp or Key.Tab) Close(); return false; }
        e.Handled = true; return true;
    }
    public void Accept()
    {
        if (list.SelectedItem is not ListBoxItem { Tag: SlashCommand command } || unavailable(command.Id) is not null
            || !permitted() || editor.IsReadOnly || context is null || SlashCommandService.Query(editor.Text, editor.CaretOffset) != context) return;
        var range = context; dismissed = true; Close(); execute(command.Id, range);
    }
    private void TextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (composing) { composing = false; dismissed = true; Close(); return; }
        if (e.Text == "/") dismissed = false;
        Refresh();
    }
    private void CompositionStarted(object sender, TextCompositionEventArgs e) { composing = true; Close(); }
    private void KeyReleased(object sender, KeyEventArgs e) { if (IsOpen && e.Key is Key.Back or Key.Delete) Refresh(); }
    private void ChooseWithMouse(object sender, MouseButtonEventArgs e)
    {
        var row = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (row is null) return;
        e.Handled = true; list.SelectedItem = row; Accept();
    }
    private void EditorMouseDown(object sender, MouseButtonEventArgs e) { dismissed = true; Close(); }
    private void Scrolled(object? sender, EventArgs e) { if (IsOpen) { popup.HorizontalOffset += .01; popup.HorizontalOffset -= .01; } }
    private void LostFocus(object sender, KeyboardFocusChangedEventArgs e) => Close();
    private void DocumentChanged(object? sender, EventArgs e) { dismissed = false; Close(); }
    public void Close() { popup.IsOpen = false; context = null; }
    public void Dispose()
    {
        Close(); editor.TextArea.TextEntered -= TextEntered; editor.DocumentChanged -= DocumentChanged;
        editor.TextArea.TextView.ScrollOffsetChanged -= Scrolled; editor.LostKeyboardFocus -= LostFocus; editor.PreviewMouseDown -= EditorMouseDown;
        editor.PreviewKeyUp -= KeyReleased;
        TextCompositionManager.RemovePreviewTextInputStartHandler(editor, CompositionStarted);
        TextCompositionManager.RemovePreviewTextInputUpdateHandler(editor, CompositionStarted);
        list.PreviewMouseLeftButtonDown -= ChooseWithMouse;
    }
}
