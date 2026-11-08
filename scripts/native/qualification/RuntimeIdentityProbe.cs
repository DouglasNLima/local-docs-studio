// Pure console discovery: assembly loading only, before any future qualification process.
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

static object FileIdentity(string path) => new {
    path = Path.GetFullPath(path), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
    fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion
};
var assemblies = new Dictionary<string, object> { ["core"] = FileIdentity(typeof(object).Assembly.Location) };
foreach (var pair in new[] { ("framework", "PresentationFramework"), ("presentationCore", "PresentationCore"), ("windowsBase", "WindowsBase") })
    assemblies[pair.Item1] = FileIdentity(Assembly.Load(new AssemblyName(pair.Item2)).Location);
var required = new HashSet<string>(new[] { "dotnet.exe", "hostfxr.dll", "hostpolicy.dll", "coreclr.dll" }, StringComparer.OrdinalIgnoreCase);
var modules = new Dictionary<string, object>();
using (var process = Process.GetCurrentProcess())
    foreach (ProcessModule module in process.Modules)
        if (required.Contains(module.ModuleName)) modules.Add(module.ModuleName.ToLowerInvariant(), FileIdentity(module.FileName));
Console.WriteLine(JsonSerializer.Serialize(new {
    processId = Environment.ProcessId, runtimeVersion = Environment.Version.ToString(),
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), assemblies, modules,
    windowOrAppearanceApisUsed = false
}));
