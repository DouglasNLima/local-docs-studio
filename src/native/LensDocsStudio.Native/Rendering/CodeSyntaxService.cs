using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Rendering;

public static class CodeSyntaxService
{
    public const int MaximumBlockCharacters = 32 * 1024;
    public const int MaximumLineCharacters = 4096;
    public const int MaximumDocumentCharacters = 1024 * 1024;
    public const int MaximumBlockSpans = CodeSyntaxBlock.MaximumSpans;
    public const int MaximumDocumentSpans = 16 * 1024;
    private const int MaximumColourAssignments = 4 * MaximumDocumentCharacters;
    private static readonly ConcurrentDictionary<string, Lazy<HighlightingRuleSet?>> Rules = new(StringComparer.Ordinal);

    // Only definitions bundled with the pinned AvalonEdit dependency are used.
    // Document text cannot supply definitions, expressions or resource locations.
    public static PreparedCodeSyntax Prepare(ParsedDocument parsed, CancellationToken cancellationToken = default, ContainerBlock? scope = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocks = new Dictionary<CodeBlock, CodeSyntaxBlock>();
        var characters = 0; var spanCount = 0; var assignments = 0; var limited = false;
        var watch = Stopwatch.StartNew();
        foreach (var code in (scope ?? parsed.Syntax).Descendants<FencedCodeBlock>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definitionName = DefinitionName(code.Info);
            if (definitionName is null) continue;
            if (watch.ElapsedMilliseconds >= 250 || characters >= MaximumDocumentCharacters || spanCount >= MaximumDocumentSpans)
            { limited = true; break; }
            // The source span includes its fence; conservatively leave large blocks plain before copying their text.
            if (code.Span.Length > MaximumBlockCharacters) { limited = true; continue; }
            var text = code.Lines.ToString();
            if (text.Length > MaximumBlockCharacters || text.Length > MaximumDocumentCharacters - characters)
            { limited = true; continue; }
            characters += text.Length;
            var document = new TextDocument(text);
            if (document.Lines.Any(line => line.Length > MaximumLineCharacters)) { limited = true; continue; }
            // First use compiles trusted bundled definitions on this worker; that one-off cost
            // is separate from the budget for processing untrusted document text.
            watch.Stop();
            var rules = Rules.GetOrAdd(definitionName, name => new(() => HighlightingManager.Instance.GetDefinition(name) is { } definition
                ? BoundedCodeRules.Create(definition.MainRuleSet) : null)).Value;
            watch.Start();
            if (rules is null) continue;
            var roles = new CodeTokenRole[text.Length]; var sections = 0; var complete = true;
            var highlighter = new HighlightingEngine(rules);
            foreach (var line in document.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (watch.ElapsedMilliseconds >= 250) { complete = false; break; }
                HighlightedLine highlighted;
                try { highlighted = highlighter.HighlightLine(document, line); }
                catch (RegexMatchTimeoutException) { complete = false; break; }
                sections += highlighted.Sections.Count;
                if (sections > MaximumBlockSpans) { complete = false; break; }
                // Sections are ordered outer-before-inner. Known nested roles replace their parent;
                // uncoloured nested decorations retain the parent's role.
                foreach (var section in highlighted.Sections)
                {
                    var mapped = Role(section.Color.Name);
                    // Several bundled definitions use anonymous foreground styles for tokens.
                    // Give those a fixed readable colour without importing the definition's palette.
                    if (mapped is null && section.Color.Foreground is not null) mapped = CodeTokenRole.Keyword;
                    if (mapped is not { } role) continue;
                    if (section.Offset < 0 || section.Length < 0 || section.Offset > text.Length - section.Length
                        || section.Length > MaximumColourAssignments - assignments) { complete = false; break; }
                    assignments += section.Length;
                    Array.Fill(roles, role, section.Offset, section.Length);
                }
                if (!complete) break;
            }
            if (!complete) { limited = true; continue; }
            var spans = new List<CodeSyntaxSpan>();
            for (var start = 0; start < roles.Length;)
            {
                var end = start + 1;
                while (end < roles.Length && roles[end] == roles[start]) end++;
                if (roles[start] != CodeTokenRole.Plain) spans.Add(new(start, end - start, roles[start]));
                start = end;
            }
            if (spans.Count > MaximumBlockSpans || spans.Count > MaximumDocumentSpans - spanCount)
            { limited = true; continue; }
            spanCount += spans.Count;
            if (spans.Count != 0) blocks.Add(code, new(text, spans.ToArray()));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(blocks, limited);
    }

    private static string? DefinitionName(string? language) => (language is { Length: <= 64 } ? language.Trim().ToLowerInvariant() : null) switch
    {
        "cs" or "csharp" or "c#" => "C#", "js" or "javascript" => "JavaScript", "json" => "Json",
        "py" or "python" => "Python", "xml" or "xaml" => "XML", "html" or "htm" => "HTML", "css" => "CSS",
        "ps1" or "powershell" => "PowerShell", "sql" or "tsql" => "TSQL", "java" => "Java",
        "c" or "cpp" or "c++" => "C++", "php" => "PHP", "vb" or "vbnet" => "VB", "diff" or "patch" => "Patch",
        _ => null
    };

    private static CodeTokenRole? Role(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        name = name.ToLowerInvariant();
        if (name.Contains("comment", StringComparison.Ordinal)) return CodeTokenRole.Comment;
        if (name == "stringinterpolation") return CodeTokenRole.Plain;
        if (name.Contains("string", StringComparison.Ordinal) || name.Contains("char", StringComparison.Ordinal)
            || name.Contains("attributevalue", StringComparison.Ordinal)) return CodeTokenRole.String;
        if (name.Contains("number", StringComparison.Ordinal) || name.Contains("digit", StringComparison.Ordinal)) return CodeTokenRole.Number;
        if (name.Contains("keyword", StringComparison.Ordinal) || name.Contains("modifier", StringComparison.Ordinal)
            || name is "visibility" or "truefalse" or "boolean" or "null" or "getsetaddremove" or "thisorbasereference") return CodeTokenRole.Keyword;
        if (name.Contains("method", StringComparison.Ordinal) || name.Contains("tag", StringComparison.Ordinal)
            || name.Contains("attribute", StringComparison.Ordinal) || name.Contains("name", StringComparison.Ordinal)) return CodeTokenRole.Name;
        return null;
    }
}
