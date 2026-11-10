using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace LensDocsStudio.Native.UxProbe;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) => args.FirstOrDefault() == "--pages-transition"
        ? PagesTransitionControl.Run(args) : LayoutControl.Run(args);
    internal static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    internal static void Write(string directory, string name, object value) => File.WriteAllText(Path.Combine(directory, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
}
