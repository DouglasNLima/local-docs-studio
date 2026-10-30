using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Updates;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Platform;

public sealed record UpdateLaunchTicket(int SchemaVersion, int ParentId, long ParentStartTicks, string OriginalExecutable, string StateDirectory,
    DownloadedUpdate Update, string Channel);

/// <summary>A temporary copy of the native executable waits for graceful shutdown before launching a verified installer.</summary>
public static class NativeUpdateLauncher
{
    public static async Task PrepareAsync(DownloadedUpdate update, UpdateEnvironment environment, string stateDirectory, CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"))) throw new IOException("In-app installation requires the installed self-contained native edition.");
        var parent = Process.GetCurrentProcess();
        var root = Path.Combine(stateDirectory, "update-launchers", Guid.NewGuid().ToString("N"));
        WorkspacePath.RejectReparsePoints(root); Directory.CreateDirectory(root);
        await Task.Run(() =>
        {
            long copied = 0;
            foreach (var source in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested(); WorkspacePath.RejectReparsePoints(source);
                copied += new FileInfo(source).Length;
                if (copied > 1024L * 1024 * 1024) throw new IOException("The update launcher exceeds its copy limit.");
                var target = Path.Combine(root, Path.GetRelativePath(AppContext.BaseDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, false);
            }
        }, cancellationToken);
        var ticketPath = Path.Combine(root, "update-ticket.json");
        var executable = parent.MainModule?.FileName ?? throw new IOException("The running native executable could not be located.");
        var ticket = new UpdateLaunchTicket(1, parent.Id, parent.StartTime.ToUniversalTime().Ticks, executable, stateDirectory, update, environment.Channel);
        await File.WriteAllBytesAsync(ticketPath, JsonSerializer.SerializeToUtf8Bytes(ticket, UpdatePolicy.Json), cancellationToken);
        var start = new ProcessStartInfo(Path.Combine(root, "LensDocsStudio.Native.exe")) { UseShellExecute = false };
        start.ArgumentList.Add("--apply-native-update"); start.ArgumentList.Add(ticketPath);
        using var helper = Process.Start(start) ?? throw new IOException("The update launcher could not start.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (!File.Exists(ticketPath + ".ready"))
            {
                if (helper.HasExited) throw new IOException("The update launcher could not prepare safely.");
                await Task.Delay(100, timeout.Token);
            }
        }
        catch { if (!helper.HasExited) helper.Kill(); throw; }
    }

    public static async Task ApplyAsync(string ticketPath)
    {
        WorkspacePath.RejectReparsePoints(ticketPath);
        if (new FileInfo(ticketPath).Length > 1024 * 1024) throw new IOException("The update ticket exceeds its limit.");
        var ticket = JsonSerializer.Deserialize<UpdateLaunchTicket>(await File.ReadAllBytesAsync(ticketPath), UpdatePolicy.Json)
            ?? throw new IOException("The update ticket is invalid.");
        if (ticket.SchemaVersion != 1 || ticket.Channel is not ("Stable" or "Preview") || Path.GetFileName(ticket.OriginalExecutable) != "LensDocsStudio.Native.exe") throw new IOException("The update ticket is incompatible.");
        var currentVersion = typeof(NativeUpdateLauncher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var environment = new UpdateEnvironment(currentVersion, ticket.Channel, RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), System.Environment.OSVersion.Version);
        using var transport = new UpdateTransport();
        var service = new UpdateService(transport, new(UpdateTrustConfiguration.Embedded()), new AuthenticodeVerifier());
        using var parent = Process.GetProcessById(ticket.ParentId);
        if (parent.StartTime.ToUniversalTime().Ticks != ticket.ParentStartTicks || !string.Equals(parent.MainModule?.FileName, ticket.OriginalExecutable, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The update parent does not match the authorised native application.");
        WorkspacePath.RejectReparsePoints(ticket.StateDirectory);
        await File.WriteAllTextAsync(ticketPath + ".ready", "Ready to wait for graceful shutdown.");
        using var wait = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await parent.WaitForExitAsync(wait.Token);
        var succeeded = false; var message = "The update did not start.";
        try
        {
            // Hold the installer against modification through final verification and process creation.
            await using var held = new FileStream(ticket.Update.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            await service.VerifyAsync(ticket.Update, environment, wait.Token);
            using var installer = Process.Start(new ProcessStartInfo(ticket.Update.Path) { UseShellExecute = false }) ?? throw new IOException("The verified installer could not start.");
            await installer.WaitForExitAsync();
            succeeded = installer.ExitCode == 0;
            message = succeeded ? "The native installer completed. Your previous session was retained." : $"The installer returned {installer.ExitCode}. Your workspace and recovery data were retained.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { message = "The update was not applied: " + ex.Message; }
        await new SessionStore(ticket.StateDirectory).WriteAsync("update-result.json", new { Succeeded = succeeded, Message = message, RecordedAt = DateTimeOffset.UtcNow });
        if (File.Exists(ticket.OriginalExecutable))
        {
            WorkspacePath.RejectReparsePoints(ticket.OriginalExecutable);
            var restart = new ProcessStartInfo(ticket.OriginalExecutable) { UseShellExecute = false };
            foreach (var argument in RestartStateArguments(ticket.StateDirectory)) restart.ArgumentList.Add(argument);
            Process.Start(restart);
        }
    }

    public static string[] RestartStateArguments(string stateDirectory)
    {
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensDocsStudio.Native");
        return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(stateDirectory)), Path.TrimEndingDirectorySeparator(standard), StringComparison.OrdinalIgnoreCase)
            ? [] : ["--state-directory", stateDirectory];
    }
}
