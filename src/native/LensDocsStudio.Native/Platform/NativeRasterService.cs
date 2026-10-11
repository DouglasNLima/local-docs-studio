using System.Windows.Media.Imaging;

namespace LensDocsStudio.Native.Platform;

public static class NativeRasterService
{
    public static BitmapSource Read(Stream input)
    {
        if (!input.CanSeek || input.Length > 32 * 1024 * 1024) throw new IOException("Images are limited to 32 MB.");
        // Inspect dimensions before requesting a decoded pixel cache. OnLoad here would allocate first.
        var frame = BitmapFrame.Create(input, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || (long)frame.PixelWidth * frame.PixelHeight > 64_000_000)
            throw new IOException("The image exceeds the 64 megapixel limit.");
        var cached = new CachedBitmap(frame, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        cached.Freeze();
        return cached;
    }
}
