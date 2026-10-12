using System.Text.Json;
using System.Text.Json.Serialization;

namespace LensDocsStudio.Core.State;

/// <summary>Portable appearance choices. Imported data never carries paths, document state or update consent.</summary>
public static class SettingsTransferFormat
{
    public const string FormatVersion = "lens-docs-studio-settings-1.2";
    public const string ReadingFormatVersion = "lens-docs-studio-settings-1.1";
    public const string LegacyFormatVersion = "lens-docs-studio-settings-1.0";
    public const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private sealed record Transfer(string? FormatVersion, string? Theme, string? Layout, bool? ShowOutline, EditorPatch? Editor, PreviewPatch? Preview);
    private sealed record EditorPatch(string? FontFamily, int? FontSize, bool? WordWrap, bool? ShowLineNumbers, int? IndentationSize, bool? ConvertTabsToSpaces);
    private sealed record PreviewPatch(int? Zoom, string? ReadingMode, string? DiagramTheme);

    public static byte[] Write(WorkspaceSettings settings)
    {
        if (!settings.IsValid) throw new ArgumentException("Choose valid appearance settings before exporting them.");
        return JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = FormatVersion, settings.Theme, settings.Layout, settings.ShowOutline, settings.Editor, settings.Preview }, Json);
    }

    public static WorkspaceSettings Read(ReadOnlyMemory<byte> bytes, WorkspaceSettings current)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("The settings file exceeds 64 KB.");
        if (bytes.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) bytes = bytes[3..];
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            Unique(root);
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Choose a Lens Docs Studio appearance settings JSON file.");
            var imported = root.Deserialize<Transfer>(Json)!;
            if (imported.FormatVersion is not (FormatVersion or ReadingFormatVersion or LegacyFormatVersion))
                throw new InvalidDataException("Choose a Lens Docs Studio appearance settings export (version 1.0, 1.1 or 1.2).");
            if (imported.FormatVersion == LegacyFormatVersion && imported.Preview?.ReadingMode is not null)
                throw new InvalidDataException("Preview reading mode requires appearance settings version 1.1.");
            if (imported.FormatVersion != FormatVersion && imported.Preview?.DiagramTheme is not null)
                throw new InvalidDataException("Diagram theme requires appearance settings version 1.2.");
            if (imported.Theme is null && imported.Layout is null && imported.ShowOutline is null && imported.Editor is null && imported.Preview is null)
                throw new InvalidDataException("The settings file contains no appearance choices.");
            var editor = current.Editor;
            if (imported.Editor is { } patch)
            {
                if (patch == new EditorPatch(null, null, null, null, null, null)) throw new InvalidDataException("The editor settings contain no choices.");
                editor = new(patch.FontFamily ?? editor.FontFamily, patch.FontSize ?? editor.FontSize, patch.WordWrap ?? editor.WordWrap,
                    patch.ShowLineNumbers ?? editor.ShowLineNumbers, patch.IndentationSize ?? editor.IndentationSize, patch.ConvertTabsToSpaces ?? editor.ConvertTabsToSpaces);
            }
            if (imported.Preview is { Zoom: null, ReadingMode: null, DiagramTheme: null }) throw new InvalidDataException("The preview settings contain no choices.");
            var result = current with
            {
                Theme = imported.Theme ?? current.Theme, Layout = imported.Layout ?? current.Layout,
                ShowOutline = imported.ShowOutline ?? current.ShowOutline, Editor = editor,
                Preview = new(imported.Preview?.Zoom ?? current.Preview.Zoom, imported.Preview?.ReadingMode ?? current.Preview.ReadingMode,
                    imported.Preview?.DiagramTheme ?? current.Preview.DiagramTheme)
            };
            if (!result.IsValid) throw new InvalidDataException("The settings file contains unsupported appearance values.");
            return result;
        }
        catch (JsonException error) { throw new InvalidDataException("The settings JSON is malformed or contains unsupported fields or values.", error); }
    }
    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("The settings JSON contains duplicate fields.");
                if (property.Value.ValueKind == JsonValueKind.Null) throw new InvalidDataException("A supplied settings value cannot be null.");
                Unique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) Unique(child);
    }
}
