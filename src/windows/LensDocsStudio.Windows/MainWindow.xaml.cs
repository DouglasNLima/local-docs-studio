using LensDocsStudio.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LensDocsStudio.Windows;

public sealed partial class MainWindow : Window
{
    private readonly StaticAppLocator staticAppLocator = new();
    private readonly WebViewBootstrapper webViewBootstrapper;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Lens Docs Studio";
        var nativeFileService = new NativeFileService(this);
        var nativeWorkspaceService = new NativeWorkspaceService(this);
        webViewBootstrapper = new WebViewBootstrapper(new NativeBridge(nativeFileService, nativeWorkspaceService));
        _ = InitialiseAsync();
    }

    private async Task InitialiseAsync()
    {
        try
        {
            var staticAppRoot = staticAppLocator.GetStaticAppRoot(AppContext.BaseDirectory);
            await webViewBootstrapper.InitialiseAsync(AppWebView, staticAppRoot);
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private void ShowStartupError(Exception ex)
    {
        RootGrid.Children.Clear();
        RootGrid.Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 250));
        RootGrid.Children.Add(new Border
        {
            Padding = new Thickness(32),
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Lens Docs Studio could not start.",
                        FontSize = 24,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = ex.Message,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        });
    }
}
