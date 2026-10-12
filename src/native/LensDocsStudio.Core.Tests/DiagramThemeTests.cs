using System.Text;
using System.Text.Json;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class DiagramThemeTests
{
    private const string Source = "flowchart LR\r\nA[Café] --> B[Review]";

    [Theory]
    [InlineData("Lens", "#FAFAFA", "#FF883E")]
    [InlineData("Default", "#FFFFFF", "#ECECFF")]
    [InlineData("Neutral", "#FAFAFA", "#F4F4F5")]
    [InlineData("Forest", "#F7FCF7", "#E7F3E7")]
    [InlineData("Dark", "#202124", "#F3F4F6")]
    public void Fixed_themes_keep_their_colours_in_either_application_appearance(string choice, string background, string colour)
    {
        var service = new DiagramService();
        var light = service.Render(Source, false, choice);
        var dark = service.Render(Source, true, choice);
        Assert.True(light.Success, light.Error); Assert.Same(light, dark);
        Assert.Equal(Source, light.Source);
        Assert.Contains(background, light.Svg!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(colour, light.Svg!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Café", light.Svg!); Assert.Contains("Review", light.Svg!);
    }

    [Fact]
    public void Automatic_appearance_and_fixed_palettes_do_not_share_incorrect_cached_results()
    {
        var service = new DiagramService();
        var light = service.Render(Source);
        var dark = service.Render(Source, true);
        var forest = service.Render(Source, false, "Forest");
        Assert.NotEqual(light.Svg, dark.Svg); Assert.NotEqual(light.Svg, forest.Svg);
        Assert.Same(light, service.Render(Source, true, "Lens"));
        Assert.Same(dark, service.Render(Source, false, "Dark"));
        Assert.Same(forest, service.Render(Source, true, "Forest"));
        Assert.NotSame(forest, service.Render(Source + "\r\nB --> C[Publish]", true, "Forest"));
    }

    [Theory]
    [InlineData("Auto")] [InlineData("Lens")] [InlineData("Default")]
    [InlineData("Neutral")] [InlineData("Forest")] [InlineData("Dark")]
    public async Task Diagram_choice_survives_state_and_portable_settings_without_changing_update_consent(string choice)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lens-theme-" + Guid.NewGuid().ToString("N"));
        try
        {
            var original = new UserPreferences(AutomaticUpdateChecks: false, UpdateChannel: "Preview", Preview: new(125, "Pages", choice));
            var store = new SessionStore(directory);
            await store.WriteAsync("preferences.json", original);
            var restored = await new SessionStore(directory).ReadAsync<UserPreferences>("preferences.json", validate: WorkspaceSettings.ArePreferencesValid);
            Assert.NotNull(restored);
            Assert.Equal(original, restored);
            var settings = WorkspaceSettings.From(restored);
            var exported = SettingsTransferFormat.Write(settings);
            Assert.Contains("lens-docs-studio-settings-1.2", Encoding.UTF8.GetString(exported));
            Assert.Equal(settings, SettingsTransferFormat.Read(exported, WorkspaceSettings.Default));
            Assert.False(settings.ApplyTo(original).AutomaticUpdateChecks);
            Assert.Equal("Preview", settings.ApplyTo(original).UpdateChannel);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("lens-docs-studio-settings-1.0")]
    [InlineData("lens-docs-studio-settings-1.1")]
    public void Older_portable_settings_preserve_the_selected_diagram_theme(string version)
    {
        var current = WorkspaceSettings.Default with { Preview = new(125, "Pages", "Forest") };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = version, preview = new { zoom = 150 } });
        Assert.Equal(current with { Preview = current.Preview with { Zoom = 150 } }, SettingsTransferFormat.Read(bytes, current));
        var extended = JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = version, preview = new { diagramTheme = "Dark" } });
        Assert.Throws<InvalidDataException>(() => SettingsTransferFormat.Read(extended, current));
    }

    [Fact]
    public void Earlier_native_preferences_default_to_automatic_diagrams()
    {
        var previous = JsonSerializer.Deserialize<UserPreferences>("""{"Theme":"Dark","Preview":{"Zoom":125,"ReadingMode":"Pages"}}""")!;
        Assert.Equal(new PreviewPreferences(125, "Pages", "Auto"), previous.Preview);
        Assert.True(WorkspaceSettings.ArePreferencesValid(previous));
    }

    [Theory]
    [InlineData("forest")] [InlineData("https://example.invalid/theme")] [InlineData("")]
    public void Unsupported_themes_cannot_be_rendered_or_imported_as_appearance_choices(string choice)
    {
        Assert.Throws<ArgumentException>(() => new DiagramService().Render(Source, theme: choice));
        Assert.False((WorkspaceSettings.Default with { Preview = new(DiagramTheme: choice) }).IsValid);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = SettingsTransferFormat.FormatVersion, preview = new { diagramTheme = choice } });
        Assert.Throws<InvalidDataException>(() => SettingsTransferFormat.Read(bytes, WorkspaceSettings.Default));
    }
}
