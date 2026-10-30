using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Updates;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.ViewModels;

public partial class UpdatesViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceViewModel workspace;
    private readonly UpdateTransport? transport;
    private readonly UpdateService service;
    private readonly Func<DownloadedUpdate, UpdateEnvironment, Task> install;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private CancellationTokenSource? operation;
    private int pendingSettingsWrites;
    private bool settingsPersistenceFailed;
    public Task SettingsSave { get; private set; } = Task.CompletedTask;
    [ObservableProperty] private bool automaticChecks;
    [ObservableProperty] private string channel;
    [ObservableProperty] private string status = "Updates are checked through public GitHub Releases. Local documents are never sent.";
    [ObservableProperty] private double progress;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private UpdateCandidate? candidate;
    [ObservableProperty] private DownloadedUpdate? downloaded;
    public string[] Channels { get; } = ["Stable", "Preview"];
    public string CurrentVersion { get; } = typeof(UpdatesViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0-preview.1";
    public string ReleaseNotes => Candidate?.Release.Notes ?? "No compatible update has been selected.";
    public string AvailableVersion => Candidate is null ? "No update selected" : "Version " + Candidate.Manifest.Version;
    public string TrustSummary => Candidate is { Trusted: true } ? "Signed release metadata verified. The installer must also pass Windows publisher verification." : "Internal preview: production signing identities are not configured. Available releases can be reviewed in your browser.";
    public bool CanDownload => !IsBusy && Candidate is { Trusted: true };
    public bool CanInstall => !IsBusy && Downloaded is not null;
    public bool CanOpenRelease => !IsBusy && Candidate is not null;
    public bool CanCheck => !IsBusy;
    public event Action<string>? UpdateAvailable;
    public UpdateEnvironment Environment => new(CurrentVersion, Channel, RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), System.Environment.OSVersion.Version);

    public UpdatesViewModel(WorkspaceViewModel workspace, Func<DownloadedUpdate, UpdateEnvironment, Task> install, UpdateService? updateService = null)
    {
        this.workspace = workspace; this.install = install;
        automaticChecks = workspace.Preferences.AutomaticUpdateChecks; channel = workspace.Preferences.UpdateChannel is "Preview" ? "Preview" : "Stable";
        // An injected service remains owned by its composer; the default uses embedded production trust.
        if (updateService is null)
        {
            transport = new();
            service = new(transport, new(UpdateTrustConfiguration.Embedded()), new AuthenticodeVerifier());
        }
        else service = updateService;
        timer.Tick += async (_, _) => { timer.Interval = TimeSpan.FromHours(1); await CheckAutomaticallyAsync(); };
        if (workspace.PreferencesInitialised) timer.Start();
    }
    partial void OnAutomaticChecksChanged(bool value) => SettingsSave = SaveSettingsAsync();
    partial void OnChannelChanged(string value)
    {
        // Changing away and back still abandons the old operation and its selection.
        operation?.Cancel(); Candidate = null; Downloaded = null; SettingsSave = SaveSettingsAsync();
    }
    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    partial void OnCandidateChanged(UpdateCandidate? value)
    {
        OnPropertyChanged(nameof(ReleaseNotes)); OnPropertyChanged(nameof(AvailableVersion)); OnPropertyChanged(nameof(TrustSummary)); RefreshCommands();
    }
    partial void OnDownloadedChanged(DownloadedUpdate? value) => RefreshCommands();
    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(CanInstall)); OnPropertyChanged(nameof(CanOpenRelease)); OnPropertyChanged(nameof(CanCheck));
        CheckNowCommand.NotifyCanExecuteChanged(); DownloadCommand.NotifyCanExecuteChanged(); InstallCommand.NotifyCanExecuteChanged();
        IgnoreCommand.NotifyCanExecuteChanged(); OpenReleaseCommand.NotifyCanExecuteChanged();
    }
    private async Task SaveSettingsAsync()
    {
        var enabled = AutomaticChecks; var selectedChannel = Channel;
        pendingSettingsWrites++;
        try { await workspace.UpdatePreferencesAsync(p => p with { AutomaticUpdateChecks = enabled, UpdateChannel = selectedChannel }); settingsPersistenceFailed = false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { settingsPersistenceFailed = true; Status = "Update preferences could not be saved; automatic checks are paused: " + ex.Message; }
        finally { pendingSettingsWrites--; }
    }
    public Task CheckAutomaticallyAsync() => CheckAsync(false);
    [RelayCommand(CanExecute = nameof(CanCheck))] private Task CheckNowAsync() => CheckAsync(true);
    private async Task CheckAsync(bool manual)
    {
        if (IsBusy || !workspace.PreferencesInitialised || (!manual && (settingsPersistenceFailed || pendingSettingsWrites != 0 || !AutomaticChecks
            || !workspace.Preferences.AutomaticUpdateChecks || !UpdatePolicy.IsCheckDue(DateTimeOffset.UtcNow, workspace.Preferences.LastUpdateCheck)))) return;
        await RunAsync(async token =>
        {
            Status = "Checking for compatible native updates…";
            var checkEnvironment = Environment;
            // Record the attempt even on network failure; automatic retries must not poll aggressively.
            await workspace.UpdatePreferencesAsync(p => p with { LastUpdateCheck = DateTimeOffset.UtcNow });
            var result = await service.CheckAsync(checkEnvironment, token);
            token.ThrowIfCancellationRequested();
            if (Channel != checkEnvironment.Channel) return;
            Candidate = result; Downloaded = null;
            Status = result is null ? "No newer compatible native release was found." : result.Trusted ? "A verified native update is available." : "A compatible internal release is available for review. In-app installation requires configured signing identities.";
            if (result is not null && (manual || workspace.Preferences.IgnoredVersion != result.Manifest.Version)) UpdateAvailable?.Invoke("Native update " + result.Manifest.Version + " is available. Open Help > Updates to review it.");
        });
    }
    [RelayCommand(CanExecute = nameof(CanDownload))] private Task DownloadAsync() => RunAsync(async token =>
    {
        var selected = Candidate; var environment = Environment;
        if (selected is null) return;
        Status = "Downloading the selected update…"; Progress = 0;
        var downloaded = await service.DownloadAsync(selected, environment, Path.Combine(workspace.StateDirectory, "update-cache"), new Progress<double>(value => Progress = value * 100), token);
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Candidate, selected) || Environment != environment) throw new OperationCanceledException(token);
        Downloaded = downloaded;
        Status = "Download and publisher verified. Choose Install when you are ready to close this workspace.";
    });
    [RelayCommand(CanExecute = nameof(CanInstall))] private Task InstallAsync() => RunAsync(async token =>
    {
        var selected = Downloaded; var environment = Environment;
        if (selected is null) return;
        await service.VerifyAsync(selected, environment, token);
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Downloaded, selected) || Environment != environment) throw new OperationCanceledException(token);
        await install(selected, environment);
    });
    [RelayCommand(CanExecute = nameof(CanOpenRelease))] private async Task IgnoreAsync()
    {
        if (Candidate is null) return;
        await RunAsync(async _ => { await workspace.UpdatePreferencesAsync(p => p with { IgnoredVersion = Candidate.Manifest.Version }); Status = "This version will not trigger automatic notifications. Manual checks still show it."; });
    }
    [RelayCommand(CanExecute = nameof(CanOpenRelease))] private void OpenRelease()
    {
        if (Candidate is not null) Process.Start(new ProcessStartInfo(UpdatePolicy.ReleasePage(Candidate.Manifest.ReleaseTag).AbsoluteUri) { UseShellExecute = true });
    }
    [RelayCommand] private void Cancel() => operation?.Cancel();
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; operation = new();
        try { await action(operation.Token); }
        catch (OperationCanceledException) { Status = "Update operation cancelled; the current application and documents are unchanged."; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Status = ex.Message; }
        finally { operation.Dispose(); operation = null; IsBusy = false; }
    }
    public void Dispose() { timer.Stop(); operation?.Cancel(); transport?.Dispose(); }
}
