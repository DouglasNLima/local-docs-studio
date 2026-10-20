using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

/// <summary>Common chart inspector fields and exact SVG shape conversion; semantics remain in family drafts.</summary>
public abstract class ChartVisualEditorWindow : SourceVisualEditorWindow
{
    protected readonly TextBox Label, Identifier, Value, Maximum;
    protected override string Values => Label is null ? "" : string.Join('\u001f', Label.Text, Identifier.Text, Value.Text, Maximum.Text);
    protected ChartVisualEditorWindow(string family, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark, string diagramTheme, Action? editSource, FontFamily? sourceFont, double sourceFontSize)
        : base(family, diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        Label = Field("Label, title or source endpoint", "Label"); Identifier = Field("Identifier or target endpoint", "Identifier");
        Value = Field("Value, weight or minimum (decimal point)", "Value"); Maximum = Field("Maximum (blank for automatic)", "Maximum");
    }
    protected void Properties(string label = "", string identifier = "", string value = "", string maximum = "", bool editLabel = false, bool editIdentifier = false, bool editValue = false, bool editMaximum = false)
    {
        Label.Text = label; Identifier.Text = identifier; Value.Text = value; Maximum.Text = maximum;
        Label.IsEnabled = editLabel; Identifier.IsEnabled = editIdentifier; Value.IsEnabled = editValue; Maximum.IsEnabled = editMaximum;
        foreach (var field in new[] { Label, Identifier, Value, Maximum })
        {
            var visibility = field.IsEnabled || field.Text.Length > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            field.Visibility = visibility;
            InspectorFields.Children[InspectorFields.Children.IndexOf(field) - 1].Visibility = visibility;
        }
    }
    protected void Captions(string label, string identifier = "Identifier", string value = "Value (decimal point)", string maximum = "Maximum (blank for automatic)")
    {
        foreach (var (field, caption) in new[] { (Label, label), (Identifier, identifier), (Value, value), (Maximum, maximum) })
        {
            ((System.Windows.Controls.Label)InspectorFields.Children[InspectorFields.Children.IndexOf(field) - 1]).Content = caption;
            System.Windows.Automation.AutomationProperties.SetName(field, caption);
        }
    }
    protected static NativeSourceHit? Hit(string id, string description, XElement shape, XElement root)
    {
        var geometry = NativeSourceHit.ShapeGeometry(shape, root);
        return geometry is null || geometry.Bounds.IsEmpty ? null : new(id, description, geometry.Bounds, geometry);
    }
    protected static SourceAuthoringResult Protected(string error) => new(false, error);
}
