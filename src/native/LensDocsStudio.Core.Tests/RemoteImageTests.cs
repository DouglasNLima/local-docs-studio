using System.Net;
using System.Net.Http.Headers;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class RemoteImageTests
{
    [Fact] public async Task Explicit_image_download_follows_https_redirects_without_sending_document_context()
    {
        var requests = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Referrer); Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie"));
            Assert.Null(request.Content);
            return requests.Count == 1 ? new(HttpStatusCode.Found) { Headers = { Location = new Uri("/actual.png", UriKind.Relative) } } : Image([1, 2, 3]);
        }));
        Assert.Equal(new byte[] { 1, 2, 3 }, await new RemoteImageService(client).DownloadAsync("https://example.test/image"));
        Assert.Equal(new[] { "https://example.test/image", "https://example.test/actual.png" }, requests);
    }

    [Theory]
    [InlineData("file:///C:/private/image.png")]
    [InlineData("http://example.test/image.png")]
    [InlineData("https://user:password@example.test/image.png")]
    [InlineData("data:image/png;base64,AAAA")]
    public async Task Unsafe_addresses_are_rejected_before_any_request(string address)
    {
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("No request should be sent.")));
        await Assert.ThrowsAsync<IOException>(() => new RemoteImageService(client).DownloadAsync(address));
    }

    [Fact] public async Task Redirects_cannot_downgrade_to_http_or_embed_credentials()
    {
        foreach (var target in new[] { "http://example.test/image.png", "https://user:secret@example.test/image.png" })
        {
            using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.Found) { Headers = { Location = new Uri(target) } }));
            await Assert.ThrowsAsync<IOException>(() => new RemoteImageService(client).DownloadAsync("https://example.test/image"));
        }
    }

    [Fact] public async Task Active_content_empty_images_oversized_responses_and_redirect_loops_are_rejected()
    {
        foreach (var response in new[] { Image([1], "text/html"), Image([1], "image/svg+xml"), Image([]), Image([1]) })
        {
            if (response.Content.Headers.ContentLength == 1 && response.Content.Headers.ContentType!.MediaType == "image/png")
                response.Content.Headers.ContentLength = RemoteImageService.MaximumBytes + 1L;
            using var client = new HttpClient(new Handler(_ => response));
            await Assert.ThrowsAsync<IOException>(() => new RemoteImageService(client).DownloadAsync("https://example.test/image"));
        }
        var requests = 0;
        using var redirects = new HttpClient(new Handler(_ => { requests++; return new(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.test/again") } }; }));
        await Assert.ThrowsAsync<IOException>(() => new RemoteImageService(redirects).DownloadAsync("https://example.test/image"));
        Assert.Equal(6, requests);
    }

    [Fact] public async Task A_stream_without_a_content_length_is_still_bounded()
    {
        using var client = new HttpClient(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new OversizedStream()) };
            response.Content.Headers.ContentType = new("image/png"); return response;
        }));
        await Assert.ThrowsAsync<IOException>(() => new RemoteImageService(client).DownloadAsync("https://example.test/image"));
    }

    [Fact] public async Task Caller_cancellation_is_preserved_and_transport_timeout_has_a_clear_message()
    {
        using var client = new HttpClient(new Handler(_ => throw new OperationCanceledException()));
        var error = await Assert.ThrowsAsync<IOException>(() => new RemoteImageService(client).DownloadAsync("https://example.test/image"));
        Assert.Contains("timed out", error.Message);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RemoteImageService(client).DownloadAsync("https://example.test/image", cancelled.Token));
    }

    private static HttpResponseMessage Image(byte[] bytes, string type = "image/png") => new(HttpStatusCode.OK)
        { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue(type) } } };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
    private sealed class OversizedStream : Stream
    {
        private long remaining = RemoteImageService.MaximumBytes + 1L;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { var length = (int)Math.Min(count, remaining); remaining -= length; return length; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
