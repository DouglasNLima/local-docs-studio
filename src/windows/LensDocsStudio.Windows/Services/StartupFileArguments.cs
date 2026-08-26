namespace LensDocsStudio.Windows.Services;

public static class StartupFileArguments
{
    private static readonly HashSet<string> OptionsWithValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "--smoke-root",
        "--smoke-timeout-seconds",
    };

    public static string? GetStartupFilePath(IEnumerable<string> arguments)
    {
        var tokens = arguments.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList();
        for (var index = 0; index < tokens.Count; index += 1)
        {
            var token = tokens[index];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (OptionsWithValues.Contains(token) && index + 1 < tokens.Count)
                {
                    index += 1;
                }
                continue;
            }

            if (NativeFileService.IsSupportedExtension(Path.GetExtension(token)))
            {
                return token;
            }
        }

        return null;
    }
}
