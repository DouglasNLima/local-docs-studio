using System.Text.RegularExpressions;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Shared exact tokens and conservative group checks for line/block authoring.</summary>
public sealed record SourceField(string Identity, string Name, string Value, int Start, int Length, string Raw)
{
    public SourceText Set(SourceText text, string value) => text.Replace(text.Line(Identity).Replace(Start, Length, value));
}

public static class WorkflowSource
{
    private static readonly Regex Header = SourceText.Pattern(@"^(?:kanban$|requirement(?:Diagram)?$|gitGraph(?:\s|$))", RegexOptions.IgnoreCase);
    public static bool IsWorkflow(string source) => Header.IsMatch(ChartSource.Header(source));
    public static SourceField Field(SourceLine line, string name, Group group, bool unquote = false)
        => new(line.Key, name, unquote ? Unquote(group.Value) : group.Value, group.Index, group.Length, group.Value);
    public static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    public static string Quoted(string value, string? original = null, bool empty = false)
    {
        if (value.Length > 0 || !empty) SourceText.Label(value);
        if (value.IndexOfAny(['"', '\r', '\n']) >= 0 || value.Any(char.IsControl)) throw new ArgumentException("Double quotes and line breaks have no proved escape here. Pending input is retained.");
        return original?.StartsWith('"') == true || value.StartsWith("%%", StringComparison.Ordinal) || value.Length == 0 || value != value.Trim() ? "\"" + value + "\"" : value;
    }
    public static void Rendered(SourceText text, DiagramResult result)
    { if (!result.Success || result.Source != text.Source) throw new ArgumentException(result.Error ?? "Exact draft source did not render."); }
    public static SourceText SetField(SourceText text, IReadOnlyList<SourceField> fields, string name, string value, int insertion, string indent)
    {
        var existing = fields.LastOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing?.Value == value || existing is null && value.Length == 0) return text;
        var encoded = Quoted(value, existing?.Raw, true);
        // An explicit empty final value overrides every earlier repeated property; none is silently revealed.
        return existing is null ? text.Insert(insertion, indent + name + ": " + encoded) : existing.Set(text, encoded);
    }
    public static void CompleteGroup(SourceText text, int start, int end, IEnumerable<string> keys)
    { SourceSections.Safe(text, start, end, keys); SourceSections.CheckTail(text, start, end); }
}
