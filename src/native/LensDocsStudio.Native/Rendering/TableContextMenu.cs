using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LensDocsStudio.Core.Exports;
using MdTable = Markdig.Extensions.Tables.Table;

namespace LensDocsStudio.Native.Rendering;

public static class TableContextMenu
{
    public static ContextMenu Create(MdTable table, string source, Action<ExportTableSelection>? copy, Action<ExportTableSelection>? exportCsv = null)
    {
        var menu = new ContextMenu(); AutomationProperties.SetName(menu, "Table actions");
        var bounded = source.Length <= TableCsvExporter.MaximumSourceCharacters && table.Span.Start >= 0 && table.Span.Length > 0 && table.Span.End < source.Length;
        Add("Copy table for Excel", "CopyPreviewTable", copy);
        Add("Export table CSV…", "ExportPreviewTableCsv", exportCsv);
        return menu;
        void Add(string label, string id, Action<ExportTableSelection>? action)
        {
            var item = new MenuItem { Header = label, IsEnabled = action is not null && bounded };
            AutomationProperties.SetAutomationId(item, id);
            if (!bounded) { item.ToolTip = "Table actions support documents up to 4 million characters."; ToolTipService.SetShowOnDisabled(item, true); }
            item.Click += (_, _) => { if (action is not null && bounded) action(new(table.Span.Start, table.Span.Length)); };
            menu.Items.Add(item);
        }
    }
}
