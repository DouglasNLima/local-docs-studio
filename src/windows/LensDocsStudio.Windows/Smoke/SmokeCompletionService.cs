using System.Text.Json;

namespace LensDocsStudio.Windows.Smoke;

public sealed class SmokeCompletionService
{
    private readonly SmokeOptions options;

    public SmokeCompletionService(SmokeOptions options)
    {
        this.options = options;
    }

    public object Complete(JsonElement? payload)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.RootPath))
        {
            return new
            {
                completed = false,
                accepted = false,
                message = "Smoke completion is unavailable.",
            };
        }

        var success = payload is { ValueKind: JsonValueKind.Object } value
            && value.TryGetProperty("success", out var successProperty)
            && successProperty.ValueKind == JsonValueKind.True;

        var resultPath = Path.Combine(options.RootPath, "smoke-result.json");
        var result = payload?.GetRawText() ?? "{}";
        File.WriteAllText(resultPath, result);

        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            Environment.Exit(success ? 0 : 1);
        });

        return new
        {
            completed = true,
            accepted = true,
            resultPath = "smoke-result.json",
            exitCode = success ? 0 : 1,
        };
    }
}
