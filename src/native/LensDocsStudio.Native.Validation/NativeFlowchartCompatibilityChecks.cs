using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeFlowchartCompatibilityChecks
{
    public static void Run(string output, string repository)
    {
        if (Directory.Exists(output)) throw new IOException("Fresh flowchart evidence required.");
        Directory.CreateDirectory(output);
        var fixture = Path.Combine(repository, "tests/fixtures/native-flowchart/spc-application-architecture.md");
        var original = File.ReadAllBytes(fixture); var markdown = File.ReadAllText(fixture);
        var checks = new List<string>();
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dark in new[] { false, true })
        {
            ThemeService.Apply(app.Resources, dark ? "Dark" : "Light");
            var parsed = new MarkdownService().Parse(markdown); var code = parsed.Syntax.Descendants<FencedCodeBlock>().Single();
            var diagrams = new DiagramService(); var result = diagrams.Render(code.Lines.ToString(), dark);
            Check(result.Success && result.FlowchartSemantics!.Nodes.Count == 9 && result.FlowchartSemantics.Edges.Count == 9, "SPC canonical graph has nine nodes and nine connections");
            Check(!result.FlowchartSemantics!.Nodes.ContainsKey("class") && result.FlowchartSemantics.ClassDefs.Count == 8
                && result.FlowchartSemantics.ClassAssignments.Count == 9, "Eight declared classes style all nine nodes without a phantom class node");
            var native = new NativeSvgService(); var trusted = native.Prepare(result.Svg!); var drawing = native.Render(result.Svg!);
            var png = NativeSvgService.ToExportPng(drawing);
            var preview = new NativeMarkdownRenderer(native).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = result }, _ => { }, (_, _) => { });
            Check(preview.Warnings.Contains(result.FidelityWarning!), "Actual WPF preview surfaces the unsupported label typography warning");
            Check(NativeSourceEditorChecks.Pixels(png).SequenceEqual(NativeSourceEditorChecks.Pixels(NativeSvgService.ToExportPng(preview.Media.Single().Image.Source))),
                "Actual document preview and canonical PNG export have identical pixels on the same native path");
            var inspection = DiagramInspection.Create(result, native)!;
            Check(inspection.Entities.Count == 9 && inspection.Connections.Count == 9 && inspection.Entities.All(item => item.Identity != "node:class"),
                "Supported native visual inspection maps exactly nine nodes and nine connections");
            Check(NativeSourceEditorChecks.Pixels(png).SequenceEqual(NativeSourceEditorChecks.Pixels(NativeSvgService.ToExportPng(inspection.Drawing))),
                "Native inspection retains the canonical drawing");
            var exported = new ExportPreparationService(new MarkdownService(), diagrams, native).Prepare(markdown, null, null, dark);
            Check(NativeSourceEditorChecks.Pixels(png).SequenceEqual(NativeSourceEditorChecks.Pixels(exported.Diagrams.Single().Value.PngBytes)),
                "The existing document export pipeline embeds the same canonical PNG");
            Check(parsed.Source == markdown && exported.Document.Source == markdown && result.Source == code.Lines.ToString(),
                "Preview and export retain the original Markdown and Mermaid source in memory");
            var name = dark ? "spc-dark" : "spc-light";
            File.WriteAllText(Path.Combine(output, name + ".svg"), trusted); File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
            Check(NativeSourceEditorChecks.Pixels(png).SequenceEqual(NativeSourceEditorChecks.Pixels(NativeSvgService.ToExportPng(
                new NativeSvgService().Render(File.ReadAllText(Path.Combine(output, name + ".svg")))))),
                "The written SVG export reopens with identical native pixels to preview and PNG export");
            File.WriteAllText(Path.Combine(output, name + "-semantics.json"), JsonSerializer.Serialize(result.FlowchartSemantics, new JsonSerializerOptions { WriteIndented = true }));
        }
        Check(File.ReadAllBytes(fixture).SequenceEqual(original), "The exact captured SPC Markdown bytes remain unchanged");
        File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new
        {
            status = "PASS", source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), renderer = "Mermaider 0.15.1", checks,
            scope = "COMPILED_WPF_PREVIEW_NATIVE_SVG_PNG_EXPORT_AND_INSPECTION; OS_HIGH_CONTRAST_AND_PHYSICAL_GUI_NOT_CERTIFIED"
        }, new JsonSerializerOptions { WriteIndented = true }));
        void Check(bool condition, string text) { if (!condition) throw new IOException(text); checks.Add(text); }
    }
}
