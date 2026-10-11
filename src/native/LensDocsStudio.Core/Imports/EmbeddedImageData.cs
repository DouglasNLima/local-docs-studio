namespace LensDocsStudio.Core.Imports;

public static class EmbeddedImageData
{
    public static byte[] Decode(string source)
    {
        var comma = source.IndexOf(',');
        if (comma is < 0 or > 40 || !new[] { "png", "jpeg", "gif", "webp", "bmp" }.Any(type =>
            source[..comma].Equals("data:image/" + type + ";base64", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("The embedded image is not a supported base64 raster image.");
        if (source.Length - comma > 44 * 1024 * 1024) throw new IOException("An embedded image exceeds the 32 MB limit.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(source[(comma + 1)..]); }
        catch (FormatException ex) { throw new IOException("An embedded image has invalid base64 data.", ex); }
        if (bytes.Length is 0 or > 32 * 1024 * 1024) throw new IOException("An embedded image is empty or exceeds the 32 MB limit.");
        return bytes;
    }
}
