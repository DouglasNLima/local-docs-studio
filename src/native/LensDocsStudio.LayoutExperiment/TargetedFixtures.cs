namespace LensDocsStudio.LayoutExperiment;

internal sealed record TargetedFixture(string Name, string Source, string[] RequiredText);
internal static class TargetedFixtures
{
    public static IReadOnlyList<TargetedFixture> Cases { get; } =
    [
        new("rich", "## Heading\n\nPlain **bold** *italic* and `code` &amp; escaped \\* star. é 😀\n\n++under++ ~~strike~~ ==mark== H~2~O x^2^.\n\n```csharp\n// comment\nvar count = 42;\n```\n\n[Deep link](#final-heading)\n\n## Final heading\n\nFinal marker.",
            ["Heading", "Plain bold italic and code & escaped * star. é 😀", "// comment", "var count = 42;", "Final marker."]),
        new("nested-lists", "3. **First** item\n\n   continuation *italic*\n\n   - nested unordered\n     7. nested ordered\n     8. another nested\n\n4. Second item\n\n   another paragraph\n\nAfter list.",
            ["First item", "continuation italic", "nested unordered", "nested ordered", "another nested", "Second item", "another paragraph", "After list."]),
        new("table-wrap", "| Left | Centre | Right |\n|:---|:---:|---:|\n| **first** cell | centre content | final value |\n| " + string.Join(' ', Enumerable.Repeat("long wrapped cell", 16)) + " | é 😀 | 12345 |\n\nAfter table.",
            ["Left\tCentre\tRight", "first cell", "centre content", "final value", "long wrapped cell", "é 😀", "12345", "After table."]),
        new("collapsed-details", "<details>\n<summary>Hidden section</summary>\n\n## Deep target\n\nHidden Café 😀 é content.\n\n</details>\n\nAfter fold.", ["Hidden section", "After fold."]),
        new("unicode-bidi", "# Unicode\n\nLatin العربيّة and हिन्दी with é, 😀 and 👩‍💻.\n\n[**rich link**](#final-heading) and `escaped code`.\n\n## Final heading\n\nFinal marker.",
            ["العربيّة", "हिन्दी", "é", "😀", "👩‍💻", "rich link", "Final marker."])
    ];
}
