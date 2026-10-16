using System.Windows.Media.Imaging;

namespace LensDocsStudio.Native.Platform;

public static class NativeRasterService
{
    public static BitmapSource Read(Stream input, long maximumDecodedBytes = 256L * 1024 * 1024)
    {
        if (!input.CanSeek || input.Length > 32 * 1024 * 1024) throw new IOException("Images are limited to 32 MB.");
        // Inspect dimensions before requesting a decoded pixel cache. OnLoad here would allocate first.
        var frame = BitmapFrame.Create(input, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || (long)frame.PixelWidth * frame.PixelHeight > 64_000_000)
            throw new IOException("The image exceeds the 64 megapixel limit.");
        if (DecodedBytes(frame) > maximumDecodedBytes) throw new IOException("The image exceeds the decoded pixel memory budget.");
        var cached = new CachedBitmap(frame, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        cached.Freeze();
        return cached;
    }
    public static long DecodedBytes(BitmapSource image) => checked((long)image.PixelWidth * image.PixelHeight * Math.Max(4, (image.Format.BitsPerPixel + 7) / 8));
}
