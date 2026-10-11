using System.Text;
using System.Text.Json;

namespace LensDocsStudio.Core.State;

public sealed record LocalLibraryItem(string Id, string Label, string Content, long CreatedAt, string? Name = null);
public sealed record LocalDocsSiteProfile(string Title, string Description, string Theme = "system");
public sealed record LocalExportProfile(string Id, string Label, bool DevopsMarkdownExport, LocalDocsSiteProfile? DocsSite, long CreatedAt);
public sealed record LocalLibraryData(LocalLibraryItem[] Templates, LocalLibraryItem[] Snippets, LocalExportProfile[] Profiles)
{
    public static LocalLibraryData Empty => new([], [], []);
}
public sealed record LocalLibraryMerge(LocalLibraryData Library, int Added, int AlreadyPresent, int ReassignedIds);

/// <summary>Explicit interchange with the browser's exported JSON; content and metadata are inert data.</summary>
public static class LocalLibraryFormat
{
    public const string FormatVersion = "lens-docs-studio-library-1.0";
    public const int MaximumFileBytes = 16 * 1024 * 1024;
    public const int MaximumContentBytes = 1024 * 1024;
    public const int MaximumAggregateBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 16,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static LocalLibraryData Read(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("The library JSON exceeds 16 MB.");
        if (bytes.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) bytes = bytes[3..];
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            ValidateUniqueProperties(root);
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("formatVersion", out var version)
                || version.ValueKind != JsonValueKind.String || version.GetString() != FormatVersion)
                throw new InvalidDataException("Choose a Lens Docs Studio local library JSON export (version 1.0).");
            var library = new LocalLibraryData(Items<LocalLibraryItem>("templates"), Items<LocalLibraryItem>("snippets"), Items<LocalExportProfile>("profiles"));
            Validate(library); return library;

