using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LensDocsStudio.Native.Controls;

/// <summary>A persistent native inspector group with an explicit UI Automation boundary.</summary>
public sealed class MermaidInspectorPanel : DockPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new InspectorPeer(this);
    private sealed class InspectorPeer(MermaidInspectorPanel owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(MermaidInspectorPanel);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }
}
