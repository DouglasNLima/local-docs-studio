using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LensDocsStudio.Native.Controls;

/// <summary>Measures the existing native menu and toolbar, then arranges them without reparenting controls.</summary>
public sealed class AdaptiveHeaderPanel : Panel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);
    private sealed class HeaderPeer(AdaptiveHeaderPanel owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(AdaptiveHeaderPanel);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
    private const double Gap = 12;
    private static readonly DependencyPropertyKey IsSingleRowPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSingleRow), typeof(bool), typeof(AdaptiveHeaderPanel), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsSingleRowProperty = IsSingleRowPropertyKey.DependencyProperty;
    public bool IsSingleRow => (bool)GetValue(IsSingleRowProperty);
    public double SingleRowRequiredWidth { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count != 2) throw new InvalidOperationException("The adaptive application header requires a menu and a command toolbar.");
        var menu = InternalChildren[0]; var toolbar = InternalChildren[1];
        menu.Measure(new(double.PositiveInfinity, availableSize.Height));
        toolbar.Measure(new(double.PositiveInfinity, availableSize.Height));
        SingleRowRequiredWidth = menu.DesiredSize.Width + (toolbar.Visibility == Visibility.Collapsed ? 0 : Gap + toolbar.DesiredSize.Width);
        SetValue(IsSingleRowPropertyKey, availableSize.Width >= SingleRowRequiredWidth);
        toolbar.Measure(new(Math.Max(0, availableSize.Width - (IsSingleRow ? menu.DesiredSize.Width + Gap : 0)), availableSize.Height));
        return new(Math.Min(availableSize.Width, SingleRowRequiredWidth), IsSingleRow ? Math.Max(menu.DesiredSize.Height, toolbar.DesiredSize.Height) : menu.DesiredSize.Height + toolbar.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var menu = InternalChildren[0]; var toolbar = InternalChildren[1];
        var menuWidth = Math.Min(menu.DesiredSize.Width, finalSize.Width);
        if (IsSingleRow)
        {
            menu.Arrange(new Rect(0, (finalSize.Height - menu.DesiredSize.Height) / 2, menuWidth, menu.DesiredSize.Height));
            var start = Math.Min(finalSize.Width, menuWidth + Gap);
            toolbar.Arrange(new Rect(start, 0, Math.Max(0, finalSize.Width - start), finalSize.Height));
        }
        else
        {
            menu.Arrange(new Rect(0, 0, finalSize.Width, menu.DesiredSize.Height));
            toolbar.Arrange(new Rect(0, menu.DesiredSize.Height, finalSize.Width, Math.Max(0, finalSize.Height - menu.DesiredSize.Height)));
        }
        AutomationProperties.SetHelpText(this, IsSingleRow ? "Menu and commands share one row" : "Commands are below the menu");
        return finalSize;
    }
}
