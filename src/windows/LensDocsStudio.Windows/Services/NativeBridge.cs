using System.Reflection;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LensDocsStudio.Windows.Services;

public sealed class NativeBridge
{
    private const int ProtocolVersion = 1;
    private const string WebSource = "LensDocsStudio.Web";
    private const string HostSource = "LensDocsStudio.Windows";
    private const string PingType = "lensDocs.native.ping";
    private const string PongType = "lensDocs.native.pong";
    private const string OpenFileType = "lensDocs.native.openFile";
    private const string OpenFileResultType = "lensDocs.native.openFileResult";
    private const string SaveFileType = "lensDocs.native.saveFile";
    private const string SaveFileResultType = "lensDocs.native.saveFileResult";
    private const string SaveFileAsType = "lensDocs.native.saveFileAs";
    private const string SaveFileAsResultType = "lensDocs.native.saveFileAsResult";
    private const string ErrorType = "lensDocs.native.error";
    private static readonly string[] Capabilities = ["diagnostics.ping", "file.open", "file.save", "file.saveAs"];
    private readonly NativeFileService nativeFileService;

    public NativeBridge(NativeFileService nativeFileService)
    {
        this.nativeFileService = nativeFileService;
    }

    public void Attach(CoreWebView2 coreWebView)
    {
        coreWebView.WebMessageReceived += HandleWebMessageReceived;
    }

    private async void HandleWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var id = ReadString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!TryReadInt(root, "protocolVersion", out var protocolVersion) || protocolVersion != ProtocolVersion)
            {
                PostError(coreWebView, id, "Unsupported native bridge protocol version.");
                return;
            }

            var type = ReadString(root, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                PostError(coreWebView, id, "Native bridge message type is required.");
                return;
            }

            if (ReadString(root, "source") is { } source && source != WebSource)
            {
                PostError(coreWebView, id, "Native bridge message source is unsupported.");
                return;
            }

            switch (type)
            {
                case PingType:
                    PostPong(coreWebView, id);
                    break;
                case OpenFileType:
                    PostResult(coreWebView, id, OpenFileResultType, await nativeFileService.OpenFileAsync());
                    break;
                case SaveFileType:
                    PostResult(coreWebView, id, SaveFileResultType, await nativeFileService.SaveFileAsync(
                        ReadPayloadString(root, "nativeHandleId"),
                        ReadPayloadString(root, "content")));
                    break;
                case SaveFileAsType:
                    PostResult(coreWebView, id, SaveFileAsResultType, await nativeFileService.SaveFileAsAsync(
                        ReadPayloadString(root, "suggestedName"),
                        ReadPayloadString(root, "content")));
                    break;
                default:
                    PostError(coreWebView, id, "Native bridge message type is unsupported.");
                    break;
            }
        }
        catch (NativeFileException ex)
        {
            TryPostSafeError(sender, args, ex.Message);
        }
        catch (JsonException)
        {
            // Invalid JSON is ignored because it cannot be trusted to contain a safe correlation id.
        }
        catch (Exception)
        {
            TryPostSafeError(sender, args, "Windows file operation failed safely.");
        }
    }

    private static void PostPong(CoreWebView2 coreWebView, string id)
    {
        var message = new
        {
            protocolVersion = ProtocolVersion,
            id,
            type = PongType,
            source = HostSource,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            payload = new
            {
                host = HostSource,
                appVersion = GetAppVersion(),
                capabilities = Capabilities,
            },
        };
        coreWebView.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    private static void PostError(CoreWebView2 coreWebView, string id, string message)
    {
        var response = new
        {
            protocolVersion = ProtocolVersion,
            id,
            type = ErrorType,
            source = HostSource,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            payload = new
            {
                message,
            },
        };
        coreWebView.PostWebMessageAsJson(JsonSerializer.Serialize(response));
    }

    private static void PostResult(CoreWebView2 coreWebView, string id, string type, object payload)
    {
        var response = new
        {
            protocolVersion = ProtocolVersion,
            id,
            type,
            source = HostSource,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            payload,
        };
        coreWebView.PostWebMessageAsJson(JsonSerializer.Serialize(response));
    }

    private static void TryPostSafeError(object? sender, CoreWebView2WebMessageReceivedEventArgs args, string message)
    {
        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var id = ReadString(document.RootElement, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                PostError(coreWebView, id, message);
            }
        }
        catch (JsonException)
        {
            // Invalid JSON still fails closed.
        }
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryReadInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static string? ReadPayloadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty("payload", out var payload)
            && payload.ValueKind == JsonValueKind.Object
            ? ReadString(payload, propertyName)
            : null;
    }
}
