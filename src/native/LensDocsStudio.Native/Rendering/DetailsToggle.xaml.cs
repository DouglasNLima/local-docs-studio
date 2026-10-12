using System.Windows.Controls;

namespace LensDocsStudio.Native.Rendering;

/// <summary>A native expansion control sized for a selectable FlowDocument summary line.</summary>
public partial class DetailsToggle : Expander
{
    public DetailsToggle()
    {
        PreviewConstructionEvents.Log.Stage("details-toggle-initialise");
        InitializeComponent();
        PreviewConstructionEvents.Log.Stage("details-toggle-initialised");
    }
}
