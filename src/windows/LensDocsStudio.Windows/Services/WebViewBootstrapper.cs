using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace LensDocsStudio.Windows.Services;

public sealed class WebViewBootstrapper
{
    private const string AppHostName = "lens-docs-studio.local";
    private readonly NativeBridge nativeBridge;

    public WebViewBootstrapper(NativeBridge nativeBridge)
    {
        this.nativeBridge = nativeBridge;
    }

    public async Task InitialiseAsync(WebView2 webView, string staticAppRoot)
    {
        await webView.EnsureCoreWebView2Async();

        var coreWebView = webView.CoreWebView2 ?? throw new InvalidOperationException("WebView2 did not initialise.");
        coreWebView.SetVirtualHostNameToFolderMapping(
            AppHostName,
            staticAppRoot,
            CoreWebView2HostResourceAccessKind.DenyCors);

        coreWebView.Settings.IsStatusBarEnabled = false;
        nativeBridge.Attach(coreWebView);
        webView.Source = new Uri($"https://{AppHostName}/index.html");
    }
}
