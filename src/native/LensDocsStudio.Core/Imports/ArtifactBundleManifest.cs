using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Imports;

public sealed record ArtifactEntry(string Path, string Title, string Kind, string EvidenceLevel, double? Order, bool IsDiagram)
{
    public string Group => ArtifactBundleManifest.Group(this);
    public string DisplayTitle => Title.Length == 0 ? Path : Title;
}
public sealed record ArtifactWarning(string Code, string Message, string EvidenceLevel);
public sealed record ArtifactBundle(string Title, string Summary, string SourceTool, string SourceToolVersion, string GeneratedAtUtc,
    string EntryDocument, IReadOnlyList<string> EvidenceLevels, IReadOnlyList<ArtifactEntry> Documents,
    IReadOnlyList<ArtifactEntry> Diagrams, IReadOnlyList<ArtifactWarning> Warnings, IReadOnlyList<string> MetadataWarnings)
{
    public IEnumerable<ArtifactEntry> Entries => Documents.Concat(Diagrams).DistinctBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
    public ArtifactEntry? Find(string path) => Entries.FirstOrDefault(entry => entry.Path == path);
    public bool HasCandidateFindings => EvidenceLevels.Contains("candidate finding", StringComparer.Ordinal);
}
public sealed record ArtifactManifestResult(ArtifactBundle? Bundle, IReadOnlyList<string> Warnings);

/// <summary>Untrusted ZIP display metadata. It cannot confer access or verify evidence.</summary>
public static partial class ArtifactBundleManifest
{
    public const string Filename = "lens-artifact-bundle.json";
    public const string FormatVersion = "lens-artifact-bundle-1.0";
    public const int MaximumBytes = 256 * 1024;
    public static IReadOnlyList<string> EvidenceLabels { get; } = Array.AsReadOnly(new[] { "static evidence", "connected metadata evidence", "runtime evidence",
        "manual evidence", "candidate finding", "confirmed finding", "blocked/unavailable evidence" });
    public static IReadOnlyList<string> Groups { get; } = Array.AsReadOnly(new[] { "Summary", "Findings", "Diagrams", "ADRs", "Release notes", "Documentation", "Other" });

