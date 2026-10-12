using System.Text;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class SettingsTransferTests
{
    [Fact] public void Native_appearance_round_trip_has_no_document_paths_recovery_or_update_consent()
    {
        var preferences = new UserPreferences(Theme: "Dark", Layout: "Editor", AutomaticUpdateChecks: false, UpdateChannel: "Preview",
            RecentDocuments: ["C:\\Private\\draft.md"], Editor: new("Consolas", 18, false, false, 2, true), Preview: new(125), ShowOutline: false);
        var settings = WorkspaceSettings.From(preferences); var bytes = SettingsTransferFormat.Write(settings);
        Assert.Equal(settings, SettingsTransferFormat.Read(bytes, WorkspaceSettings.Default));
        var json = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("Private", json); Assert.DoesNotContain("automaticUpdate", json); Assert.DoesNotContain("updateChannel", json);
        var merged = SettingsTransferFormat.Read(bytes, WorkspaceSettings.Default).ApplyTo(preferences);
        Assert.False(merged.AutomaticUpdateChecks); Assert.Equal("Preview", merged.UpdateChannel); Assert.Equal(preferences.RecentDocuments, merged.RecentDocuments);
    }
    [Fact] public void Browser_choices_preserve_native_editor_and_preview_preferences()
    {
        var current = WorkspaceSettings.Default with { Editor = new(FontSize: 23), Preview = new(145) };
        var bytes = Encoding.UTF8.GetBytes("""{"formatVersion":"lens-docs-studio-settings-1.0","theme":"Dark","layout":"Preview","showOutline":false}""");
        var imported = SettingsTransferFormat.Read(bytes, current);
        Assert.Equal("Dark", imported.Theme); Assert.Equal("Preview", imported.Layout); Assert.False(imported.ShowOutline);
        Assert.Equal(current.Editor, imported.Editor); Assert.Equal(current.Preview, imported.Preview);
        Assert.Equal(imported, SettingsTransferFormat.Read(new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray(), current));
    }
    [Theory]
    [InlineData("\"editor\":{}")]
    [InlineData("\"preview\":{}")]
    [InlineData("\"theme\":\"Unknown\"")]
    [InlineData("\"theme\":null")]
    [InlineData("\"theme\":\"Light\",\"theme\":\"Dark\"")]
    [InlineData("\"theme\":\"Light\",\"Theme\":\"Dark\"")]
    [InlineData("\"layout\":false")]
    [InlineData("\"showOutline\":\"false\"")]
    [InlineData("\"preview\":{\"zoom\":201}")]
    [InlineData("\"editor\":{\"fontFamily\":\"https://example.invalid/font.ttf\"}")]
    [InlineData("\"editor\":{\"fontSize\":18,\"fontSize\":25}")]
    [InlineData("\"automaticUpdateChecks\":true")]
    [InlineData("\"recentDocuments\":[\"C:\\\\Private\\\\draft.md\"]")]
    public void Unsupported_or_ambiguous_values_are_rejected_before_settings_change(string fields)
        => Assert.Throws<InvalidDataException>(() => SettingsTransferFormat.Read(Encoding.UTF8.GetBytes("{\"formatVersion\":\"lens-docs-studio-settings-1.0\"," + fields + "}"), WorkspaceSettings.Default));
    [Theory]
    [InlineData("[]")] [InlineData("null")] [InlineData("{")]
    [InlineData("{\"formatVersion\":\"lens-docs-studio-settings-1.0\"}")]
    [InlineData("{\"formatVersion\":\"lens-docs-studio-settings-2.0\",\"theme\":\"Dark\"}")]
    public void Wrong_or_empty_formats_leave_current_settings_untouched(string json)
        => Assert.Throws<InvalidDataException>(() => SettingsTransferFormat.Read(Encoding.UTF8.GetBytes(json), WorkspaceSettings.Default));
    [Fact] public void Partial_native_choices_preserve_other_editable_settings()
    {
        var current = WorkspaceSettings.Default with { Editor = new("Consolas", 21, false, false, 3, true), Preview = new(150) };
        var bytes = Encoding.UTF8.GetBytes("""{"formatVersion":"lens-docs-studio-settings-1.0","editor":{"fontSize":18}}""");
        Assert.Equal(current with { Editor = current.Editor with { FontSize = 18 } }, SettingsTransferFormat.Read(bytes, current));
    }
    [Fact] public void Oversized_and_invalid_utf8_settings_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => SettingsTransferFormat.Read(new byte[SettingsTransferFormat.MaximumBytes + 1], WorkspaceSettings.Default));
        Assert.Throws<InvalidDataException>(() => SettingsTransferFormat.Read(new byte[] { 0xff, 0xfe, 0x7b }, WorkspaceSettings.Default));
        Assert.Throws<ArgumentException>(() => SettingsTransferFormat.Write(WorkspaceSettings.Default with { Preview = new(900) }));
    }
}
