using System.Diagnostics;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckLoadedRuntime(Process process, string executable, string output, bool requireBundledRuntime)
    {
        // Observe the actual application after editor and Mermaid readiness, never the validation host.
        // The installer caller explicitly requires the bundled runtime; metadata cannot opt it out.
        var expectedExecutable = Path.GetFullPath(executable);
        var packageDirectory = Path.GetDirectoryName(expectedExecutable)!;
        process.Refresh();
        var actualExecutable = process.MainModule?.FileName;
        var modules = process.Modules.Cast<ProcessModule>().ToArray();
        var required = new[] { "coreclr.dll", "wpfgfx_cor3.dll" };
        var observations = required.Select(name =>
        {
            var loaded = modules.Where(module => string.Equals(module.ModuleName, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new
            {
                Name = name,
                LoadedPaths = loaded.Select(module => module.FileName).ToArray(),
                LoadedExactlyOnce = loaded.Length == 1,
                Bundled = loaded.Length == 1 && string.Equals(Path.GetDirectoryName(Path.GetFullPath(loaded[0].FileName)),
                    packageDirectory, StringComparison.OrdinalIgnoreCase)
            };
        }).ToArray();
        var executableMatches = string.Equals(actualExecutable, expectedExecutable, StringComparison.OrdinalIgnoreCase);
        var passed = executableMatches && observations.All(item => item.LoadedExactlyOnce && (!requireBundledRuntime || item.Bundled));
        File.WriteAllText(Path.Combine(output, "native-ui-runtime.json"), JsonSerializer.Serialize(new
        {
            Passed = passed, ProcessId = process.Id, ExpectedExecutable = expectedExecutable, ActualExecutable = actualExecutable,
            ExecutableMatches = executableMatches, BundledRuntimeRequired = requireBundledRuntime, Modules = observations,
            CleanMachineCertification = false,
            Limitation = "Loaded-module snapshot after editor and diagram readiness. The hosted runner contains development tools; this does not certify SDK-free clean Windows."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!passed) throw new IOException("The application did not load the expected native runtime modules"
            + (requireBundledRuntime ? " from its installed package directory." : "."));
    }
}
