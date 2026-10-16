using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Exports;

/// <summary>Windows CF_HTML uses byte offsets into UTF-8, including its ASCII header.</summary>
/// <remarks>https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format</remarks>
public static partial class HtmlClipboardFormat
{
    public const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private const string Prefix = "<html xmlns:x=\"urn:schemas-microsoft-com:office:excel\"><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
    private const string Suffix = "<!--EndFragment--></body></html>";
    private const string Header = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

    public static string Encode(string fragment)
    {
        if (fragment.Length > MaximumBytes) throw new IOException("The HTML clipboard content exceeds 32 MB.");
        var headerBytes = string.Format(CultureInfo.InvariantCulture, Header, 0, 0, 0, 0).Length;
        var start = headerBytes + Utf8.GetByteCount(Prefix);
        var end = checked(start + Utf8.GetByteCount(fragment));
        var htmlEnd = checked(end + Utf8.GetByteCount(Suffix));
        if (htmlEnd > MaximumBytes) throw new IOException("The HTML clipboard content exceeds 32 MB.");
        return string.Format(CultureInfo.InvariantCulture, Header, headerBytes, htmlEnd, start, end) + Prefix + fragment + Suffix;
    }

    public static string Decode(string clipboard)
    {
        if (clipboard.Length > MaximumBytes) throw new IOException("The HTML clipboard content exceeds 32 MB.");
        var bytes = Utf8.GetBytes(clipboard);
        if (bytes.Length > MaximumBytes) throw new IOException("The HTML clipboard content exceeds 32 MB.");
        // Some applications provide raw HTML through the registered format without a CF_HTML header.
        if (!clipboard.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)) return clipboard.TrimEnd('\0');
        var header = clipboard[..Math.Min(clipboard.Length, 4096)];
        var firstTag = header.IndexOf('<');
        if (firstTag >= 0) header = header[..firstTag];
        var fields = Offset().Matches(header).ToArray().GroupBy(m => m.Groups[1].Value, StringComparer.OrdinalIgnoreCase).ToArray();
        if (fields.Any(g => g.Count() != 1)) throw new IOException("The HTML clipboard header contains duplicate offsets.");
        var offsets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in fields)
        {
            if (!int.TryParse(group.Single().Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset)) throw new IOException("The HTML clipboard offsets are invalid.");
            offsets[group.Key] = offset;
        }
        if (!offsets.TryGetValue("StartFragment", out var start) || !offsets.TryGetValue("EndFragment", out var end) ||
            start < Utf8.GetByteCount(header) || end < start || end > bytes.Length)
            throw new IOException("The HTML clipboard fragment lies outside its content.");
        if (offsets.TryGetValue("StartHTML", out var htmlStart) && htmlStart >= 0 && (htmlStart > start || htmlStart < Utf8.GetByteCount(header)) ||
            offsets.TryGetValue("EndHTML", out var htmlEnd) && htmlEnd >= 0 && (htmlEnd < end || htmlEnd > bytes.Length))
            throw new IOException("The HTML clipboard context does not contain its fragment.");
        try { return Utf8.GetString(bytes, start, end - start); }
        catch (DecoderFallbackException ex) { throw new IOException("The HTML clipboard fragment splits a Unicode character.", ex); }
    }

    [GeneratedRegex(@"(?:^|[\r\n])(StartHTML|EndHTML|StartFragment|EndFragment):[ \t]*(-?\d+)[ \t]*(?=[\r\n]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Offset();
}
