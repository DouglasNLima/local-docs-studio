using LensDocsStudio.Core.Diagnostics;

namespace LensDocsStudio.Native.ViewModels;

public partial class DocumentViewModel
{
    partial void OnIsSavingChanged(bool value) => SaveTimeline.Event("is-saving.changed-before-notification", new { Value = value });
}