    public static ArtifactManifestResult Parse(ReadOnlyMemory<byte> bytes, IEnumerable<string> importedDocuments)
    {
        if (bytes.Length > MaximumBytes) return new(null, ["Artefact bundle manifest is too large."]);
        if (bytes.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) bytes = bytes[3..];
        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(bytes.Span);
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 }); var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new(null, ["Artefact bundle manifest could not be read."]);
            if (String(Field(root, "formatVersion")) != FormatVersion) return new(null, ["Artefact bundle manifest format is not supported."]);
            var paths = importedDocuments.Where(IsArtifactDocument).ToHashSet(StringComparer.Ordinal);
            var warnings = new List<string>();
            var documents = ReadEntries(Field(root, "documents"), false); var diagrams = ReadEntries(Field(root, "diagrams"), true);
            var suppliedWarnings = new List<ArtifactWarning>(); var rawWarnings = Field(root, "warnings");
            if (rawWarnings.ValueKind == JsonValueKind.Array)
                foreach (var item in rawWarnings.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var warning = new ArtifactWarning(Text(Field(item, "code")), Text(Field(item, "message"), 420), Evidence(Field(item, "evidenceLevel")));
                    if (warning.Code.Length + warning.Message.Length + warning.EvidenceLevel.Length > 0) suppliedWarnings.Add(warning);
                    if (suppliedWarnings.Count == 20) break;
                }
            else if (rawWarnings.ValueKind != JsonValueKind.Undefined) warnings.Add("Warning metadata was ignored.");
            var entryField = Field(root, "entryDocument"); var entry = NormalisePath(String(entryField));
            if (entryField.ValueKind != JsonValueKind.Undefined)
            {
                if (entry.Length == 0) warnings.Add("Entry document path was ignored.");
                else if (!paths.Contains(entry)) { warnings.Add("Entry document was not found."); entry = ""; }
            }
            var levels = new List<string>(); var declared = Field(root, "evidenceLevels");
            if (declared.ValueKind == JsonValueKind.Array) foreach (var level in declared.EnumerateArray()) AddEvidence(Evidence(level));
            else if (declared.ValueKind != JsonValueKind.Undefined) warnings.Add("Evidence level metadata was ignored.");
            foreach (var item in documents.Concat(diagrams)) AddEvidence(item.EvidenceLevel);
            foreach (var warning in suppliedWarnings) AddEvidence(warning.EvidenceLevel);
            var generated = String(Field(root, "generatedAtUtc")).Trim();
            generated = generated.EndsWith('Z') && DateTimeOffset.TryParse(generated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
                ? timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) : "";
            var limitedWarnings = warnings.Distinct(StringComparer.Ordinal).Take(8).ToArray();
            var bundle = new ArtifactBundle(Text(Field(root, "title")), Text(Field(root, "summary"), 420), Text(Field(root, "sourceTool")),
                Text(Field(root, "sourceToolVersion")), generated, entry, levels.AsReadOnly(), documents.AsReadOnly(), diagrams.AsReadOnly(), suppliedWarnings.AsReadOnly(), Array.AsReadOnly(limitedWarnings));
            return new(bundle, bundle.MetadataWarnings);

            void AddEvidence(string level) { if (level.Length > 0 && !levels.Contains(level, StringComparer.Ordinal)) levels.Add(level); }
            List<ArtifactEntry> ReadEntries(JsonElement value, bool diagram)
            {
                var result = new List<ArtifactEntry>(); var name = diagram ? "Diagram" : "Document";
                if (value.ValueKind == JsonValueKind.Undefined) return result;
                if (value.ValueKind != JsonValueKind.Array) { warnings.Add(name + " metadata was ignored."); return result; }
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var unsafePath = false; var missingPath = false;
                foreach (var item in value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var path = NormalisePath(String(Field(item, "path")));
                    if (path.Length == 0) { unsafePath = true; continue; }
                    if (!paths.Contains(path)) { missingPath = true; continue; }
                    if (!seen.Add(path)) continue;
                    result.Add(new(path, Text(Field(item, "title")), Text(Field(item, "kind")), Evidence(Field(item, "evidenceLevel")), Order(Field(item, "order")), diagram));
                }
                if (unsafePath) warnings.Add(name + " metadata paths were ignored.");
                if (missingPath) warnings.Add(name + " metadata paths were not found in the ZIP.");
                return result;
            }
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException) { return new(null, ["Artefact bundle manifest could not be read."]); }
    }

    public static string NormalisePath(string? value)
    {
        if (value is null) return ""; var path = value.Trim().Replace('\\', '/');
        if (path.Length == 0 || path.StartsWith('/') || path.Contains(':') || path.Any(character => character < 32 || character == 127)) return "";
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 || parts.Any(part => part is "." or "..") ? "" : string.Join('/', parts);
    }
    public static bool IsArtifactDocument(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown" or ".mmd" or ".mermaid";
    public static string SafeExportText(string value, int maximum = 180)
    {
        var text = NormaliseText(value, maximum); return Scheme().IsMatch(text) ? "" : text;
    }
    public static string Group(ArtifactEntry entry)
    {
        var kind = entry.Kind.ToLowerInvariant(); var path = entry.Path.ToLowerInvariant();
        bool Match(string value, string pattern) => Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant);
        if (entry.IsDiagram || Match(kind, @"\b(diagram|mermaid|mmd)\b") || System.IO.Path.GetExtension(path) is ".mmd" or ".mermaid") return "Diagrams";
        if (Match(kind, @"\bfinding") || Match(path, @"(^|/)findings?/")) return "Findings";
        if (Match(kind, @"\badr\b|architecture decision") || Match(path, @"(^|/)adrs?/|adr-\d+")) return "ADRs";
        if (kind.Contains("release", StringComparison.Ordinal) || Match(path, "release[- ]?notes")) return "Release notes";
        if (Match(kind, "summary|overview") || Match(path, @"(^|/)(readme|index)\.(md|markdown)$")) return "Summary";
        if (Match(kind, "doc|guide|runbook|manual") || Match(path, @"(^|/)docs?/")) return "Documentation";
        return "Other";
    }

    public static IReadOnlyDictionary<string, object> BuildSiteMetadata(ArtifactBundle bundle, IEnumerable<string> exportedPaths)
    {
        var metadata = new Dictionary<string, object>();
        foreach (var (key, value) in new[] { ("title", bundle.Title), ("sourceTool", bundle.SourceTool), ("sourceToolVersion", bundle.SourceToolVersion), ("generatedAtUtc", bundle.GeneratedAtUtc) })
        { var safe = SafeExportText(value); if (safe.Length > 0) metadata[key] = safe; }
        if (exportedPaths.Contains(bundle.EntryDocument, StringComparer.Ordinal)) metadata["entryDocument"] = bundle.EntryDocument;
        var levels = bundle.EvidenceLevels.Select(Evidence).Where(level => level.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (levels.Length > 0) metadata["evidenceLevels"] = levels;
        var notes = new List<string> { "Evidence labels are displayed as supplied by the bundle." };
        if (bundle.HasCandidateFindings) { notes.Add("Candidate findings remain candidate findings."); metadata["candidateFindings"] = true; }
        metadata["notes"] = notes;
        return metadata;
    }

    public static byte[] BuildReviewManifest(ArtifactBundle bundle, IEnumerable<string> exportedPaths)
    {
        var paths = exportedPaths.Select(NormalisePath).Where(path => path.Length > 0 && IsArtifactDocument(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var documents = new List<Dictionary<string, object>>(); var diagrams = new List<Dictionary<string, object>>();
        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index]; var source = bundle.Find(path);
            var entry = new Dictionary<string, object> { ["path"] = path, ["order"] = source?.Order is { } order && double.IsFinite(order) ? order : index + 1 };
            AddText(entry, "title", source?.Title ?? ""); AddText(entry, "kind", source?.Kind ?? ""); AddText(entry, "evidenceLevel", Evidence(source?.EvidenceLevel ?? ""));
            (source?.IsDiagram == true || System.IO.Path.GetExtension(path).ToLowerInvariant() is ".mmd" or ".mermaid" ? diagrams : documents).Add(entry);
        }
        var manifest = new Dictionary<string, object> { ["formatVersion"] = FormatVersion, ["reviewedWith"] = "Lens Docs Studio" };
        AddText(manifest, "title", bundle.Title); AddText(manifest, "sourceTool", bundle.SourceTool); AddText(manifest, "sourceToolVersion", bundle.SourceToolVersion);
        AddText(manifest, "generatedAtUtc", bundle.GeneratedAtUtc);
        if (paths.Contains(bundle.EntryDocument, StringComparer.Ordinal)) manifest["entryDocument"] = bundle.EntryDocument;
        var evidence = bundle.EvidenceLevels.Select(Evidence).Where(level => level.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (evidence.Length > 0) manifest["evidenceLevels"] = evidence;
        if (documents.Count > 0) manifest["documents"] = documents; if (diagrams.Count > 0) manifest["diagrams"] = diagrams;
        var warnings = bundle.Warnings.Take(20).Select(warning =>
        {
            var item = new Dictionary<string, object>(); AddText(item, "code", warning.Code); AddText(item, "message", warning.Message, 420);
            AddText(item, "evidenceLevel", Evidence(warning.EvidenceLevel)); return item;
        }).Where(item => item.Count > 0).ToArray();
        if (warnings.Length > 0) manifest["warnings"] = warnings;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > MaximumBytes) throw new IOException("The rebuilt artefact manifest exceeds 256 KB. Export a smaller review workspace.");
        return bytes;
        static void AddText(Dictionary<string, object> target, string key, string value, int maximum = 180)
        { var safe = SafeExportText(value, maximum); if (safe.Length > 0) target[key] = safe; }
    }

    private static JsonElement Field(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value : default;
    private static string String(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) return "";
        try { return value.GetString() ?? ""; }
        catch (InvalidOperationException ex) { throw new JsonException("The manifest contains invalid encoded text.", ex); }
    }
    private static string Text(JsonElement value, int maximum = 180) => NormaliseText(String(value), maximum);
    private static string NormaliseText(string value, int maximum)
    {
        var text = Whitespace().Replace(value, " ").Trim(); if (text.Length <= maximum) return text;
        return text[..(char.IsHighSurrogate(text[maximum - 1]) ? maximum - 1 : maximum)];
    }
    private static string Evidence(JsonElement value) => Evidence(String(value));
    private static string Evidence(string value) { value = value.Trim(); return EvidenceLabels.Contains(value, StringComparer.Ordinal) ? value : ""; }
    private static double? Order(JsonElement value)
    {
        double number;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDouble(out number) && double.IsFinite(number) ? number : null;
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.False) return 0;
        if (value.ValueKind == JsonValueKind.True) return 1;
        if (value.ValueKind != JsonValueKind.String) return null;
        var text = String(value).Trim(); if (text.Length == 0) return 0;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number) ? number : null;
    }
    [GeneratedRegex(@"[\u0000-\u001f\u007f\s]+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Scheme();
}
