namespace LensDocsStudio.Windows.Smoke;

public sealed record SmokeOptions(bool Enabled, string? RootPath, int TimeoutSeconds)
{
    public const int DefaultTimeoutSeconds = 60;

    public static SmokeOptions Disabled { get; } = new(false, null, DefaultTimeoutSeconds);

    public static SmokeOptions Parse(string? arguments)
    {
        var tokens = SplitArguments(arguments);
        return ParseTokens(tokens);
    }

    public static SmokeOptions Parse(IEnumerable<string> arguments)
    {
        return ParseTokens(arguments.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList());
    }

    private static SmokeOptions ParseTokens(IReadOnlyList<string> tokens)
    {
        var enabled = false;
        string? rootPath = null;
        var timeoutSeconds = DefaultTimeoutSeconds;

        for (var index = 0; index < tokens.Count; index += 1)
        {
            var token = tokens[index];
            if (string.Equals(token, "--smoke-native-bridge", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                continue;
            }

            if (string.Equals(token, "--smoke-root", StringComparison.OrdinalIgnoreCase) && index + 1 < tokens.Count)
            {
                rootPath = tokens[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(token, "--smoke-timeout-seconds", StringComparison.OrdinalIgnoreCase) && index + 1 < tokens.Count)
            {
                if (int.TryParse(tokens[index + 1], out var parsed) && parsed > 0)
                {
                    timeoutSeconds = Math.Min(parsed, 600);
                }
                index += 1;
            }
        }

        if (!enabled)
        {
            return Disabled;
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("--smoke-root is required when --smoke-native-bridge is enabled.");
        }

        var fullRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRoot))
        {
            throw new InvalidOperationException("The smoke root does not exist.");
        }

        return new SmokeOptions(true, fullRoot, timeoutSeconds);
    }

    public static List<string> SplitArguments(string? arguments)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return tokens;
        }

        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < arguments.Length; index += 1)
        {
            var character = arguments[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddToken(tokens, current);
                continue;
            }

            current.Append(character);
        }

        AddToken(tokens, current);
        return tokens;
    }

    private static void AddToken(List<string> tokens, System.Text.StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }
}
