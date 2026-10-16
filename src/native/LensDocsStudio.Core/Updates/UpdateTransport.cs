using System.Net;

namespace LensDocsStudio.Core.Updates;

/// <summary>Public GitHub release transport. Sends no workspace data or credentials.</summary>
public sealed class UpdateTransport : IDisposable
{
    private readonly HttpClient client;
    public UpdateTransport(HttpMessageHandler? handler = null)
    {
        client = new(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(15) }) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LensDocsStudio.Native/0.1");
    }
    public async Task<HttpResponseMessage> OpenAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!IsInitial(uri)) throw new IOException("Update requests must use the configured GitHub repository.");
        for (var redirect = 0; redirect < 5; redirect++)
        {
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect or HttpStatusCode.SeeOther)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location is null) throw new IOException("The release download redirect has no destination.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if (!IsInitial(uri) && !IsAssetHost(uri)) throw new IOException("The update redirected outside the permitted GitHub asset hosts.");
                continue;
            }
            if (!response.IsSuccessStatusCode) { var status = response.StatusCode; response.Dispose(); throw new IOException("GitHub update check returned " + (int)status + ". Try again later."); }
            return response;
        }
        throw new IOException("The update download exceeded the redirect limit.");
    }
    public async Task<byte[]> ReadAsync(Uri uri, int maximumBytes, CancellationToken cancellationToken)
    {
        using var response = await OpenAsync(uri, cancellationToken);
        if (response.Content.Headers.ContentLength > maximumBytes) throw new IOException("Update metadata exceeds the size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var data = new MemoryStream(); var buffer = new byte[16384]; int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (data.Length + count > maximumBytes) throw new IOException("Update metadata exceeds the size limit.");
            data.Write(buffer, 0, count);
        }
        return data.ToArray();
    }
    private static bool Secure(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0;
    private static bool IsInitial(Uri uri) => Secure(uri) && ((uri.Host == "api.github.com" && uri.AbsolutePath == $"/repos/{UpdatePolicy.Repository}/releases") ||
        (uri.Host == "github.com" && uri.AbsolutePath.StartsWith($"/{UpdatePolicy.Repository}/releases/download/", StringComparison.Ordinal)));
    private static bool IsAssetHost(Uri uri) => Secure(uri) && uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
    public void Dispose() => client.Dispose();
}