            T[] Items<T>(string name)
            {
                if (!root.TryGetProperty(name, out var items)) return [];
                if (items.ValueKind != JsonValueKind.Array) throw new InvalidDataException("The library's " + name + " must be a JSON array.");
                if (items.GetArrayLength() > (name == "templates" ? 200 : name == "snippets" ? 500 : 100))
                    throw new InvalidDataException("The imported library contains too many " + name + ".");
                return items.Deserialize<T[]>(Json) ?? throw new InvalidDataException("The library's " + name + " are missing.");
            }
        }
        catch (JsonException exception) { throw new InvalidDataException("The library JSON is malformed or contains an unsupported value.", exception); }
    }

    public static byte[] Write(LocalLibraryData library, DateTimeOffset? exportedAt = null)
    {
        Validate(library);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = FormatVersion, exportedAt = (exportedAt ?? DateTimeOffset.UtcNow).ToString("O"),
            library.Templates, library.Snippets, library.Profiles
        }, Json);
        if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("The exported library JSON exceeds 16 MB. Export fewer entries.");
        return bytes;
    }

    public static void Validate(LocalLibraryData library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (library.Templates is null || library.Snippets is null || library.Profiles is null
            || library.Templates.Length > 200 || library.Snippets.Length > 500 || library.Profiles.Length > 100)
            throw new InvalidDataException("A library supports up to 200 templates, 500 snippets and 100 export profiles.");
        long total = 0;
        ValidateItems(library.Templates, true); ValidateItems(library.Snippets, false);
        var profiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in library.Profiles)
        {
            if (profile is null) throw new InvalidDataException("The library contains an empty export profile.");
            Identity(profile.Id, profile.Label, profile.CreatedAt, profiles);
            if (profile.DocsSite is { } site)
            {
                Label(site.Title, 512, "Docs Site title");
                if (site.Description is null || site.Description.Length > 2048 || site.Description.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
                    throw new InvalidDataException("Use a Docs Site description of up to 2,048 characters without control characters.");
                if (site.Theme is not ("system" or "light" or "dark")) throw new InvalidDataException("The Docs Site theme must be system, light or dark.");
            }
        }
        if (total > MaximumAggregateBytes) throw new InvalidDataException("The library's Markdown content exceeds 8 MB in total.");

        void ValidateItems(LocalLibraryItem[] items, bool template)
        {
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item is null) throw new InvalidDataException("The library contains an empty content entry.");
                Identity(item.Id, item.Label, item.CreatedAt, identifiers);
                if (string.IsNullOrEmpty(item.Content) || item.Content.Contains('\0')) throw new InvalidDataException("Each template or snippet needs Markdown text without null characters.");
                int size;
                try { size = Utf8.GetByteCount(item.Content); }
                catch (EncoderFallbackException exception) { throw new InvalidDataException("A library entry contains invalid Unicode.", exception); }
                if (size > MaximumContentBytes) throw new InvalidDataException("Each library template or snippet supports up to 1 MB of UTF-8 text.");
                total += size;
                if (template && item.Name is not null) ValidateSuggestedName(item.Name);
            }
        }
    }

    public static LocalLibraryMerge Merge(LocalLibraryData current, LocalLibraryData imported, LocalLibraryData? archived = null)
    {
        Validate(current); Validate(imported); archived ??= LocalLibraryData.Empty; Validate(archived);
        var added = 0; var duplicate = 0; var reassigned = 0;
        var templates = MergeItems(current.Templates, imported.Templates, archived.Templates, "template");
        var snippets = MergeItems(current.Snippets, imported.Snippets, archived.Snippets, "snippet");
        var profiles = current.Profiles.ToList(); var reserved = profiles.Concat(archived.Profiles).ToList();
        var profileIds = reserved.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in imported.Profiles)
        {
            if (reserved.Any(other => other with { Id = item.Id } == item)) { duplicate++; continue; }
            var next = item;
            if (!profileIds.Add(item.Id)) { next = item with { Id = FreshId("profile", profileIds) }; reassigned++; }
            profiles.Add(next); reserved.Add(next); added++;
        }
        var result = new LocalLibraryData(templates, snippets, profiles.ToArray()); Validate(result);
        return new(result, added, duplicate, reassigned);

        LocalLibraryItem[] MergeItems(LocalLibraryItem[] existing, LocalLibraryItem[] incoming, LocalLibraryItem[] retired, string prefix)
        {
            var list = existing.ToList(); var seen = existing.Concat(retired).ToList(); var ids = seen.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var item in incoming)
            {
                if (seen.Any(other => other with { Id = item.Id } == item)) { duplicate++; continue; }
                var next = item;
                if (!ids.Add(item.Id)) { next = item with { Id = FreshId(prefix, ids) }; reassigned++; }
                list.Add(next); seen.Add(next); added++;
            }
            return list.ToArray();
        }
    }

    private static string FreshId(string prefix, HashSet<string> ids)
    { string id; do { id = prefix + "-" + Guid.NewGuid().ToString("N"); } while (!ids.Add(id)); return id; }

    private static void Identity(string id, string label, long createdAt, HashSet<string> ids)
    {
        Label(id, 160, "Library identifier"); Label(label, 160, "Library name");
        if (!ids.Add(id)) throw new InvalidDataException("The library contains repeated identifiers. Existing entries have not been changed.");
        if (createdAt < 0 || createdAt > 253402300799999L) throw new InvalidDataException("A library creation date is outside the supported range.");
    }
    internal static void Label(string? text, int maximum, string kind)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl))
            throw new InvalidDataException($"{kind} must contain 1–{maximum} characters without line breaks or control characters.");
    }
    private static void ValidateSuggestedName(string name)
    {
        Label(name, 160, "Template filename");
        if (name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || name.EndsWith('.') || name.EndsWith(' ')
            || Path.GetExtension(name).ToLowerInvariant() is not (".md" or ".markdown" or ".mmd" or ".mermaid" or ".txt"))
            throw new InvalidDataException("A template filename must be a plain Markdown, Mermaid or text filename, without a folder or drive path.");
        var stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9')
            throw new InvalidDataException("The template filename is reserved by Windows.");
    }
    internal static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("The library JSON contains repeated property names.");
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateUniqueProperties(item);
    }
}
