namespace ContinuousViewer.Model;

public sealed record ViewerFixture(string Name, string Source, string Last);
public static class FixtureCatalogue
{
    // Exact performance sources adopted from NativeContinuousPerformanceChecks at 9c7454e.
    public static ViewerFixture[] All =>
    [
        new("list", string.Join("\n", Enumerable.Range(1, 4000).Select(i => $"- **word{i}**")), "word4000"),
        new("table", "| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"| Row{i} | Value{i} |")), "Value1000"),
        new("quote", string.Join("\n>\n", Enumerable.Range(1, 1200).Select(i => $"> Quoted{i}")), "Quoted1200"),
        new("details", "<details open>\n<summary>Large section</summary>\n\n" + string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Hidden{i}")) + "\n\n</details>", "Hidden1200"),
        new("paragraphs", string.Join("\n\n", Enumerable.Range(1, 1200).Select(i => $"Paragraph{i}: **Evidence** &amp; review remains selectable.")), "Paragraph1200"),
        new("mixed", "# Mixed document\n\n[Deep target](#deep-target)\n\n" + string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"- **Mixed item{i}**"))
            + "\n\n| Row | Value |\n|---|---|\n" + string.Join("\n", Enumerable.Range(1, 300).Select(i => $"| Row{i} | Value{i} |"))
            + "\n\n<details open>\n<summary>Outer evidence</summary>\n\n<details open>\n<summary>Nested evidence</summary>\n\n"
            + string.Join("\n\n", Enumerable.Range(1, 300).Select(i => $"> **Quoted{i}** Café 😀 é"))
            + "\n\n</details>\n\n</details>\n\n" + string.Join("\n\n", Enumerable.Repeat("![Repeated pixel](pixel.png)", 12))
            + "\n\n```mermaid\nflowchart LR\nA[Source] --> B[Native preview]\n```\n\n## Deep target\n\nMixed final marker.", "Mixed final marker."),
        new("large-4mb", "# Large editing fixture\n\n" + string.Concat(Enumerable.Range(0, 20000).Select(i =>
            $"Paragraph {i:D5}: **portable Markdown** retains Unicode Café and [relative links](Section-0/Document-0.md). "
            + "The current source must survive native insertion, layout and Undo without an implicit file write.\n\n")), "Paragraph 19999"),
        new("rich", "# Heading Café 😀 é\n\n**Bold** *italic* ~~strike~~ ++underline++ [Link](#distant) [[Guide|Wiki label]]\n\n"
            + "1. First **item**\n   - Nested item\n2. Second item\n\n- [x] Complete task\n- [ ] Open task\n\n"
            + "| Left | Centre | Right |\n|:---|:---:|---:|\n| Café | First<br>Second | 😀 |\n\n"
            + "> [!NOTE]\n> Native callout.\n\n```csharp\nvar text = \"Café\";\n```\n\n"
            + "![Caption](pixel.png)\n\n```mermaid\nflowchart LR\nA --> B\n```\n\n$$x^2 + y^2$$\n\n"
            + "<details>\n<summary>Outer details</summary>\n\n<details>\n<summary>Inner details</summary>\n\n## Distant\n\nRetained final text.\n\n</details>\n\n</details>", "Retained final text."),
        new("oversized", string.Concat(Enumerable.Repeat("Native documentation preserves every word. ", 8192)) + "Final retained target.", "Final retained target.")
    ];
}
