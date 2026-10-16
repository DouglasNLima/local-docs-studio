using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Func<WorkspaceSettings, Task> save;
    private readonly DesktopPreferences desktop;
    [ObservableProperty] private bool showWorkspace = true;
    [ObservableProperty] private bool followCaret;
    [ObservableProperty] private bool synchroniseScroll;
    [ObservableProperty] private bool synchroniseSelection;
    public DesktopPreferences CurrentDesktop => desktop with { ShowSidebar = ShowWorkspace, FollowCaret = FollowCaret, SyncScroll = SynchroniseScroll, SyncSelection = SynchroniseSelection };
    [ObservableProperty] private string theme = "System";
    [ObservableProperty] private string layout = "Split";
    [ObservableProperty] private string fontFamily = "Cascadia Code, Consolas";
    [ObservableProperty] private int fontSize = 14;
    [ObservableProperty] private bool wordWrap = true;
    [ObservableProperty] private bool showLineNumbers = true;
    [ObservableProperty] private int indentationSize = 4;
    [ObservableProperty] private bool convertTabsToSpaces;
    [ObservableProperty] private int previewZoom = 100;
    [ObservableProperty] private string previewReadingMode = "Automatic";
    [ObservableProperty] private string diagramTheme = "Auto";
    [ObservableProperty] private bool showOutline = true;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string status = "Changes apply to this device when you choose Apply. Document text and undo history are retained.";
    public IReadOnlyList<string> Themes => WorkspaceSettings.Themes;
    public IReadOnlyList<string> Layouts => WorkspaceSettings.Layouts;
    public IReadOnlyList<string> Fonts => WorkspaceSettings.Fonts;
    public IReadOnlyList<string> ReadingModes => WorkspaceSettings.ReadingModes;
    public IReadOnlyList<string> DiagramThemes => Core.Rendering.DiagramThemes.Choices;
    public IReadOnlyList<int> FontSizes { get; } = Enumerable.Range(10, 27).ToArray();
    public IReadOnlyList<int> IndentationSizes { get; } = Enumerable.Range(2, 7).ToArray();
    public bool CanEdit => !IsSaving;
    public event Action? Applied;
    public WorkspaceSettings Current => new(Theme, Layout, new(FontFamily, FontSize, WordWrap, ShowLineNumbers, IndentationSize, ConvertTabsToSpaces), new(PreviewZoom, PreviewReadingMode, DiagramTheme), ShowOutline);
    public void Import(ReadOnlyMemory<byte> bytes)
    {
        var imported = SettingsTransferFormat.Read(bytes, Current);
        Read(imported);
        Status = "Imported appearance choices are ready for review. Choose Apply to save, or Cancel to keep your current settings.";
    }

    public SettingsViewModel(WorkspaceSettings initial, Func<WorkspaceSettings, Task> save, DesktopPreferences? desktop = null)
    {
        this.save = save; this.desktop = desktop ?? new(); Read(initial); ReadDesktop(this.desktop);
    }
    private void ReadDesktop(DesktopPreferences value)
    {
        ShowWorkspace = value.ShowSidebar; FollowCaret = value.FollowCaret;
        SynchroniseScroll = value.SyncScroll; SynchroniseSelection = value.SyncSelection;
    }
    private void Read(WorkspaceSettings settings)
    {
        Theme = settings.Theme; Layout = settings.Layout; ShowOutline = settings.ShowOutline;
        FontFamily = settings.Editor.FontFamily; FontSize = settings.Editor.FontSize; WordWrap = settings.Editor.WordWrap;
        ShowLineNumbers = settings.Editor.ShowLineNumbers; IndentationSize = settings.Editor.IndentationSize;
        ConvertTabsToSpaces = settings.Editor.ConvertTabsToSpaces; PreviewZoom = settings.Preview.Zoom; PreviewReadingMode = settings.Preview.ReadingMode;
        DiagramTheme = settings.Preview.DiagramTheme;
    }
    partial void OnIsSavingChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); ApplyCommand.NotifyCanExecuteChanged(); ResetCommand.NotifyCanExecuteChanged(); }
    [RelayCommand(CanExecute = nameof(CanEdit))] private void Reset()
    {
        Read(WorkspaceSettings.Default); Status = "Default appearance settings selected. Choose Apply to save them, or Cancel to keep your current settings.";
        ReadDesktop(new());
    }
    [RelayCommand(CanExecute = nameof(CanEdit))] private async Task ApplyAsync()
    {
        var settings = Current;
        if (!settings.IsValid) { Status = "Choose valid settings before applying them."; return; }
        IsSaving = true;
        try { await save(settings); Status = "Settings saved."; Applied?.Invoke(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Status = "Settings could not be saved: " + ex.Message; }
        finally { IsSaving = false; }
    }
}
