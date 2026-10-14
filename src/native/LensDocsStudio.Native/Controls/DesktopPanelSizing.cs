namespace LensDocsStudio.Native.Controls;

public readonly record struct DesktopPanelWidths(double Sidebar, double Outline);

/// <summary>Keep usable document panes without overwriting preferred panel widths.</summary>
public static class DesktopPanelSizing
{
    public static DesktopPanelWidths Fit(double windowWidth, bool split, double sidebar, double outline)
    {
        var sidebarMinimum = sidebar > 0 ? 150d : 0; var outlineMinimum = outline > 0 ? 160d : 0;
        sidebar = sidebar > 0 ? Math.Clamp(sidebar, sidebarMinimum, 600) : 0;
        outline = outline > 0 ? Math.Clamp(outline, outlineMinimum, 600) : 0;
        var minimum = sidebarMinimum + outlineMinimum;
        var budget = Math.Max(minimum, windowWidth - 26 - (split ? 520 : 400));
        var flexible = sidebar + outline - minimum;
        if (sidebar + outline <= budget || flexible <= 0) return new(sidebar, outline);
        var scale = Math.Clamp((budget - minimum) / flexible, 0, 1);
        return new(sidebarMinimum + (sidebar - sidebarMinimum) * scale, outlineMinimum + (outline - outlineMinimum) * scale);
    }
}
