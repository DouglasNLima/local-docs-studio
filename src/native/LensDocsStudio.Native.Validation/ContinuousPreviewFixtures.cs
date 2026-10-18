namespace LensDocsStudio.Native.Validation;

internal static class ContinuousPreviewFixtures
{
    public static (string Name, string Source, string Last)[] Create(string output, string? scenario)
    {
        var fixtures = new[]
        {
            (Name: "list", Source: string.Join("\n", Enumerable.Range(1, 4000).Select(i => $"- **word{i}**")), Last: "word4000"),
            (Name: "table", Source: "| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"| Row{i} | Value{i} |")), Last: "Value1000"),
            (Name: "quote", Source: string.Join("\n>\n", Enumerable.Range(1, 1200).Select(i => $"> Quoted{i}")), Last: "Quoted1200"),
            (Name: "details", Source: "<details open>\n<summary>Large section</summary>\n\n" + string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Hidden{i}")) + "\n\n</details>", Last: "Hidden1200"),
            (Name: "paragraphs", Source: string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Paragraph{i}: **Evidence** &amp; review remains selectable.")), Last: "Paragraph1200")
        };
        if (scenario is "extended" or "large-4mb" or "mixed" or "all")
        {
            var large = "# Large editing fixture\n\n" + string.Concat(Enumerable.Range(0, 20000).Select(i =>
                $"Paragraph {i:D5}: **portable Markdown** retains Unicode Café and [relative links](Section-0/Document-0.md). "
                + "The current source must survive native insertion, layout and Undo without an implicit file write.\n\n"));
            var mixed = "# Mixed document\n\n[Deep target](#deep-target)\n\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"- **Mixed item{i}**"))
                + "\n\n| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 300).Select(i => $"| Row{i} | Value{i} |"))
                + "\n\n<details open>\n<summary>Outer evidence</summary>\n\n<details open>\n<summary>Nested evidence</summary>\n\n"
                + string.Join("\n\n", Enumerable.Range(1, 300).Select(i => $"> **Quoted{i}** Café 😀 é"))
                + "\n\n</details>\n\n</details>\n\n" + string.Join("\n\n", Enumerable.Repeat("![Repeated pixel](pixel.png)", 12))
                + "\n\n```mermaid\nflowchart LR\nA[Source] --> B[Native preview]\n```\n\n## Deep target\n\nMixed final marker.";
            fixtures = scenario == "all" ? [.. fixtures, ("mixed", mixed, "Mixed final marker.")]
                : [("large-4mb", large, "Paragraph 19999"), ("mixed", mixed, "Mixed final marker.")];
            if (scenario is not ("extended" or "all")) fixtures = fixtures.Where(fixture => fixture.Name == scenario).ToArray();
            File.WriteAllBytes(Path.Combine(output, "pixel.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII="));
        }
        else if (scenario is not null)
        {
            fixtures = fixtures.Where(fixture => fixture.Name == scenario).ToArray();
            if (fixtures.Length == 0) throw new ArgumentException("Choose an existing Continuous fixture.");
        }
        return fixtures;
    }
}
