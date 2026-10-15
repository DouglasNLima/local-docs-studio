namespace LensDocsStudio.Core.Rendering;

/// <summary>Native palettes; fixed choices are independent of the application appearance.</summary>
public static class DiagramThemes
{
    public static IReadOnlyList<string> Choices { get; } = Array.AsReadOnly(new[] { "Auto", "Lens", "Default", "Neutral", "Forest", "Dark" });

    public static string ResolveName(string choice, bool dark)
    {
        if (!Choices.Contains(choice)) throw new ArgumentException("Choose a supported diagram theme.", nameof(choice));
        return choice == "Auto" ? dark ? "Dark" : "Lens" : choice;
    }

    public static (string Background, string Foreground, string Accent, string? Surface, string? Border, string? Line) Colours(string theme) => theme switch
    {
        "Lens" => ("#FAFAFA", "#202124", "#FF883E", null, null, null),
        "Default" => ("#FFFFFF", "#1F2937", "#7C3AED", "#ECECFF", "#9370DB", "#4B5563"),
        "Neutral" => ("#FAFAFA", "#202124", "#52525B", "#F4F4F5", "#71717A", "#52525B"),
        "Forest" => ("#F7FCF7", "#17351F", "#2E7D32", "#E7F3E7", "#3E7C4A", "#376D43"),
        "Dark" => ("#202124", "#F3F4F6", "#FF883E", null, null, null),
        _ => throw new ArgumentException("Resolve the diagram theme before rendering.", nameof(theme))
    };
}
