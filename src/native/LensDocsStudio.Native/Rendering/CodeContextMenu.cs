using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Rendering;

public sealed record PreviewTextCopy(string Text, bool Markdown);

public static class CodeContextMenu
{
    public const int MaximumCopyCharacters = 4 * 1024 * 1024;
    public static ContextMenu Create(CodeBlock code, string source, Action<PreviewTextCopy>? copy)
    {
        var menu = new ContextMenu();
        var validSpan = code.Span.Start >= 0 && code.Span.End < source.Length && code.Span.End >= code.Span.Start;
        var start = validSpan && code.Span.Start > 0 ? source.LastIndexOf('\n', code.Span.Start - 1) + 1 : 0;
        var length = validSpan ? code.Span.End - start + 1 : 0;
        var bounded = validSpan && length <= MaximumCopyCharacters;
        Add("Copy code", "CopyCodeBlock", false);
        Add("Copy Markdown source", "CopyCodeMarkdown", true);
        return menu;

        void Add(string label, string id, bool markdown)
        {
            var item = new MenuItem { Header = label, IsEnabled = copy is not null && bounded };
            AutomationProperties.SetAutomationId(item, id);
            if (!bounded) { item.ToolTip = "Copy supports code blocks up to 4 million characters."; ToolTipService.SetShowOnDisabled(item, true); }
            item.Click += (_, _) =>
            {
                if (copy is null || !bounded) return;
                copy(new(markdown ? source.Substring(start, length) : code.Lines.ToString(), markdown));
            };
            menu.Items.Add(item);
        }
    }
}
