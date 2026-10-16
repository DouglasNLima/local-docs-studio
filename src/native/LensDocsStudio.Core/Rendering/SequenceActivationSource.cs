using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Rendering;

/// <summary>Adapts message-bound activation commands to the pinned renderer's supported shorthand without changing saved source.</summary>
internal static partial class SequenceActivationSource
{
    public static string Prepare(string source)
    {
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        var header = Array.FindIndex(lines, line => !Ignorable(line));
        if (header < 0 || lines[header].Trim() != "sequenceDiagram") return source;
        var changed = false;
        for (var index = header + 1; index < lines.Length; index++)
        {
            var command = Command().Match(lines[index]);
            if (!command.Success)
            {
                if (CommandStart().IsMatch(lines[index]))
                    throw new InvalidDataException("Use one sequence activation command and actor per line. The original source is preserved.");
                continue;
            }
            var actor = command.Groups["actor"].Value;
            var activate = command.Groups["verb"].Value == "activate";
            var previous = index - 1;
            while (previous > header && Ignorable(lines[previous])) previous--;
            var message = Message().Match(lines[previous]);
            if (!message.Success || message.Groups["mark"].Length != 0
                || message.Groups[activate ? "to" : "from"].Value != actor)
                throw new InvalidDataException("This sequence activation must immediately follow a message to that actor (activate) or from that actor (deactivate), without another activation marker. The original source is preserved.");
            lines[previous] = lines[previous].Insert(message.Groups["to"].Index, activate ? "+" : "-");
            lines[index] = "";
            changed = true;
        }
        return changed ? string.Join('\n', lines) : source;
    }

    private static bool Ignorable(string line) => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("%%", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*(?<verb>activate|deactivate)\s+(?<actor>\S+)\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Command();

    [GeneratedRegex(@"^\s*(?:activate|deactivate)(?:\s|$)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CommandStart();

    [GeneratedRegex(@"^\s*(?<from>\S+?)\s*(?<arrow>->>|-->>|-\)|--\)|-x|--x|->|-->)\s*(?<mark>[+-]?)(?<to>\S+?)\s*:\s*(?<label>.+)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Message();
}
