using System.IO.Compression;
using System.Text;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class DevOpsMarkdownExportTests
{
    [Theory] [InlineData("```mermaid", "```", "\n")] [InlineData("~~~~mmd", "~~~~~", "\r\n")]
    [InlineData("   ```MERMAID", "   ```", "\r\n")]
    public void Complete_diagrams_convert_with_exact_surrounding_source_line_endings_and_indent(string open, string close, string newline)
    {
        var prefix = open.StartsWith(' ') ? "   " : "";
        var source = "# Café" + newline + newline + open + newline + "flowchart LR;" + newline + "A[Keep] --> B[Source]" + newline + close + newline + newline + "**After**";
        var expected = "# Café" + newline + newline + prefix + "::: mermaid" + newline + "graph LR;" + newline + "A[Keep] --> B[Source]" + newline + prefix + ":::" + newline + newline + "**After**";
        Assert.Equal(expected, DevOpsMarkdownExporter.Convert(source, "sample.md"));
    }

    [Theory] [InlineData("sample.mmd")] [InlineData("sample.mermaid")] [InlineData("sample.txt")]
    public void Standalone_diagrams_and_text_files_keep_literal_bytes(string filename)
    {
        const string source = "```mermaid\nflowchart LR\nA --> B\n```\n";
        Assert.Same(source, DevOpsMarkdownExporter.Convert(source, filename));
    }

    [Theory] [InlineData("```mermaid\nflowchart LR\nA --> B")]
    [InlineData("::: mermaid\r\nflowchart LR\r\nA --> B")]
    [InlineData("````text\n```mermaid\nflowchart LR\n```\n````\n")]
    [InlineData("    ```mermaid\n    flowchart LR\n    ```\n")]
    public void Incomplete_diagrams_and_literal_code_examples_are_unchanged(string source)
        => Assert.Same(source, DevOpsMarkdownExporter.Convert(source, "example.markdown"));

    [Fact] public void Existing_DevOps_blocks_and_multiple_diagrams_convert_idempotently()
    {
        const string source = ":::mermaid\r\n\r\nflowchart TD\r\nA --> B\r\n:::\r\n\n```mmd\nsequenceDiagram\nA->>B: Café\n```";
        const string expected = "::: mermaid\r\n\r\ngraph TD\r\nA --> B\r\n:::\r\n\n::: mermaid\nsequenceDiagram\nA->>B: Café\n:::";
        var converted = DevOpsMarkdownExporter.Convert(source, "Example.MD"); Assert.Equal(expected, converted);
        Assert.Equal(expected, DevOpsMarkdownExporter.Convert(converted, "Example.MD"));
    }

    [Fact] public void Only_the_first_nonempty_simple_flowchart_declaration_changes()
    {
        const string source = "```mermaid\n%% Keep this comment\nflowchart LR\nA --> B\n```\n\n```mermaid\nflowchart LR; A --> B\n```\n";
        var result = DevOpsMarkdownExporter.Convert(source, "sample.md");
        Assert.Contains("%% Keep this comment\nflowchart LR", result); Assert.Contains("flowchart LR; A --> B", result);
        Assert.DoesNotContain("graph LR", result.Replace("flowchart LR", "", StringComparison.Ordinal));
    }

    [Fact] public void Cancellation_does_not_return_a_partially_converted_document()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => DevOpsMarkdownExporter.Convert("```mermaid\nflowchart LR\n```", "document.md", cancellation.Token));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Bundle_conversion_is_explicit_preserves_BOM_and_assets_and_does_not_write_source(bool enabled)
    {
        var root = Path.Combine(Path.GetTempPath(), "lens-devops-bundle-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            const string source = "# Café\r\n\r\n```mermaid\r\nflowchart LR\r\nA --> B\r\n```\r\n";
            var path = Path.Combine(root, "guide.md"); var draftPath = Path.Combine(root, "draft.markdown"); var textPath = Path.Combine(root, "plain.txt");
            var original = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(source)).ToArray();
            await File.WriteAllBytesAsync(path, original); await File.WriteAllTextAsync(draftPath, "# Saved draft source"); await File.WriteAllTextAsync(textPath, source);
            var image = new byte[] { 137, 80, 78, 71 }; await File.WriteAllBytesAsync(Path.Combine(root, "image.png"), image);
            var edited = new Dictionary<string, string> { [draftPath] = source + "\r\nUnsaved editor text" };
            var bytes = await new MarkdownBundleService().ExportAsync(new(root), edited, devopsMarkdown: enabled);
            using var stream = new MemoryStream(bytes); using var archive = new ZipArchive(stream);
            byte[] Read(string name) { using var input = archive.GetEntry(name)!.Open(); using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray(); }
            var saved = Read("guide.md"); Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, saved[..3]);
            Assert.Equal(enabled ? DevOpsMarkdownExporter.Convert(source, "guide.md") : source, Encoding.UTF8.GetString(saved[3..]));
            Assert.Equal(enabled ? DevOpsMarkdownExporter.Convert(edited[draftPath], "draft.markdown") : edited[draftPath], Encoding.UTF8.GetString(Read("draft.markdown")));
            Assert.Equal(source, Encoding.UTF8.GetString(Read("plain.txt"))); Assert.Equal(image, Read("image.png"));
            Assert.Equal(original, await File.ReadAllBytesAsync(path)); Assert.Equal("# Saved draft source", await File.ReadAllTextAsync(draftPath));
        }
        finally { Directory.Delete(root, true); }
    }
}
