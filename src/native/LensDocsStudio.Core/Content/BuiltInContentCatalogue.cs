using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Content;

public sealed record CatalogueField(string Key, string Label, string DefaultValue);
public sealed record CatalogueEntry(string Id, string Group, string Label, string Kind, string Format, string? NamePattern,
    string ContentPattern, IReadOnlyList<CatalogueField> Fields)
{
    public bool IsSnippet => Kind == "snippet";
    public string Description => Group + " · " + (IsSnippet ? "Snippet" : Format == "mermaid" ? "Mermaid diagram" : "Document template");
}

/// <summary>Bundled, reviewed source templates. No script engine or filesystem access is involved in generation.</summary>
public static partial class BuiltInContentCatalogue
{
    public const int MaximumFieldCharacters = 512;
    private static readonly Lazy<IReadOnlyList<CatalogueEntry>> catalogue = new(Read);
    public static IReadOnlyList<CatalogueEntry> Entries => catalogue.Value;
    private sealed record CatalogueData(int Version, CatalogueEntry[] Entries);

    private static IReadOnlyList<CatalogueEntry> Read()
    {
        using var stream = typeof(BuiltInContentCatalogue).Assembly.GetManifestResourceStream("Content.built-in-catalogue.json")
            ?? throw new InvalidDataException("The built-in content catalogue is unavailable.");
        var data = JsonSerializer.Deserialize<CatalogueData>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The built-in content catalogue is empty.");
        if (data.Version != 1 || data.Entries.Length > 200 || data.Entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != data.Entries.Length)
            throw new InvalidDataException("The built-in content catalogue version or entries are invalid.");
        foreach (var entry in data.Entries)
        {
            if (entry.Kind is not ("template" or "snippet") || entry.Format is not ("markdown" or "mermaid") || entry.Fields.Count > 12
                || entry.ContentPattern.Length > 32768 || entry.Fields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count() != entry.Fields.Count)
                throw new InvalidDataException("A built-in content entry is invalid: " + entry.Id);
            foreach (Match token in FieldToken().Matches(entry.ContentPattern + entry.NamePattern))
                if (!entry.Fields.Any(field => field.Key == token.Groups[2].Value)) throw new InvalidDataException("A built-in content field is undefined: " + token.Value);
        }
        return Array.AsReadOnly(data.Entries.Select(entry => entry with { Fields = Array.AsReadOnly(entry.Fields.ToArray()) }).ToArray());
    }

    public static IReadOnlyDictionary<string, string> Defaults(CatalogueEntry entry, DateOnly? date = null)
    {
        RequireBundledEntry(entry);
        var today = (date ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return entry.Fields.ToDictionary(field => field.Key, field => field.DefaultValue == "{{today}}" ? today : field.DefaultValue, StringComparer.Ordinal);
    }

    public static LocalLibraryItem Generate(CatalogueEntry entry, IReadOnlyDictionary<string, string>? values = null, DateOnly? date = null)
    {
        var fields = new Dictionary<string, string>(Defaults(entry, date), StringComparer.Ordinal);
        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                if (!fields.ContainsKey(key)) throw new ArgumentException("The template does not define field: " + key, nameof(values));
                if (value is null || value.Length > MaximumFieldCharacters || value.Any(char.IsControl))
                    throw new ArgumentException("Use one line of up to 512 characters for " + entry.Fields.Single(field => field.Key == key).Label + ".", nameof(values));
                if (!string.IsNullOrWhiteSpace(value)) fields[key] = value.Trim();
            }
        }
        // Regex replacement visits only the original pattern, so field values containing tokens stay literal.
        string Expand(string pattern) => FieldToken().Replace(pattern, match => match.Groups[1].Success ? Slug(fields[match.Groups[2].Value]) : fields[match.Groups[2].Value]);
        var name = entry.NamePattern is null ? null : Expand(entry.NamePattern);
        if (name is not null && ReservedName().IsMatch(Path.GetFileNameWithoutExtension(name))) name = "document-" + name;
        var result = new LocalLibraryItem("built-in-" + entry.Id.Replace('/', '-'), entry.Label, Expand(entry.ContentPattern), 0, name);
        // Reuse the portable library contract, including strict Unicode and safe save suggestions.
        _ = LocalLibraryFormat.Write(entry.IsSnippet ? new([], [result], []) : new([result], [], []));
        return result;
    }

    private static void RequireBundledEntry(CatalogueEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!Entries.Any(candidate => ReferenceEquals(candidate, entry))) throw new ArgumentException("Choose a bundled catalogue entry.", nameof(entry));
    }
    private static string Slug(string value)
    {
        var slug = SlugSeparators().Replace(value.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 96) slug = slug[..96].TrimEnd('-');
        return slug.Length == 0 ? "document" : slug;
    }
    [GeneratedRegex(@"\{\{(slug:)?([a-zA-Z][a-zA-Z0-9]*)\}\}", RegexOptions.CultureInvariant, 1000)] private static partial Regex FieldToken();
    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant, 1000)] private static partial Regex SlugSeparators();
    [GeneratedRegex(@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)] private static partial Regex ReservedName();
}
