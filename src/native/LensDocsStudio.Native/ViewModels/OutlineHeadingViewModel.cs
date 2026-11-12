using CommunityToolkit.Mvvm.ComponentModel;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.ViewModels;

public partial class OutlineHeadingViewModel(DocumentHeading heading, OutlineHeadingViewModel? parent) : ObservableObject
{
    public DocumentHeading Heading { get; } = heading;
    public OutlineHeadingViewModel? Parent { get; } = parent;
    public List<OutlineHeadingViewModel> Children { get; } = [];
    public string Title => Heading.Title;
    public string LevelLabel => "H" + Heading.Level;
    public string AccessibleName => $"Heading level {Heading.Level}: {Title}";
    [ObservableProperty] private bool isExpanded = true;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isCurrent;
    [ObservableProperty] private bool isVisible = true;
}
