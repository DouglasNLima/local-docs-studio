using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LensDocsStudio.Core.Exports;
using MdTable = Markdig.Extensions.Tables.Table;

namespace LensDocsStudio.Native.Rendering;

public static class TableContextMenu
{
    public static ContextMenu Create(MdTable table, string source, Action<ExportTableSelection>? copy)
    {
        var menu = new ContextMenu(); AutomationProperties.SetName(menu, "Table actions");
        var bounded = source.Length <= 4 * 1024 * 1024 && table.Span.Start >= 0 && table.Span.Length > 0 && table.Span.End < source.Length;
        var item = new MenuItem { Header = "Copy table for Excel", IsEnabled = copy is not null && bounded };
        AutomationProperties.SetAutomationId(item, "CopyPreviewTable");
        if (!bounded) { item.ToolTip = "Table copying supports documents up to 4 million characters."; ToolTipService.SetShowOnDisabled(item, true); }
        item.Click += (_, _) => { if (copy is not null && bounded) copy(new(table.Span.Start, table.Span.Length)); };
        menu.Items.Add(item); return menu;
    }
}
