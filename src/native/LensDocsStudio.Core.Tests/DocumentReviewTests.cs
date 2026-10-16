using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Core.Tests;

public sealed class DocumentReviewTests
{
    [Fact]
    public void Review_reports_exact_CRLF_source_locations_for_prose_without_changing_it()
    {
        const string source = "# Café 😀\r\n\r\n### Skipped\r\n\r\nColor and TODO remain.";
        var result = new DocumentReviewService().Analyse(source);
        Assert.Equal(3, result.Issues.Count);
        var spelling = Assert.Single(result.Issues, issue => issue.Rule == "British English");
        Assert.Equal("Color", source.Substring(spelling.Start, spelling.Length));
        Assert.Equal(5, spelling.Line); Assert.Equal(1, spelling.Column); Assert.Contains("Colour", spelling.Suggestion);
        var marker = Assert.Single(result.Issues, issue => issue.Rule == "TODO/FIXME");
        Assert.Equal("TODO", source.Substring(marker.Start, marker.Length)); Assert.Equal(11, marker.Column);
        Assert.Equal(ReviewSeverity.Warning, result.Issues[0].Severity);
    }

    [Fact]
    public void Review_excludes_code_HTML_front_matter_and_URL_destinations_from_prose_checks()
    {
        const string source = "---\ntitle: Color TODO\n---\n# Safe\n\n`Color TODO` [Guide](https://example.invalid/color/TODO) <https://example.invalid/color/TODO>\n\n" +
            "```text\n# Color\nFIXME TODO\n```\n\n    Color TODO\n\n<div>Color TODO</div>\n\n:::mermaid\nflowchart LR\ncolor --> TODO\n:::";
        var result = new DocumentReviewService().Analyse(source);
        Assert.Empty(result.Issues); Assert.Equal(1, result.Metrics.Headings); Assert.Equal(2, result.Metrics.CodeBlocks); Assert.Equal(1, result.Metrics.Diagrams);
    }

    [Fact]
    public void Review_retains_word_boundaries_across_formatting_and_decodes_entities()
    {
        const string source = "# Title\n\none**two** three &amp; four-five Café's 中文\n\n| A | B |\n|---|---|\n| Cell | Text |";
        var result = new DocumentReviewService().Analyse(source);
        Assert.Equal(10, result.Metrics.Words); Assert.Equal(1, result.Metrics.ReadingMinutes);
        Assert.Equal(1, result.Metrics.Tables); Assert.Empty(result.Issues);
    }

    [Fact]
    public void Review_reports_reference_images_with_empty_alt_text_without_reading_files()
    {
        const string source = "# Images\n\n![][missing] ![Café][other]\n\n[missing]: ../unreadable.png\n[other]: https://example.invalid/no-download.png";
        var result = new DocumentReviewService().Analyse(source);
        var issue = Assert.Single(result.Issues); Assert.Equal("Alt text", issue.Rule);
        Assert.Equal("![][missing]", source.Substring(issue.Start, issue.Length)); Assert.Equal(2, result.Metrics.Images);
    }

    [Fact]
    public void Review_resolves_heading_and_named_anchor_fragments_in_current_source()
    {
        const string source = "# Title\n\n<span id=\"Café\" data-doc-anchor></span>\n\n[Title](#title) [Named](#Caf%C3%A9) [Top](#) [Missing](#missing) [Other](other.md#missing)";
        var result = new DocumentReviewService().Analyse(source);
        var issue = Assert.Single(result.Issues); Assert.Equal("Internal links", issue.Rule);
        Assert.Equal("[Missing](#missing)", source.Substring(issue.Start, issue.Length)); Assert.Equal(5, result.Metrics.Links);
    }

    [Fact]
    public void Review_finds_setext_nested_and_duplicate_headings()
    {
        const string source = "Title\n=====\n\n> ### Nested\n\n## Again\n\n## Again";
        var result = new DocumentReviewService().Analyse(source);
        Assert.Equal(4, result.Metrics.Headings);
        Assert.Single(result.Issues, issue => issue.Rule == "Heading hierarchy");
        Assert.Single(result.Issues, issue => issue.Message.Contains("Duplicate heading", StringComparison.Ordinal));
    }

    [Fact]
    public void Review_offers_case_preserving_advice_without_changing_names_or_quotations()
    {
        const string source = "# Wording\n\nCOLOR Color color colorful coloration.\n\nOrganized FIXME.";
        var result = new DocumentReviewService().Analyse(source);
        var spelling = result.Issues.Where(issue => issue.Rule == "British English").ToArray();
        Assert.Equal(4, spelling.Length); Assert.Contains("COLOUR", spelling[0].Suggestion); Assert.Contains("Colour", spelling[1].Suggestion);
        Assert.Contains("colour", spelling[2].Suggestion); Assert.Contains("Organised", spelling[3].Suggestion);
        Assert.All(spelling, issue => Assert.Contains("immutable name or quotation", issue.Suggestion));
    }

    [Theory]
    [InlineData("", "Document is empty.")]
    [InlineData("   \n\n", "Document is empty.")]
    [InlineData("Text without a title.", "Missing H1 title.")]
    [InlineData("<div>Content</div>", "Missing H1 title.")]
    public void Review_distinguishes_empty_source_from_content_without_a_title(string source, string expected)
    {
        var result = new DocumentReviewService().Analyse(source);
        Assert.Equal(expected, Assert.Single(result.Issues).Message);
    }

    [Fact]
    public void Standalone_diagram_review_does_not_treat_diagram_labels_as_prose()
    {
        var result = new DocumentReviewService().Analyse("flowchart LR\nColor --> TODO", true);
        Assert.Empty(result.Issues); Assert.Equal(1, result.Metrics.Diagrams); Assert.Equal(0, result.Metrics.Words);
    }

    [Fact]
    public void Review_reports_its_issue_limit_instead_of_silently_claiming_a_complete_result()
    {
        var result = new DocumentReviewService().Analyse("# Markers\n\n" + string.Join(' ', Enumerable.Repeat("TODO", DocumentReviewService.MaximumIssues + 1)));
        Assert.True(result.Limited); Assert.Equal(DocumentReviewService.MaximumIssues, result.Issues.Count);
    }

    [Fact]
    public void Review_refuses_oversized_source_before_parsing() => Assert.Throws<IOException>(() =>
        new DocumentReviewService().Analyse(new string('x', DocumentReviewService.MaximumSourceCharacters + 1)));

    [Fact]
    public void Review_honours_cancellation()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new DocumentReviewService().Analyse("# Title", cancellationToken: cancelled.Token));
    }
}
