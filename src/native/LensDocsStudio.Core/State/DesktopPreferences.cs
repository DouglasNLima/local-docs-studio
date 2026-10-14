namespace LensDocsStudio.Core.State;

/// <summary>Private desktop state; synchronisation is opt-in for existing users.</summary>
public sealed record DesktopPreferences(bool ShowSidebar = true, double SidebarWidth = 230, double OutlineWidth = 240,
    bool FollowCaret = false, bool SyncScroll = false, bool SyncSelection = false)
{
    public bool IsValid => double.IsFinite(SidebarWidth) && SidebarWidth is >= 150 and <= 600
        && double.IsFinite(OutlineWidth) && OutlineWidth is >= 160 and <= 600;
}
