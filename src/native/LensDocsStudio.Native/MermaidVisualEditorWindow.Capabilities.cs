using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Native;

public partial class MermaidVisualEditorWindow
{
    private void UpdateCapabilityActions()
    {
        var analysis = draft.Model.Capabilities;
        var selected = Selected;
        var nodeId = selected?.StartsWith("node:", StringComparison.Ordinal) == true ? selected[5..] : null;
        var groupId = selected?.StartsWith("group:", StringComparison.Ordinal) == true ? selected[6..] : null;
        var diagnostics = new List<string>();
        Field(NodeLabel, MermaidOperationKind.NodeLabel, nodeId, "Label");
        Field(NodeId, MermaidOperationKind.NodeId, nodeId, "Node ID");
        Action(NodeShape, MermaidOperationKind.NodeShape, nodeId, "Shape");
        Action(NodeGroup, MermaidOperationKind.MoveNode, nodeId, "Membership");
        Action(MoveNodeButton, MermaidOperationKind.MoveNode, nodeId, "Move");
        Action(NodeClass, MermaidOperationKind.NodeClass, nodeId, "Class");
        Action(ClassButton, MermaidOperationKind.NodeClass, nodeId, "Assign class");
        foreach (var field in new[] { FillColour, StrokeColour, TextColour }) Field(field, MermaidOperationKind.NodeStyle, nodeId, "Local colour override");
        Action(ApplyStyleButton, MermaidOperationKind.NodeStyle, nodeId, "Apply colours");
        Field(EdgeLabel, MermaidOperationKind.ConnectionLabel, selected, "Label");
        Action(EdgeType, MermaidOperationKind.ConnectionType, selected, "Type");
        Action(EdgeSource, MermaidOperationKind.Reconnect, selected, "Source endpoint");
        Action(EdgeTarget, MermaidOperationKind.Reconnect, selected, "Destination endpoint");
        Action(ReverseEdgeButton, MermaidOperationKind.Reconnect, selected, "Reverse direction");
        Field(GroupLabel, MermaidOperationKind.SubgraphLabel, groupId, "Label");
        Action(GroupDirection, MermaidOperationKind.Direction, groupId, "Direction");
        Action(DeleteGroupContents, MermaidOperationKind.DeleteSubgraph, groupId, "Delete members");
        Action(Direction, MermaidOperationKind.Direction, null, "Diagram direction", !PropertiesPending);
        Action(InsertSubgraphButton, MermaidOperationKind.AddSubgraph, null, "Add subgraph", !PropertiesPending);
        Action(StartConnectionButton, MermaidOperationKind.AddConnection, null, "Add connection", !PropertiesPending);
        foreach (var button in Palette.Children.OfType<Button>().Where(button => Enum.TryParse<FlowNodeShape>(button.Tag as string, out _)))
            Action(button, MermaidOperationKind.AddNode, null, "Add node", !PropertiesPending);
        foreach (var port in ports.Values) Action(port, MermaidOperationKind.AddConnection, null, "Add connection", !PropertiesPending);
        var deletions = Elements.SelectedItems.OfType<ElementChoice>().Select(choice => analysis.Query(
            choice.Key.StartsWith("node:", StringComparison.Ordinal) ? MermaidOperationKind.DeleteNode : choice.Key.StartsWith("group:", StringComparison.Ordinal) ? MermaidOperationKind.DeleteSubgraph : MermaidOperationKind.DeleteConnection,
            choice.Key.StartsWith("node:", StringComparison.Ordinal) ? choice.Key[5..] : choice.Key.StartsWith("group:", StringComparison.Ordinal) ? choice.Key[6..] : choice.Key)).ToArray();
        if (deletions.Any(capability => !capability.Permitted))
        { DeleteButton.IsEnabled = false; Explain(DeleteButton, deletions.First(capability => !capability.Permitted).Diagnostic); }
        else Explain(DeleteButton, "Delete the selected elements after complete source validation.");
        var visible = NodeProperties.Visibility == Visibility.Visible ? new[] { NodeLabel, NodeId, FillColour }.Select(field => field.ToolTip?.ToString())
            : EdgeProperties.Visibility == Visibility.Visible ? new[] { EdgeLabel.ToolTip?.ToString(), EdgeSource.ToolTip?.ToString() } : [];
        CapabilitySummary.Text = draft.Model.Compatibility switch
        { MermaidCompatibility.PartiallyEditable => "Partially editable", MermaidCompatibility.Editable => "Editable", MermaidCompatibility.Invalid => "Invalid source", _ => "Protected" };
        CapabilitySummary.ToolTip = string.Join("\n", diagnostics.Distinct());
        AutomationProperties.SetHelpText(CapabilitySummary, string.Join("\n", visible.Where(text => text is not null)));
        Explain(FillColour, FillColour.ToolTip + " Colours show effective appearance; edits append a local override and preserve class definitions and custom properties.");

        void Field(TextBox field, MermaidOperationKind kind, string? target, string title)
        {
            var capability = analysis.Query(kind, target); field.IsReadOnly = !CanTransform || !capability.Permitted;
            Explain(field, title + ": " + (capability.Permitted ? "Editable. " : "Protected. ") + capability.Diagnostic);
            if (field.IsVisible) diagnostics.Add(field.ToolTip.ToString()!);
        }
        void Action(Control control, MermaidOperationKind kind, string? target, string title, bool additional = true)
        {
            var capability = analysis.Query(kind, target); control.IsEnabled = CanTransform && additional && capability.Permitted;
            Explain(control, title + ": " + (capability.Permitted ? "Editable. " : "Protected. ") + capability.Diagnostic);
            if (control.IsVisible) diagnostics.Add(control.ToolTip.ToString()!);
        }
    }
    private static void Explain(FrameworkElement element, string explanation)
    {
        element.ToolTip = explanation; ToolTipService.SetShowOnDisabled(element, true);
        AutomationProperties.SetHelpText(element, explanation);
    }
    private string CompatibilityStatus => draft.Model.Compatibility == MermaidCompatibility.PartiallyEditable
        ? "Partially editable. Select an element to inspect its permitted operations. Every change validates the complete diagram."
        : !draft.Model.CanEdit ? draft.Model.Diagnostic ?? "Protected source. Inspect the original diagram or edit its source."
        : draft.IsDirty ? "Draft modified. Apply when ready." : "Ready. Select an element or add one from the palette.";
}
