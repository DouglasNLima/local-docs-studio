using System.Windows.Automation.Peers;
using ICSharpCode.AvalonEdit;

namespace LensDocsStudio.Native.Controls;

/// <summary>Expose AvalonEdit's actual typing surface through its existing automation contracts.</summary>
public sealed class MarkdownEditor : TextEditor
{
    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownEditorAutomationPeer(this);
    private sealed class MarkdownEditorAutomationPeer(MarkdownEditor owner) : TextEditorAutomationPeer(owner)
    {
        protected override bool IsKeyboardFocusableCore() => owner.IsEnabled && owner.TextArea.Focusable;
        protected override bool HasKeyboardFocusCore() => owner.TextArea.IsKeyboardFocusWithin;
        protected override void SetFocusCore() => owner.TextArea.Focus();
    }
}
