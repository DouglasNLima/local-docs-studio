using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

internal static class RuntimeIdentity
{
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void Require(bool value) { if (!value) throw new InvalidOperationException("Exact pre-GET identity differs."); }
    internal static object Verify(string phase)
    {
        Require(phase is "BEFORE_GETS" or "AFTER_GETS");
        var path = Environment.GetEnvironmentVariable("GOAL77_IDENTITY_PATH") ?? throw new IOException("Identity record absent.");
        Require(Hash(path) == Environment.GetEnvironmentVariable("GOAL77_IDENTITY_SHA256"));
        using var json = JsonDocument.Parse(File.ReadAllBytes(path)); var expected = json.RootElement;
        var root = Path.GetFullPath(expected.GetProperty("runtimeRoot").GetString()!);
        var files = expected.GetProperty("runtimeFiles").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var process = Environment.ProcessPath!; var assembly = Assembly.GetExecutingAssembly().Location;
        var revision = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var core = typeof(object).Assembly.Location;
        var observed = new Dictionary<string, object>();
        foreach (var pair in new[] { ("process", process), ("assembly", assembly), ("core", core) })
        {
            var entry = expected.GetProperty(pair.Item1);
            Require(string.Equals(Path.GetFullPath(pair.Item2), Path.GetFullPath(entry.GetProperty("path").GetString()!), StringComparison.OrdinalIgnoreCase));
            var digest = Hash(pair.Item2); Require(digest == entry.GetProperty("sha256").GetString());
            observed[pair.Item1] = new { path = pair.Item2, sha256 = digest };
        }
        Require(Environment.Version.ToString() == "10.0.14" && RuntimeInformation.ProcessArchitecture == Architecture.X64);
        Require(revision == expected.GetProperty("sourceRevision").GetString());
        var names = files.Keys.Select(p => Path.GetFileName(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modules = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        using var current = Process.GetCurrentProcess();
        foreach (ProcessModule module in current.Modules)
        {
            if (!names.Contains(module.ModuleName)) continue;
            var relative = Path.GetRelativePath(root, module.FileName).Replace('\\', '/');
            Require(files.TryGetValue(relative, out var expectedHash));
            var digest = Hash(module.FileName); Require(digest == expectedHash);
            Require(!modules.ContainsKey(module.ModuleName.ToLowerInvariant()));
            modules[module.ModuleName.ToLowerInvariant()] = new { path = module.FileName, sha256 = digest, relative };
        }
        Require(new[] { "hostfxr.dll", "hostpolicy.dll", "coreclr.dll" }.All(modules.ContainsKey));
        return new { processId = Environment.ProcessId, architecture = "X64", runtimeVersion = Environment.Version.ToString(),
            sourceRevision = revision, files = observed, modules, phase };
    }
}
