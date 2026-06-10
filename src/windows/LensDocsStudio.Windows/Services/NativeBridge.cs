using System.Reflection;
using System.Text.Json;
using LensDocsStudio.Windows.Smoke;
using Microsoft.UI.Dispatching;
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
    private const string OpenFolderType = "lensDocs.native.openFolder";
    private const string OpenFolderResultType = "lensDocs.native.openFolderResult";
    private const string SaveWorkspaceFileType = "lensDocs.native.saveWorkspaceFile";
    private const string SaveWorkspaceFileResultType = "lensDocs.native.saveWorkspaceFileResult";
    private const string CreateWorkspaceFileType = "lensDocs.native.createWorkspaceFile";
    private const string CreateWorkspaceFileResultType = "lensDocs.native.createWorkspaceFileResult";
    private const string RefreshWorkspaceFileType = "lensDocs.native.refreshWorkspaceFile";
    private const string RefreshWorkspaceFileResultType = "lensDocs.native.refreshWorkspaceFileResult";
    private const string WorkspaceChangedType = "lensDocs.native.workspaceChanged";
    private const string SmokeOpenFixtureFileType = "lensDocs.native.smoke.openFixtureFile";
    private const string SmokeOpenFixtureFileResultType = "lensDocs.native.smoke.openFixtureFileResult";
    private const string SmokeOpenFixtureWorkspaceType = "lensDocs.native.smoke.openFixtureWorkspace";
    private const string SmokeOpenFixtureWorkspaceResultType = "lensDocs.native.smoke.openFixtureWorkspaceResult";
    private const string SmokeTouchWorkspaceFileType = "lensDocs.native.smoke.touchWorkspaceFile";
    private const string SmokeTouchWorkspaceFileResultType = "lensDocs.native.smoke.touchWorkspaceFileResult";
    private const string SmokeSaveFixtureFileAsType = "lensDocs.native.smoke.saveFixtureFileAs";
    private const string SmokeSaveFixtureFileAsResultType = "lensDocs.native.smoke.saveFixtureFileAsResult";
    private const string SmokeCompleteType = "lensDocs.native.smoke.complete";
    private const string SmokeCompleteResultType = "lensDocs.native.smoke.completeResult";
    private const string ErrorType = "lensDocs.native.error";
    private static readonly string[] Capabilities =
    [
        "diagnostics.ping",
        "file.open",
        "file.save",
        "file.saveAs",
        "workspace.openFolder",
        "workspace.saveFile",
        "workspace.createFile",
        "workspace.watch",
        "workspace.refreshFile",
    ];
    private static readonly string[] SmokeCapabilities =
    [
        "smoke.nativeFixtures",
        "smoke.workspaceChange",
    ];
    private readonly NativeFileService nativeFileService;
    private readonly NativeWorkspaceService nativeWorkspaceService;
    private readonly SmokeFixtureService? smokeFixtureService;
    private readonly SmokeCompletionService? smokeCompletionService;
    private readonly DispatcherQueue dispatcherQueue;
    private CoreWebView2? attachedCoreWebView;

    public NativeBridge(
        NativeFileService nativeFileService,
        NativeWorkspaceService nativeWorkspaceService,
        SmokeFixtureService? smokeFixtureService = null,
        SmokeCompletionService? smokeCompletionService = null)
    {
        this.nativeFileService = nativeFileService;
        this.nativeWorkspaceService = nativeWorkspaceService;
        this.smokeFixtureService = smokeFixtureService;
        this.smokeCompletionService = smokeCompletionService;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        this.nativeWorkspaceService.WorkspaceChanged += HandleWorkspaceChanged;
    }

    public void Attach(CoreWebView2 coreWebView)
    {
        attachedCoreWebView = coreWebView;
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
                case OpenFolderType:
                    PostResult(coreWebView, id, OpenFolderResultType, await nativeWorkspaceService.OpenFolderAsync());
                    break;
                case SaveWorkspaceFileType:
                    PostResult(coreWebView, id, SaveWorkspaceFileResultType, await nativeWorkspaceService.SaveWorkspaceFileAsync(
                        ReadPayloadString(root, "nativeHandleId"),
                        ReadPayloadString(root, "content")));
                    break;
                case CreateWorkspaceFileType:
                    PostResult(coreWebView, id, CreateWorkspaceFileResultType, await nativeWorkspaceService.CreateWorkspaceFileAsync(
                        ReadPayloadString(root, "nativeWorkspaceId"),
                        ReadPayloadString(root, "path"),
                        ReadPayloadString(root, "content")));
                    break;
                case RefreshWorkspaceFileType:
                    PostResult(coreWebView, id, RefreshWorkspaceFileResultType, await nativeWorkspaceService.RefreshWorkspaceFileAsync(
                        ReadPayloadString(root, "nativeWorkspaceId"),
                        ReadPayloadString(root, "nativeHandleId"),
                        ReadPayloadString(root, "path")));
                    break;
                case SmokeOpenFixtureFileType:
                    PostResult(coreWebView, id, SmokeOpenFixtureFileResultType, await RequireSmokeFixtures().OpenFixtureFileAsync());
                    break;
                case SmokeSaveFixtureFileAsType:
                    PostResult(coreWebView, id, SmokeSaveFixtureFileAsResultType, await RequireSmokeFixtures().SaveFixtureFileAsAsync(
                        ReadPayloadString(root, "content")));
                    break;
                case SmokeOpenFixtureWorkspaceType:
                    PostResult(coreWebView, id, SmokeOpenFixtureWorkspaceResultType, await RequireSmokeFixtures().OpenFixtureWorkspaceAsync());
                    break;
                case SmokeTouchWorkspaceFileType:
                    PostResult(coreWebView, id, SmokeTouchWorkspaceFileResultType, await RequireSmokeFixtures().TouchWorkspaceFileAsync());
                    break;
                case SmokeCompleteType:
                    nativeWorkspaceService.StopWatching();
                    PostResult(coreWebView, id, SmokeCompleteResultType, RequireSmokeCompletion().Complete(ReadPayload(root)));
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

    private void HandleWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            _ = dispatcherQueue.TryEnqueue(() => HandleWorkspaceChanged(sender, args));
            return;
        }

        var coreWebView = attachedCoreWebView;
        if (coreWebView is null)
        {
            return;
        }

        var message = new
        {
            protocolVersion = ProtocolVersion,
            id = Guid.NewGuid().ToString("N"),
            type = WorkspaceChangedType,
            source = HostSource,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            payload = new
            {
                nativeWorkspaceId = args.NativeWorkspaceId,
                changes = args.Changes.Select(static change => new
                {
                    kind = change.Kind,
                    path = change.Path,
                    oldPath = change.OldPath,
                    nativeHandleId = change.NativeHandleId,
                }).ToArray(),
            },
        };

        try
        {
            coreWebView.PostWebMessageAsJson(JsonSerializer.Serialize(message));
        }
        catch
        {
            nativeWorkspaceService.StopWatching();
        }
    }

    private void PostPong(CoreWebView2 coreWebView, string id)
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
                capabilities = GetCapabilities(),
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

    private string[] GetCapabilities()
    {
        return smokeFixtureService?.Enabled == true
            ? [.. Capabilities, .. SmokeCapabilities]
            : Capabilities;
    }

    private SmokeFixtureService RequireSmokeFixtures()
    {
        if (smokeFixtureService?.Enabled == true)
        {
            return smokeFixtureService;
        }

        throw new NativeFileException("Smoke fixture operations are unavailable.");
    }

    private SmokeCompletionService RequireSmokeCompletion()
    {
        if (smokeFixtureService?.Enabled == true && smokeCompletionService is not null)
        {
            return smokeCompletionService;
        }

        throw new NativeFileException("Smoke completion is unavailable.");
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

    private static JsonElement? ReadPayload(JsonElement root)
    {
        return root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object
            ? payload
            : null;
    }
}
