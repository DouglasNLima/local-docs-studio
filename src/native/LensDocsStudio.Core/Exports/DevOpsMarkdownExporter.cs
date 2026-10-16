using System.Text;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Exports;

/// <summary>Source-preserving Azure DevOps Mermaid conversion for explicit Markdown bundle export.</summary>
public static partial class DevOpsMarkdownExporter
{
    public static string Convert(string source, string filename, CancellationToken cancellationToken = default)
    {
        if (Path.GetExtension(filename).ToLowerInvariant() is not (".md" or ".markdown")) return source;
        var edits = new List<(int Start, int Length, string Value)>();
        char fence = '\0'; var fenceLength = 0; var mermaid = false; var devops = false;
        var opening = (Start: 0, Length: 0, Prefix: "");
        (int Start, int Length, string Value)? declaration = null; var firstBodyLine = false;
        var start = 0;
        while (start < source.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var newline = source.IndexOf('\n', start); var end = newline < 0 ? source.Length : newline;
            var contentEnd = end > start && source[end - 1] == '\r' ? end - 1 : end;
            var line = source[start..contentEnd];
            if (devops)
            {
                if (DevOpsClose().IsMatch(line)) Finish(line);
                else Declaration(line);
            }
            else if (fence != '\0')
            {
                var close = FenceClose().Match(line);
                if (close.Success && close.Groups[2].Value[0] == fence && close.Groups[2].Length >= fenceLength) Finish(line);
                else if (mermaid) Declaration(line);
            }
            else
            {
                var standard = FenceOpen().Match(line);
                var special = DevOpsOpen().Match(line);
                if (standard.Success)
                {
                    fence = standard.Groups[2].Value[0]; fenceLength = standard.Groups[2].Length;
                    mermaid = standard.Groups[3].Value.Equals("mermaid", StringComparison.OrdinalIgnoreCase) || standard.Groups[3].Value.Equals("mmd", StringComparison.OrdinalIgnoreCase);
                    opening = (start, line.Length, standard.Groups[1].Value); declaration = null; firstBodyLine = true;
                }
                else if (special.Success)
                {
                    devops = mermaid = true; opening = (start, line.Length, special.Groups[1].Value); declaration = null; firstBodyLine = true;
                }
            }
            start = newline < 0 ? source.Length : newline + 1;

            void Declaration(string bodyLine)
            {
                if (!firstBodyLine || string.IsNullOrWhiteSpace(bodyLine)) return;
                firstBodyLine = false;
                var flowchart = Flowchart().Match(bodyLine);
                if (flowchart.Success) declaration = (start, bodyLine.Length, flowchart.Groups[1].Value + "graph" + flowchart.Groups[2].Value);
            }
            void Finish(string closingLine)
            {
                if (mermaid)
                {
                    edits.Add((opening.Start, opening.Length, opening.Prefix + "::: mermaid"));
                    if (declaration is { } graph) edits.Add(graph);
                    var leading = closingLine.Length - closingLine.TrimStart(' ', '\t').Length;
                    edits.Add((start, closingLine.Length, closingLine[..leading] + ":::"));
                }
                fence = '\0'; mermaid = devops = false; declaration = null;
            }
        }
        if (edits.Count == 0) return source;
        var result = new StringBuilder(source.Length); var position = 0;
        foreach (var edit in edits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Append(source, position, edit.Start - position).Append(edit.Value); position = edit.Start + edit.Length;
        }
        return result.Append(source, position, source.Length - position).ToString();
    }

    [GeneratedRegex(@"^( {0,3})(`{3,}|~{3,})[ \t]*([^`~\s]*)", RegexOptions.CultureInvariant, 1000)] private static partial Regex FenceOpen();
    [GeneratedRegex(@"^( {0,3})(`{3,}|~{3,})[ \t]*$", RegexOptions.CultureInvariant, 1000)] private static partial Regex FenceClose();
    [GeneratedRegex(@"^([ \t]*):::[ \t]*mermaid[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)] private static partial Regex DevOpsOpen();
    [GeneratedRegex(@"^[ \t]*:::[ \t]*$", RegexOptions.CultureInvariant, 1000)] private static partial Regex DevOpsClose();
    [GeneratedRegex(@"^([ \t]*)flowchart([ \t]+(?:TB|TD|BT|RL|LR)[ \t]*;?[ \t]*|[ \t]*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)] private static partial Regex Flowchart();
}
