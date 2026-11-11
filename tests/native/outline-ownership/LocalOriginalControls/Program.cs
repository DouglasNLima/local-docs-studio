global using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

// Explicit, bounded local invocation of the unchanged assertions. No hosted flags,
// display/taskbar transition, installation or OS theme mutation is permitted here.
if (args.Length != 5 || args[0] != "--owner-authorised-local")
    throw new ArgumentException("Require explicit local authorisation, mode, executable, fresh output and repository root.");
var mode = args[1];
if (mode is not ("caret" or "viewports" or "viewports-local-dpi" or "outline-pointer")) throw new ArgumentException("Unsupported local control.");
var executable = Path.GetFullPath(args[2]); var output = Path.GetFullPath(args[3]); var root = Path.GetFullPath(args[4]);
if (Directory.Exists(output)) throw new IOException("Preserve previous results: use a fresh directory.");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "local-invocation.json"), JsonSerializer.Serialize(new {
    StartedUtc = DateTime.UtcNow, Mode = mode, Executable = executable, ActualHighContrast = SystemParameters.HighContrast,
    DisplayMutation = false, HostedEnvironmentSpoofed = false, OriginalAssertionsUnchanged = true
}, new JsonSerializerOptions { WriteIndented = true }));
if (SystemParameters.HighContrast) throw new InvalidOperationException("This invocation is the normal appearance comparison; High Contrast is separate.");
var assembly = Assembly.Load("LensDocsStudio.Native.Validation");
var type = assembly.GetType("LensDocsStudio.Native.Validation.NativeUiChecks", true)!;
try
{
    if (mode == "outline-pointer") return await Task.Run(() => PointerControl.Run(executable, output));
    if (mode == "viewports-local-dpi")
    {
        await Task.Run(() => LensDocsStudio.Native.Validation.LocalViewportChecks.CheckWorkspaceViewportOwnership(executable, output, true));
        return 0;
    }
    // The public viewport wrapper changes display settings; call its unchanged
    // assertion body directly, retaining the existing local display configuration.
    if (mode == "viewports")
    {
        await Task.Run(() => type.GetMethod("CheckWorkspaceViewportOwnership", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [executable, output, true]));
        return 0;
    }
    return (int)type.GetMethod("RunDesktopUxInteractions", BindingFlags.Static | BindingFlags.Public)!
        .Invoke(null, [executable, output, root, false])!;
}
catch (Exception error)
{
    File.WriteAllText(Path.Combine(output, "local-failure.txt"), error.ToString());
    Console.Error.WriteLine(error); return 1;
}
