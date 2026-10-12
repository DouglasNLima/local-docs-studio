using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Exports;

/// <summary>Limits expanded resources before serialisation, including every repeated occurrence.</summary>
public sealed record ExportAllocationLimits(long MaximumImageCharacters = 32 * 1024 * 1024,
    int MaximumHtmlCharacters = 32 * 1024 * 1024, long MaximumOutputBytes = 128 * 1024 * 1024,
    int MaximumImageOccurrences = 4096)
{
    public static ExportAllocationLimits Default { get; } = new();
    public static ExportAllocationLimits Clipboard { get; } = new(8 * 1024 * 1024, 8 * 1024 * 1024, 32 * 1024 * 1024);

    public void Validate(ExportContent content, ExportTableSelection? table = null, CancellationToken cancellationToken = default)
    {
        if (MaximumImageCharacters < 1 || MaximumHtmlCharacters < 1 || MaximumOutputBytes < 1 || MaximumImageOccurrences < 1)
            throw new ArgumentOutOfRangeException(nameof(ExportAllocationLimits));
        cancellationToken.ThrowIfCancellationRequested();
        ContainerBlock scope = table is null ? content.Document.Syntax : table.Resolve(content.Document);
        long characters = 0;
        var occurrences = 0;
        foreach (var node in scope.Descendants())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportImage? image = node switch
            {
                LinkInline { IsImage: true } link => content.Images.GetValueOrDefault(link.Url ?? ""),
                FencedCodeBlock { Info: "mermaid" or "mmd" } code => content.Diagrams.GetValueOrDefault(code.Span.Start),
                MathBlock or MathInline => content.Formulae.GetValueOrDefault(node.Span.Start),
                _ => null
            };
            if (image is null) continue;
            var encodedCharacters = ((long)image.PngBytes.Length + 2) / 3 * 4;
            if (++occurrences > MaximumImageOccurrences || encodedCharacters > MaximumImageCharacters - characters)
                throw new IOException("Repeated images exceed the export size limit. Export fewer or smaller images.");
            characters += encodedCharacters;
        }
    }
}

internal sealed class BoundedExportWriter(int maximumCharacters, CancellationToken cancellationToken) : StringWriter
{
    private void Check(int count)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count > maximumCharacters - GetStringBuilder().Length) throw new IOException("The generated HTML exceeds the export size limit.");
    }
    public override void Write(char value) { Check(1); base.Write(value); }
    public override void Write(string? value) { Check(value?.Length ?? 0); base.Write(value); }
    public override void Write(char[] buffer, int index, int count) { Check(count); base.Write(buffer, index, count); }
    public override void Write(ReadOnlySpan<char> buffer) { Check(buffer.Length); base.Write(buffer); }
    public override void Write(System.Text.StringBuilder? value) { Check(value?.Length ?? 0); base.Write(value); }
    public override void WriteLine() => Write(NewLine);
    public override void WriteLine(string? value) { Write(value); WriteLine(); }
    public override void WriteLine(char[] buffer, int index, int count) { Write(buffer, index, count); WriteLine(); }
    public override void WriteLine(ReadOnlySpan<char> buffer) { Write(buffer); WriteLine(); }
    public override void WriteLine(System.Text.StringBuilder? value) { Write(value); WriteLine(); }
}

/// <summary>A seekable package output that rejects growth before its backing buffer is allocated.</summary>
public sealed class BoundedExportStream(long maximumBytes, CancellationToken cancellationToken = default) : MemoryStream
{
    private void Check(long end)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (end > maximumBytes) throw new IOException("The generated document exceeds the export size limit.");
    }
    public override void Write(byte[] buffer, int offset, int count) { Check(checked(Position + count)); base.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Check(checked(Position + buffer.Length)); base.Write(buffer); }
    public override void WriteByte(byte value) { Check(checked(Position + 1)); base.WriteByte(value); }
    public override void SetLength(long value) { Check(value); base.SetLength(value); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    { token.ThrowIfCancellationRequested(); Write(buffer, offset, count); return Task.CompletedTask; }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
}
