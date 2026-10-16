namespace LensDocsStudio.Core.State;

// Does not own the file: SessionStore flushes it durably before the atomic replacement.
internal sealed class BoundedStateWriteStream(Stream output, long maximumBytes) : Stream
{
    private long written;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => written;
    public override long Position { get => written; set => throw new NotSupportedException(); }
    public override void Flush() => output.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    private void Reserve(int count)
    {
        if (count > maximumBytes - written)
            throw new IOException($"Recovery exceeds the {maximumBytes:N0}-byte saved-state limit. The previous session is unchanged; close saved tabs to reduce recovery size before retrying.");
        written += count;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Reserve(buffer.Length);
        output.Write(buffer);
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reserve(buffer.Length);
        return output.WriteAsync(buffer, cancellationToken);
    }
}
