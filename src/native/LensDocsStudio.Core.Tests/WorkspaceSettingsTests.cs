using System.Text.Json;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceSettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-preferences-" + Guid.NewGuid().ToString("N"));
    public WorkspaceSettingsTests() => Directory.CreateDirectory(root);

    [Fact] public void Earlier_preferences_gain_reading_defaults_without_losing_existing_choices()
    {
        var original = JsonSerializer.Deserialize<UserPreferences>("""{"Theme":"Dark","Layout":"Editor","AutomaticUpdateChecks":false,"UpdateChannel":"Preview","RecentDocuments":["C:\\Docs\\Café.md"]}""")!;
        var settings = WorkspaceSettings.From(original);
        Assert.True(WorkspaceSettings.ArePreferencesValid(original));
        Assert.Equal("Dark", settings.Theme); Assert.Equal("Editor", settings.Layout);
        Assert.Equal(new EditorPreferences(), settings.Editor); Assert.Equal(new PreviewPreferences(), settings.Preview);
        Assert.True(settings.ShowOutline); Assert.False(original.AutomaticUpdateChecks);
        Assert.Equal("C:\\Docs\\Café.md", Assert.Single(original.RecentDocuments!));
    }

    [Fact] public async Task Appearance_round_trip_preserves_latest_update_consent_paths_and_window_state()
    {
        var current = new UserPreferences(AutomaticUpdateChecks: false, UpdateChannel: "Preview", LastUpdateCheck: DateTimeOffset.UtcNow,
            IgnoredVersion: "2.0.0", RecentDocuments: ["C:\\Café.md"], RecentWorkspaces: ["C:\\Docs"], Window: new(4, 8, 1400, 900, true));
        var settings = new WorkspaceSettings("Dark", "Preview", new("Consolas", 21, false, false, 3, true), new(135), false);
        var changed = settings.ApplyTo(current); var store = new SessionStore(root);
        await store.WriteAsync("preferences.json", changed);
        var restored = (await store.ReadAsync<UserPreferences>("preferences.json", validate: WorkspaceSettings.ArePreferencesValid))!;
        Assert.Equal(settings, WorkspaceSettings.From(restored));
        Assert.Equal(current.AutomaticUpdateChecks, restored.AutomaticUpdateChecks); Assert.Equal(current.UpdateChannel, restored.UpdateChannel);
        Assert.Equal(current.LastUpdateCheck, restored.LastUpdateCheck); Assert.Equal(current.IgnoredVersion, restored.IgnoredVersion);
        Assert.Equal(current.RecentDocuments, restored.RecentDocuments); Assert.Equal(current.RecentWorkspaces, restored.RecentWorkspaces); Assert.Equal(current.Window, restored.Window);
    }

    [Fact] public void Earlier_saved_preview_zoom_gains_automatic_reading_without_losing_appearance_or_consent()
    {
        var original = JsonSerializer.Deserialize<UserPreferences>("""{"Theme":"Dark","Preview":{"Zoom":135},"AutomaticUpdateChecks":false,"UpdateChannel":"Preview"}""")!;
        Assert.True(WorkspaceSettings.ArePreferencesValid(original));
        Assert.Equal(new PreviewPreferences(135, "Automatic"), WorkspaceSettings.From(original).Preview);
        Assert.Equal("Dark", original.Theme); Assert.False(original.AutomaticUpdateChecks); Assert.Equal("Preview", original.UpdateChannel);
    }

    [Theory]
    [InlineData(9, 4, 100)] [InlineData(37, 4, 100)] [InlineData(14, 1, 100)] [InlineData(14, 9, 100)]
    [InlineData(14, 4, 49)] [InlineData(14, 4, 201)]
    public void Unsupported_sizes_are_rejected_before_persistence(int font, int indentation, int zoom)
    {
        var settings = WorkspaceSettings.Default with { Editor = new(FontSize: font, IndentationSize: indentation), Preview = new(zoom) };
        Assert.False(settings.IsValid); Assert.Throws<ArgumentException>(() => settings.ApplyTo(new()));
    }

    [Theory] [InlineData("file:///C:/untrusted.ttf")] [InlineData("Unknown font")] [InlineData(null)]
    public void Font_choices_are_local_and_explicit(string? font)
    {
        Assert.False((WorkspaceSettings.Default with { Editor = new(FontFamily: font!) }).IsValid);
    }

    [Theory] [InlineData("Unknown", "Split", "Stable")] [InlineData("Light", "Unknown", "Stable")] [InlineData("System", "Editor", "Unknown")]
    public void Unknown_enumerated_preferences_are_rejected(string theme, string layout, string channel)
        => Assert.False(WorkspaceSettings.ArePreferencesValid(new(Theme: theme, Layout: layout, UpdateChannel: channel)));

    [Fact] public async Task Unsupported_saved_values_are_retained_before_recovering_the_last_good_preferences()
    {
        var store = new SessionStore(root); var good = new UserPreferences(Theme: "Dark", Editor: new(FontSize: 18));
        await store.WriteAsync("preferences.json", good);
        await store.WriteAsync("preferences.json", good with { Editor = new(FontSize: int.MaxValue) });
        var restored = await store.ReadAsync<UserPreferences>("preferences.json", validate: WorkspaceSettings.ArePreferencesValid);
        Assert.Equal(good, restored); Assert.Single(store.ReadWarnings);
        var retained = Assert.Single(Directory.GetFiles(root, "preferences.json.unreadable-*.json"));
        Assert.Contains(int.MaxValue.ToString(), await File.ReadAllTextAsync(retained), StringComparison.Ordinal);
        await store.WriteAsync("preferences.json", good); Assert.True(File.Exists(retained));
    }

    [Fact] public void Defaults_only_replace_appearance_preferences()
    {
        var current = new UserPreferences(Theme: "Dark", AutomaticUpdateChecks: false, UpdateChannel: "Preview", IgnoredVersion: "9.0", Editor: new(FontSize: 32));
        var reset = WorkspaceSettings.Default.ApplyTo(current);
        Assert.Equal(WorkspaceSettings.Default, WorkspaceSettings.From(reset)); Assert.False(reset.AutomaticUpdateChecks);
        Assert.Equal("Preview", reset.UpdateChannel); Assert.Equal("9.0", reset.IgnoredVersion);
    }

    public void Dispose() => Directory.Delete(root, true);
}
