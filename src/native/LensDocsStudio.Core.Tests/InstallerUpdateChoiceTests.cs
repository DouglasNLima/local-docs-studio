using System.Text.Json;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class InstallerUpdateChoiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-installer-choice-" + Guid.NewGuid().ToString("N"));
    private string Preferences => Path.Combine(root, "preferences.json");
    private string Intent => Path.Combine(root, SessionStore.InstallerChoiceFileName);
    public InstallerUpdateChoiceTests() => Directory.CreateDirectory(root);
    private async Task ChoiceAsync(string action, string? fingerprint = null) => await File.WriteAllTextAsync(Intent,
        JsonSerializer.Serialize(new InstallerUpdateChoice(1, action, fingerprint ?? await new SessionStore(root).PreferencesFingerprintAsync())));

    [Fact]
    public async Task Fresh_and_portable_execution_default_to_no_automatic_requests()
    {
        Assert.False(new UserPreferences().AutomaticUpdateChecks);
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync()).AutomaticUpdateChecks);
        await File.WriteAllTextAsync(Preferences, "{\"Theme\":\"Dark\"}");
        var restored = await new SessionStore(root).InitialisePreferencesAsync();
        Assert.Equal("Dark", restored.Theme);
        Assert.False(restored.AutomaticUpdateChecks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Silent_upgrade_and_reinstall_preserve_a_valid_existing_choice(bool enabled)
    {
        await new SessionStore(root).WriteAsync("preferences.json", new UserPreferences(Theme: "Dark", AutomaticUpdateChecks: enabled));
        var original = await File.ReadAllBytesAsync(Preferences);
        await ChoiceAsync("preserve");
        var result = await new SessionStore(root).InitialisePreferencesAsync(true);
        Assert.Equal(enabled, result.AutomaticUpdateChecks);
        Assert.Equal(original, await File.ReadAllBytesAsync(Preferences));
        Assert.False(File.Exists(Intent));
    }

    [Theory]
    [InlineData("enable", true)]
    [InlineData("disable", false)]
    public async Task Explicit_choice_merges_atomically_preserves_other_state_and_is_consumed_once(string action, bool enabled)
    {
        var preferences = new UserPreferences(Theme: "Dark", Layout: "Editor", AutomaticUpdateChecks: !enabled,
            UpdateChannel: "Preview", LastUpdateCheck: DateTimeOffset.UtcNow, IgnoredVersion: "4.0.0",
            RecentDocuments: [Path.Combine(root, "source.md")], RecentWorkspaces: [root], Editor: new(FontSize: 20), ShowOutline: false);
        var store = new SessionStore(root);
        await store.WriteAsync("preferences.json", preferences);
        var original = await File.ReadAllBytesAsync(Preferences);
        var recovery = new Dictionary<string, byte[]> { ["session.json"] = "private draft"u8.ToArray(), ["draft.tmp"] = "recovery"u8.ToArray(), ["source.md"] = "# Keep"u8.ToArray() };
        foreach (var pair in recovery) await File.WriteAllBytesAsync(Path.Combine(root, pair.Key), pair.Value);
        await ChoiceAsync(action);
        var result = await store.InitialisePreferencesAsync(true);
        Assert.NotNull(result.AppliedInstallerChoice);
        Assert.Equal(JsonSerializer.Serialize(preferences with { AutomaticUpdateChecks = enabled, AppliedInstallerChoice = result.AppliedInstallerChoice }), JsonSerializer.Serialize(result));
        Assert.Equal(original, await File.ReadAllBytesAsync(Preferences + ".bak"));
        Assert.False(File.Exists(Intent));
        foreach (var pair in recovery) Assert.Equal(pair.Value, await File.ReadAllBytesAsync(Path.Combine(root, pair.Key)));
        // A later in-app decision survives another startup with no replayable instruction.
        await store.WriteAsync("preferences.json", result with { AutomaticUpdateChecks = !enabled });
        Assert.Equal(!enabled, (await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
    }

    [Theory]
    [InlineData("enable", false)]
    [InlineData("disable", true)]
    public async Task A_newer_user_decision_wins_over_a_pending_installer_choice(string action, bool enabled)
    {
        var store = new SessionStore(root);
        await store.WriteAsync("preferences.json", new UserPreferences(AutomaticUpdateChecks: enabled));
        await ChoiceAsync(action);
        await store.WriteAsync("preferences.json", new UserPreferences(Theme: "Dark", AutomaticUpdateChecks: enabled));
        var current = await File.ReadAllBytesAsync(Preferences);
        var restored = await store.InitialisePreferencesAsync(true);
        Assert.Equal(enabled, restored.AutomaticUpdateChecks);
        Assert.Equal("Dark", restored.Theme);
        Assert.Equal(current, await File.ReadAllBytesAsync(Preferences));
        Assert.False(File.Exists(Intent));
    }

    [Fact]
    public async Task Fresh_opt_in_is_durable_before_becoming_effective()
    {
        await ChoiceAsync("enable");
        Assert.True((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        Assert.True((await new SessionStore(root).InitialisePreferencesAsync()).AutomaticUpdateChecks);
        Assert.False(File.Exists(Intent));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":1,\"Action\":\"enable\",\"PreferencesSha256\":\"unreadable\"}")]
    [InlineData("{\"SchemaVersion\":2,\"Action\":\"enable\",\"PreferencesSha256\":\"missing\"}")]
    [InlineData("unfinished")]
    public async Task Invalid_instruction_cannot_authorise_network_and_is_retained(string text)
    {
        await new SessionStore(root).WriteAsync("preferences.json", new UserPreferences(AutomaticUpdateChecks: true));
        var original = await File.ReadAllBytesAsync(Preferences);
        await File.WriteAllTextAsync(Intent, text);
        var store = new SessionStore(root);
        Assert.False((await store.InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        Assert.Single(store.ReadWarnings);
        Assert.False(File.Exists(Intent));
        Assert.Equal(text, await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(root, SessionStore.InstallerChoiceFileName + ".unreadable-*.json"))));
        Assert.Equal(original, await File.ReadAllBytesAsync(Preferences + ".bak"));
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync()).AutomaticUpdateChecks);
    }

    [Fact]
    public async Task Oversized_or_unreadable_instruction_cannot_enable_checks()
    {
        await File.WriteAllTextAsync(Intent, new string('x', 4097));
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        await ChoiceAsync("enable");
        using var held = new FileStream(Intent, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
    }

    [Fact]
    public async Task Malformed_primary_cannot_revive_a_backup_opt_in_even_with_an_installer_opt_in()
    {
        var store = new SessionStore(root);
        await store.WriteAsync("preferences.json", new UserPreferences(Theme: "Dark", AutomaticUpdateChecks: true));
        await store.WriteAsync("preferences.json", new UserPreferences());
        await File.WriteAllTextAsync(Preferences, "interrupted");
        await ChoiceAsync("enable");
        var restored = await store.InitialisePreferencesAsync(true);
        Assert.False(restored.AutomaticUpdateChecks);
        Assert.Equal("Dark", restored.Theme);
        Assert.Equal("interrupted", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(root, "preferences.json.unreadable-*.json"))));
    }

    [Fact]
    public async Task Denied_preferences_cannot_be_replaced_by_a_pending_opt_in()
    {
        await File.WriteAllTextAsync(Preferences, "{\"AutomaticUpdateChecks\":false}");
        await ChoiceAsync("enable");
        var store = new SessionStore(root);
        using (var held = new FileStream(Preferences, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => store.InitialisePreferencesAsync(true));
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("preferences.json", new UserPreferences(AutomaticUpdateChecks: true)));
        Assert.True(File.Exists(Intent));
        Assert.Equal("{\"AutomaticUpdateChecks\":false}", await File.ReadAllTextAsync(Preferences));
    }

    [Fact]
    public async Task Atomic_write_failure_preserves_both_versions_and_cannot_replay_an_opt_in()
    {
        const string compact = "{\"AutomaticUpdateChecks\":false}";
        await File.WriteAllTextAsync(Preferences, compact);
        await File.WriteAllTextAsync(Preferences + ".bak", compact);
        await ChoiceAsync("enable");
        await Assert.ThrowsAsync<IOException>(() => new SessionStore(root, 64).InitialisePreferencesAsync(true));
        Assert.Equal(compact, await File.ReadAllTextAsync(Preferences));
        Assert.Equal(compact, await File.ReadAllTextAsync(Preferences + ".bak"));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        Assert.False(File.Exists(Intent));
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
    }

    [Fact]
    public async Task Failed_opt_out_stays_pending_across_restart_until_it_is_durable()
    {
        const string compact = "{\"AutomaticUpdateChecks\":true}";
        await File.WriteAllTextAsync(Preferences, compact);
        await ChoiceAsync("disable");
        await Assert.ThrowsAsync<IOException>(() => new SessionStore(root, 64).InitialisePreferencesAsync(true));
        Assert.True(File.Exists(Intent));
        Assert.Equal(compact, await File.ReadAllTextAsync(Preferences));
        // A new process retries the opt-out before it can initialise an updater.
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        Assert.False(File.Exists(Intent));
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
    }

    [Fact]
    public async Task Invalid_instruction_remains_blocking_if_persisting_the_safe_decision_fails()
    {
        const string compact = "{\"AutomaticUpdateChecks\":true}";
        await File.WriteAllTextAsync(Preferences, compact);
        await File.WriteAllTextAsync(Intent, "unfinished");
        await Assert.ThrowsAsync<IOException>(() => new SessionStore(root, 64).InitialisePreferencesAsync(true));
        Assert.Equal("unfinished", await File.ReadAllTextAsync(Intent));
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
    }

    [Fact]
    public async Task Acknowledgement_prevents_replay_after_a_crash_and_a_later_identical_user_choice()
    {
        var store = new SessionStore(root);
        await store.WriteAsync("preferences.json", new UserPreferences(AutomaticUpdateChecks: true));
        await ChoiceAsync("disable");
        var instruction = await File.ReadAllBytesAsync(Intent);
        var disabled = await store.InitialisePreferencesAsync(true);
        // Simulate interruption after the atomic preference replacement but before
        // removing the instruction; all other settings remain exactly the same.
        await File.WriteAllBytesAsync(Intent, instruction);
        await store.WriteAsync("preferences.json", disabled with { AutomaticUpdateChecks = true });
        Assert.True((await new SessionStore(root).InitialisePreferencesAsync(true)).AutomaticUpdateChecks);
        Assert.False(File.Exists(Intent));
    }

    [Fact]
    public async Task State_directory_validation_does_not_consume_an_installer_instruction()
    {
        await ChoiceAsync("enable");
        Assert.False((await new SessionStore(root).InitialisePreferencesAsync()).AutomaticUpdateChecks);
        Assert.True(File.Exists(Intent));
        Assert.False(File.Exists(Preferences));
    }

    public void Dispose() => Directory.Delete(root, true);
}
