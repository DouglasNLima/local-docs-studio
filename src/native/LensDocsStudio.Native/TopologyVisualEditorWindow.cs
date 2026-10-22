using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

/// <summary>Shared native selectors, explicit identifiers and canonical path targets for topology editors.</summary>
public abstract class TopologyVisualEditorWindow : ChartVisualEditorWindow
{
    protected TopologyVisualEditorWindow(string family, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark, string diagramTheme, Action? editSource, FontFamily? sourceFont, double sourceFontSize)
        : base(family, diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize) { }
    protected ComboBox Selector(string caption, string id, IEnumerable<string>? values = null)
    {
        var box = new ComboBox { Style = (Style)FindResource("Form.ComboBox") }; Identify(box, id, caption);
        InspectorFields.Children.Add(new Label { Content = caption, Target = box, Style = (Style)FindResource("Form.Label") }); InspectorFields.Children.Add(box);
        if (values is not null) box.ItemsSource = values.ToArray();
        box.SelectionChanged += (_, _) => FieldsChanged(); return box;
    }
    protected string? PromptIdentifier(string kind, Func<string, bool> exists)
    {
        var prompt = new TextPromptWindow("Add " + kind, "Choose the new " + kind + " identifier. Its label can be edited in the inspector.", "", "Add", "New " + kind + " identifier",
            "Begin with a letter or underscore; use letters, digits, underscores or hyphens.", value =>
            { try { TopologySource.Identifier(value); return exists(value) ? "That identifier already exists." : null; } catch (ArgumentException error) { return error.Message; } }, trimInput: false) { Owner = this };
        return prompt.ShowDialog() == true ? prompt.Value : null;
    }
    protected static string Selected(ComboBox box, string fallback = "") => box.SelectedItem as string ?? fallback;
    protected static NativeSourceHit? PathHit(string identity, string description, XElement path, XElement root)
    {
        var geometry = NativeSourceHit.ShapeGeometry(path, root)?.GetWidenedPathGeometry(new Pen(Brushes.Black, 8));
        if (geometry is null || geometry.Bounds.IsEmpty) return null;
        geometry.Freeze(); return new(identity, description, geometry.Bounds, geometry);
    }
}
