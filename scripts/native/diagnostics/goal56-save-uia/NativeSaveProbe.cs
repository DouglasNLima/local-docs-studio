using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using LensDocsStudio.Core.Diagnostics;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void InitialiseSaveProbe()
    {
        SaveTimeline.NativeState = () => new { IsSaving = model.ActiveDocument?.IsSaving, ReadOnly = Editor.IsReadOnly,
            Enabled = Editor.IsEnabled, EditorIdentity = RuntimeHelpers.GetHashCode(Editor),
            DocumentIdentity = RuntimeHelpers.GetHashCode(Editor.Document), FocusWithin = Editor.IsKeyboardFocusWithin,
            FocusedType = System.Windows.Input.Keyboard.FocusedElement?.GetType().FullName,
            DispatcherAccess = Dispatcher.CheckAccess(), Pending = SaveTimeline.Pending };
        DependencyPropertyDescriptor.FromProperty(ICSharpCode.AvalonEdit.TextEditor.IsReadOnlyProperty, typeof(Controls.MarkdownEditor))!
            .AddValueChanged(Editor, (_, _) => SaveTimeline.Event("editor.read-only.changed", SaveTimeline.NativeState()));
        SaveTimeline.Event("native.probe.attached", new { State = SaveTimeline.NativeState(),
            Wpf = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(Window).Assembly.Location).ProductVersion,
            WpfPath = typeof(Window).Assembly.Location, Provider = typeof(ICSharpCode.AvalonEdit.TextEditorAutomationPeer).Assembly.FullName });
        Dispatcher.Hooks.OperationPosted += (_, e) => ObserveDispatch("posted", e);
        Dispatcher.Hooks.OperationStarted += (_, e) => ObserveDispatch("started", e);
        Dispatcher.Hooks.OperationCompleted += (_, e) => ObserveDispatch("completed", e);
        Dispatcher.Hooks.OperationAborted += (_, e) => ObserveDispatch("aborted", e);
        static void ObserveDispatch(string phase, DispatcherHookEventArgs e)
        {
            if (SaveTimeline.Saving) SaveTimeline.Event("dispatcher." + phase, new {
                Identity = RuntimeHelpers.GetHashCode(e.Operation), Priority = e.Operation.Priority.ToString(), Status = e.Operation.Status.ToString() });
        }
    }
}
