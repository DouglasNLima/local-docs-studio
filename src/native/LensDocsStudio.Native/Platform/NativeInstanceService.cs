using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LensDocsStudio.Native.Platform;

/// <summary>One writer per state directory, with same-user file activation and an installer presence mutex.</summary>
public sealed class NativeInstanceService : IDisposable
{
    private readonly Mutex instance;
    private readonly Mutex installerPresence = new(false, @"Local\LensDocsStudio.Native.Running");
    private readonly string pipeName;
    private readonly CancellationTokenSource shutdown = new();
    public bool IsPrimary { get; }
    public NativeInstanceService(string stateRoot)
    {
        pipeName = "LensDocsStudio.Native." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(stateRoot).ToUpperInvariant())))[..32];
        instance = new Mutex(false, @"Local\" + pipeName, out var created);
        IsPrimary = created;
    }
    public async Task ForwardAsync(string[] paths)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(paths);
        if (bytes.Length > 65536) throw new IOException("Too many documents were supplied for activation.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);
        await pipe.WriteAsync(bytes, timeout.Token);
        await pipe.FlushAsync(timeout.Token);
    }
    public async Task ListenAsync(Func<string[], Task> activated)
    {
        while (!shutdown.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(shutdown.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var data = new MemoryStream(); var buffer = new byte[4096];
                int count;
                while ((count = await pipe.ReadAsync(buffer, timeout.Token)) != 0)
                {
                    if (data.Length + count > 65536) throw new InvalidDataException("Activation payload is too large.");
                    data.Write(buffer, 0, count);
                }
                var paths = JsonSerializer.Deserialize<string[]>(data.ToArray()) ?? [];
                await activated(paths);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or JsonException) { /* Invalid activation never changes a workspace. */ }
        }
    }
    public void Dispose() { shutdown.Cancel(); instance.Dispose(); installerPresence.Dispose(); }
}
