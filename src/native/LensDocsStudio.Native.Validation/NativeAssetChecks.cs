using System.Text.Json;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeAssetChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "assets-" + Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(root, "draft"); var savedRoot = Path.Combine(root, "saved");
        Directory.CreateDirectory(draftRoot); Directory.CreateDirectory(savedRoot);
        var pixels = new byte[64 * 48 * 4];
        for (var offset = 0; offset < pixels.Length; offset += 4) { pixels[offset] = 62; pixels[offset + 1] = 136; pixels[offset + 2] = 255; pixels[offset + 3] = 255; }
        var bitmap = BitmapSource.Create(64, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var bytes = new MemoryStream(); encoder.Save(bytes);
        var sourcePath = Path.Combine(draftRoot, "Untitled.md");
        var reference = new ImageAssetService().ImportAsync(draftRoot, sourcePath, bytes.ToArray(), "Clipboard image.png").GetAwaiter().GetResult();
        var text = "# Recovered image\n\n![Clipboard](" + reference + ")\n";
        var session = new SessionStore(Path.Combine(root, "state"));
        session.WriteAsync("session.json", new SessionState(1, null, null, [new(Guid.NewGuid(), null, text, null, false, 0, 0, AssetWorkspace: draftRoot)])).GetAwaiter().GetResult();
        var recovered = session.ReadAsync<SessionState>("session.json").GetAwaiter().GetResult()!.Documents.Single();
        var draft = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService()).Prepare(recovered.Text, sourcePath, recovered.AssetWorkspace, false);
        if (draft.Images.Count != 1) throw new InvalidOperationException("Recovered draft image did not resolve for export.");
        var destination = Path.Combine(savedRoot, "saved.md");
        var transfer = new AssetTransferService().PrepareAsync(recovered.Text, sourcePath, new(draftRoot), destination, new(savedRoot)).GetAwaiter().GetResult();
        new DocumentStore(Path.Combine(root, "recovery")).SaveAsync(destination, transfer.Text, null).GetAwaiter().GetResult();
        transfer.Commit(); transfer.DisposeAsync().GetAwaiter().GetResult();
        var saved = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService()).Prepare(File.ReadAllText(destination), destination, savedRoot, false);
        if (saved.Images.Count != 1 || saved.Images.Values.Single().PixelWidth != 64 || saved.Images.Values.Single().PixelHeight != 48)
            throw new InvalidOperationException("Saved document did not retain its actual image.");
        var rich = new ClipboardExportService().PrepareAsync(File.ReadAllText(destination), destination, savedRoot, false).GetAwaiter().GetResult();
        var clipboardHtml = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(rich.Html));
        var embedded = clipboardHtml.QuerySelector("img")?.GetAttribute("src") ?? throw new IOException("Rich copying lost the saved image.");
        using (var imageBytes = new MemoryStream(Convert.FromBase64String(embedded.Split(',', 2)[1])))
        {
            var copied = NativeRasterService.Read(imageBytes);
            if (copied.PixelWidth != 64 || copied.PixelHeight != 48) throw new IOException("Rich clipboard pixels do not match the saved image.");
        }
        var boundedPreparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        var sizeRejected = false; var cancellationHonoured = false;
        try { boundedPreparation.Prepare(File.ReadAllText(destination), destination, savedRoot, false, maximumEmbeddedImageBytes: 8); }
        catch (IOException) { sizeRejected = true; }
        try { boundedPreparation.Prepare(File.ReadAllText(destination), destination, savedRoot, false, new CancellationToken(true)); }
        catch (OperationCanceledException) { cancellationHonoured = true; }
        if (!sizeRejected || !cancellationHonoured) throw new IOException("Output image preparation did not respect its byte budget or cancellation.");
        using (var client = new HttpClient(new FixtureImageHandler(bytes.ToArray())))
        {
            var downloaded = new RemoteImageService(client).DownloadAsync("https://fixture.invalid/image.png").GetAwaiter().GetResult();
            var imported = new ImageAssetService().ImportAsync(savedRoot, destination, downloaded, "downloaded-image.png").GetAwaiter().GetResult();
            var importedPath = new WorkspacePath(savedRoot).ResolveLink(destination, imported);
            using var input = File.OpenRead(importedPath);
            var decoded = BitmapFrame.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoded.PixelWidth != 64 || decoded.PixelHeight != 48) throw new IOException("The downloaded image did not persist as a decodable PNG.");
        }
        var imageCount = Directory.GetFiles(Path.Combine(savedRoot, "assets", "images")).Length;
        var invalidRejected = false;
        try { new ImageAssetService().ImportAsync(savedRoot, destination, "<svg onload='run()'/>"u8.ToArray(), "spoofed.png").GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException) { invalidRejected = true; }
        if (!invalidRejected || Directory.GetFiles(Path.Combine(savedRoot, "assets", "images")).Length != imageCount)
            throw new IOException("An invalid remote image was persisted before successful decoding.");
        NativeRasterChecks.RejectOversizedRaster();
        var document = new TextDocument("Before selected after");
        var insertion = new DocumentInsertion(document, 7, 8);
        document.Insert(0, "Typed while loading. ");
        insertion.Insert("![image](assets/image.png)");
        if (document.Text != "Typed while loading. Before ![image](assets/image.png) after") throw new InvalidOperationException("Image insertion did not follow source edits.");
        var edited = new TextDocument("selected");
        var pending = new DocumentInsertion(edited, 0, 8); edited.Insert(3, "new text"); pending.Insert("IMAGE");
        if (edited.Text != "IMAGEselnew textected") throw new InvalidOperationException("Pending insertion discarded intervening edits.");
        using (var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(root, "queued-writes")), session, new WorkspaceService(), new DesktopDialogs()))
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var order = new List<string>();
            var first = model.RunAsync(async () => { await release.Task; order.Add("first"); });
            var second = model.RunAsync(() => { order.Add("second"); return Task.CompletedTask; });
            if (!model.IsBusy || second.IsCompleted) throw new InvalidOperationException("A queued operation was discarded while the workspace was busy.");
            release.SetResult(); Task.WhenAll(first, second).GetAwaiter().GetResult();
            if (!order.SequenceEqual(new[] { "first", "second" }) || model.IsBusy) throw new InvalidOperationException("Workspace operations did not complete in order.");
        }
        File.WriteAllText(Path.Combine(output, "native-assets.json"), JsonSerializer.Serialize(new
        { Passed = true, Checks = new[] { "Native image decoding and durable PNG persistence", "Unsaved draft image resolves after session recovery", "Save as produces portable source and a real image", "Rich clipboard HTML embeds independently decodable native image pixels", "Image preparation respects its aggregate output budget and cancellation", "Simulated HTTPS image response is decoded and persisted as a native PNG", "Invalid image content is rejected before persistence", "Raster dimensions are checked before allocating the pixel cache", "Pending insertion follows offsets and preserves intervening edits", "Busy workspace operations queue without discarding user commands" }, FixtureRoot = root }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed class FixtureImageHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") } } });
    }
}
