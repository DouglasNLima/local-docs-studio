using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace LensDocsStudio.Native.Rendering;

/// <summary>A native expansion control sized for a selectable FlowDocument summary line.</summary>
public sealed class DetailsToggle : Expander
{
    // Templates belong to their WPF thread. Parse this fixed application markup
    // once per thread without a pack-resource lookup while constructing a preview.
    [ThreadStatic] private static ControlTemplate? sharedTemplate;

    public DetailsToggle()
    {
        PreviewConstructionEvents.Log.Stage("details-toggle-initialise");
        Width = Height = 30; Focusable = false; Background = Brushes.Transparent; BorderThickness = new Thickness(0);
        Template = sharedTemplate ??= (ControlTemplate)XamlReader.Parse(TemplateMarkup);
        PreviewConstructionEvents.Log.Stage("details-toggle-initialised");
    }

    private const string TemplateMarkup = """
        <ControlTemplate TargetType="Expander"
         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
         <ToggleButton x:Name="HeaderSite" Width="28" Height="28" MinWidth="28" MinHeight="28" Padding="0" BorderThickness="0"
          Background="Transparent" Foreground="{TemplateBinding Foreground}"
          IsChecked="{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"
          AutomationProperties.Name="{Binding Path=(AutomationProperties.Name), RelativeSource={RelativeSource TemplatedParent}}">
          <Path x:Name="Chevron" Width="12" Height="12" Stretch="None" Data="M 4,2 L 8,6 L 4,10" StrokeThickness="1.5"
           Stroke="{Binding Foreground, RelativeSource={RelativeSource TemplatedParent}}"/>
         </ToggleButton>
         <ControlTemplate.Triggers>
          <Trigger Property="IsExpanded" Value="True"><Setter TargetName="Chevron" Property="RenderTransform"><Setter.Value><RotateTransform Angle="90" CenterX="6" CenterY="6"/></Setter.Value></Setter></Trigger>
         </ControlTemplate.Triggers>
        </ControlTemplate>
        """;
}
