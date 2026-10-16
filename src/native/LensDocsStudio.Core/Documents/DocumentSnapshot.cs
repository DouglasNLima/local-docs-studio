using System.Security.Cryptography;
using System.Text;

namespace LensDocsStudio.Core.Documents;

public sealed record DocumentSnapshot(string Path, string Text, string Fingerprint, bool Utf8Bom)
{
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static DocumentSnapshot FromBytes(string path, byte[] bytes)
    {
        var bom = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf });
        var text = new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        return new(path, text, Hash(bytes), bom);
    }
    public static byte[] Encode(string text, bool bom)
    {
        var encoding = new UTF8Encoding(bom, true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }
}

public sealed class DocumentConflictException(string message) : IOException(message);
