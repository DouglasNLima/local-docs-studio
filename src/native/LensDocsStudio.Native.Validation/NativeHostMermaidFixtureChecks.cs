using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeHostMermaidFixtureChecks
{
    public const string Invalid = "flowchart TD\n  A -->\n";
    public const string Error = "An arrow is missing its destination. Complete the connection and try again.";
    public static void Run(string output)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Fresh fixture evidence required.");
        Directory.CreateDirectory(output);
        var rows = new List<object>();
        foreach (var (name, source, accepted) in new[] { ("original-recovered", "flowchart LR\nA[unfinished\n", true), ("incomplete-arrow", Invalid, false), ("valid-companion", "flowchart LR\nA[Write] --> B[Review]\n", true) })
        {
            var path = Path.Combine(output, name + ".mmd"); File.WriteAllText(path, source, new UTF8Encoding(false));
            var hash = Digest(File.ReadAllBytes(path));
            // Direct parser/render result is recorded independently of our existing pre-parser guard.
            string? rawSvg = null, rawError = null;
            try { rawSvg = Mermaider.MermaidRenderer.RenderSvg(source); } catch (Exception error) { rawError = error.ToString(); }
            var result = new DiagramService().Render(source);
            var parsed = new MarkdownService().Parse(source, standaloneMermaid: true);
            var block = parsed.Syntax.OfType<FencedCodeBlock>().Single();
            var preview = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult> { [block] = result }, _ => { }, (_, _) => { });
            var elements = Descendants(preview.Document).OfType<DependencyObject>().ToArray();
            var errors = elements.Where(e => AutomationProperties.GetAutomationId(e).StartsWith("DiagramError", StringComparison.Ordinal) && e is Label).Select(e => new { id = AutomationProperties.GetAutomationId(e), name = AutomationProperties.GetName(e) }).ToArray();
            var images = elements.OfType<Image>().Where(e => AutomationProperties.GetAutomationId(e).StartsWith("DiagramPreview", StringComparison.Ordinal)).ToArray();
            var geometry = result.Svg is null ? 0 : XDocument.Parse(result.Svg).Descendants().Count(e => e.Name.LocalName is "path" or "rect" or "circle" or "polygon" or "line");
            rows.Add(new { name, source, sha256 = hash, parserBoundary = name == "incomplete-arrow" ? "DiagramService arrow guard before Mermaider parser" : "Mermaider 0.15.1 parser/render", directMermaider = new { accepted = rawSvg is not null, error = rawError, svgSha256 = rawSvg is null ? null : Digest(Encoding.UTF8.GetBytes(rawSvg)) }, ordinaryPath = new { result.Success, result.Error, geometry, errors, images = images.Length }, sourceExact = result.Source == source && parsed.Source == source && Digest(File.ReadAllBytes(path)) == hash });
            File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(rows[^1], new JsonSerializerOptions { WriteIndented = true }));
            if (result.Success != accepted || result.Source != source || parsed.Source != source || Digest(File.ReadAllBytes(path)) != hash) throw new IOException("Fixture outcome/source differs: " + name);
            if (accepted ? result.Error is not null || geometry == 0 || images.Length != 1 || errors.Length != 0 : result.Svg is not null || result.Error != Error || geometry != 0 || images.Length != 0 || errors.Length != 1 || errors[0].name != Error)
                throw new IOException("Native fallback/positive geometry contract differs: " + name);
        }
        File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new { atUtc = DateTimeOffset.UtcNow, status = "PASS", scope = "FRAMEWORK_DEPENDENT_RENDERER_AND_NATIVE_CONTROL_TEST; NOT PORTABLE GUI", source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, coreSha256 = Digest(File.ReadAllBytes(typeof(DiagramService).Assembly.Location)), nativeSha256 = Digest(File.ReadAllBytes(typeof(NativeMarkdownRenderer).Assembly.Location)), renderer = typeof(Mermaider.MermaidRenderer).Assembly.FullName, rendererSha256 = Digest(File.ReadAllBytes(typeof(Mermaider.MermaidRenderer).Assembly.Location)), runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, wpf = typeof(Window).Assembly.GetName().Version?.ToString(), rows }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Descendants(child)) yield return item;
    }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
