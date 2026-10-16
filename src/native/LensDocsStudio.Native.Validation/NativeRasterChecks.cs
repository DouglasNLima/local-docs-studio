using System.Buffers.Binary;
using System.IO.Compression;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeRasterChecks
{
    public static void RejectOversizedRaster()
    {
        // A small valid compressed monochrome PNG whose dimensions exceed the native pixel budget.
        using var png = new MemoryStream(); png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, 9000); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), 9000);
        header[8] = 1;
        Chunk("IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, true))
        {
            var row = new byte[1126];
            for (var line = 0; line < 9000; line++) deflate.Write(row);
        }
        Chunk("IDAT"u8, compressed.ToArray()); Chunk("IEND"u8, []);
        png.Position = 0;
        try { NativeRasterService.Read(png); }
        catch (IOException ex) when (ex.Message.Contains("64 megapixel", StringComparison.Ordinal)) { return; }
        throw new IOException("The native raster service did not reject an oversized image by its dimensions.");

        void Chunk(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> word = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(word, data.Length); png.Write(word); png.Write(type); png.Write(data);
            uint crc = uint.MaxValue;
            foreach (var value in type) crc = Update(crc, value);
            foreach (var value in data) crc = Update(crc, value);
            BinaryPrimitives.WriteUInt32BigEndian(word, ~crc); png.Write(word);
        }
        static uint Update(uint crc, byte value)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
            return crc;
        }
    }
}
