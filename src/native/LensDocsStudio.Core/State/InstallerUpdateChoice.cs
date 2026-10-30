using System.Security.Cryptography;
using System.Text.Json;

namespace LensDocsStudio.Core.State;

// A bounded, disposable instruction, never a second source of effective settings.
public sealed record InstallerUpdateChoice(int SchemaVersion, string Action, string PreferencesSha256);

public sealed partial class SessionStore
{
    public const string InstallerChoiceFileName = "installer-update-choice.json";

    public async Task<UserPreferences> InitialisePreferencesAsync(bool acceptInstallerChoice = false, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(async () =>
            {
                const string name = "preferences.json";
                var preferences = await ReadFileAsync<UserPreferences>(name, cancellationToken, WorkspaceSettings.ArePreferencesValid).ConfigureAwait(false) ?? new();
                // A recovered backup may predate a later opt-out. Retain its other settings,
                // but require a fresh decision before making automatic requests.
                var recovered = readProblems.Contains(name);
                if (recovered) preferences = preferences with { AutomaticUpdateChecks = false };
                if (!acceptInstallerChoice) return preferences;
                var path = Path.Combine(DirectoryPath, InstallerChoiceFileName);
                InstallerUpdateChoice? choice;
                string? choiceToken = null;
                try
                {
                    await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        if (file.Length > 4096) throw new JsonException("Installer update choice exceeds its size limit.");
                        choiceToken = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
                        if (preferences.AppliedInstallerChoice == choiceToken)
                        {
                            // A crash/delete failure after persistence must not replay an old
                            // opt-out over a newer in-app opt-in, even if other fields match.
                            file.Close();
                            try { File.Delete(path); }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                            { ReadWarnings.Add("An already applied installer choice could not be removed: " + ex.Message); }
                            return preferences;
                        }
                        file.Position = 0;
                        choice = await JsonSerializer.DeserializeAsync<InstallerUpdateChoice>(file, Options, cancellationToken).ConfigureAwait(false);
                    }
                    if (choice is not { SchemaVersion: 1, Action: "preserve" or "enable" or "disable", PreferencesSha256: not null }
                        || (choice.PreferencesSha256 != "missing" && (choice.PreferencesSha256.Length != 64 || !choice.PreferencesSha256.All(char.IsAsciiHexDigit))))
                        throw new JsonException("Installer update choice is incomplete or unsupported.");
                    // Consume an opt-in BEFORE writing, so interruption cannot replay it.
                    // Keep an opt-out pending until it is durable: losing it could revive true.
                    if (choice.Action != "disable") File.Delete(path);
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return preferences; }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    // Settle the safe decision durably before removing malformed evidence.
                    // If this fails, leave the blocking instruction for the next startup.
                    var disabled = preferences with { AutomaticUpdateChecks = false, AppliedInstallerChoice = choiceToken };
                    await WriteFileAsync(name, disabled, cancellationToken).ConfigureAwait(false);
                    if (ex is JsonException)
                        File.Move(path, path + ".unreadable-" + Guid.NewGuid().ToString("N") + ".json");
                    ReadWarnings.Add("Installer update choice could not be applied; automatic checks remain off. " + ex.Message);
                    return disabled;
                }
                if (choice.Action == "preserve" || recovered && choice.Action == "enable") return preferences;
                var fingerprint = await PreferencesFingerprintAsync(cancellationToken).ConfigureAwait(false);
                if (!string.Equals(choice.PreferencesSha256, fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    if (choice.Action == "disable") File.Delete(path);
                    return preferences; // A newer application/user decision always wins.
                }
                var changed = preferences with { AutomaticUpdateChecks = choice.Action == "enable", AppliedInstallerChoice = choiceToken };
                await WriteFileAsync(name, changed, cancellationToken).ConfigureAwait(false);
                if (choice.Action == "disable") File.Delete(path);
                return changed; // Never expose an opt-in until the atomic write succeeds.
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<string> PreferencesFingerprintAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var file = new FileStream(Path.Combine(DirectoryPath, "preferences.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumStateBytes) throw new IOException("Saved preferences exceed the state limit.");
            return Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return "missing"; }
    }
}
