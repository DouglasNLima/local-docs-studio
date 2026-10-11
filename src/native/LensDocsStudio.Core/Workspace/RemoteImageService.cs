using System.Net;
using System.Net.Http.Headers;

namespace LensDocsStudio.Core.Workspace;

/// <summary>Fetches only an explicitly supplied image URL. No document content, cookies or local paths are sent.</summary>
public sealed class RemoteImageService(HttpClient client)
{
    public const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp", "image/x-ms-bmp" };

    public static HttpClient CreateClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false, Credentials = null
    }) { Timeout = TimeSpan.FromSeconds(45) };

    public async Task<byte[]> DownloadAsync(string address, CancellationToken cancellationToken = default)
    {
        var uri = Validate(address);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { return await DownloadCoreAsync(uri, timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        { throw new IOException("The image download timed out. Try again or choose a local image.", ex); }
    }

    private async Task<byte[]> DownloadCoreAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var redirect = 0; redirect <= 5; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/gif"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                if (redirect == 5 || response.Headers.Location is not { } location) throw new IOException("The image address redirected too many times or has no destination.");
                uri = Validate(new Uri(uri, location).AbsoluteUri);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType is not { } contentType || !ImageTypes.Contains(contentType))
                throw new IOException("The address did not return a supported raster image. Use PNG, JPEG, GIF, WebP or BMP.");
            if (response.Content.Headers.ContentLength is > MaximumBytes) throw new IOException("The image exceeds the 32 MB download limit.");
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[65536];
            while (true)
            {
                var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                if (output.Length + count > MaximumBytes) throw new IOException("The image exceeds the 32 MB download limit.");
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            }
            if (output.Length == 0) throw new IOException("The image response was empty.");
            // The Windows image service must decode and re-encode these bytes before persisting or inserting Markdown.
            return output.ToArray();
        }
        throw new IOException("The image could not be downloaded.");
    }

    private static Uri Validate(string address)
    {
        if (address.Length > 8192 || !Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0)
            throw new IOException("Enter an HTTPS image address without embedded credentials.");
        return uri;
    }
}
