using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Core.Tests;

public sealed class DocumentStatisticsTests
{
    [Theory]
    [InlineData("", 0, 0)]
    [InlineData(" \r\n\t", 0, 0)]
    [InlineData("# Café 😀\n\nHello **world**!", 3, 19)]
    [InlineData("# Title\n\none**two** three &amp; four-five Café's 中文", 6, 40)]
    [InlineData("---\ntitle: Hidden\n---\n# Visible\n\n`hidden code`\n\n```mermaid\nA --> B\n```", 1, 7)]
    [InlineData("| A | B |\n|---|---|\n| One | Two |", 4, 11)]
    [InlineData("![Alt text](private.png) [Read me](https://example.invalid)", 4, 16)]
    [InlineData("# 𝒜𝒷 𐐀 Ⅳ ²", 4, 8)]
    [InlineData("Cafe\u0301 nai\u0308ve don’t re-enter", 4, 27)]
    [InlineData("one\n\ntwo\n\n    hidden code\n\nthree", 3, 13)]
    [InlineData("<details>\n<summary>More</summary>\n## Hidden title\n</details>", 3, 17)]
    [InlineData("<figure data-image-figure>\n<img src=\"private.png\" alt=\"Alt text\">\n<figcaption>Caption</figcaption>\n</figure>", 3, 16)]
    public void Statistics_use_readable_text_without_rendering_or_accessing_assets(string source, int words, int characters)
    {
        var result = DocumentStatisticsService.Measure(source);
        Assert.Equal(words, result.Words);
        Assert.Equal(characters, result.Characters);
        var review = new DocumentReviewService().Analyse(source);
        Assert.Equal(review.Metrics.Words, result.Words);
        Assert.Equal(review.Metrics.Characters, result.Characters);
    }

    [Theory]
    [InlineData(0, 0, "0 min read")]
    [InlineData(1, 1, "1 min read")]
    [InlineData(220, 1, "1 min read")]
    [InlineData(221, 2, "2 min read")]
    [InlineData(13200, 60, "1 hr 0 min read")]
    [InlineData(13421, 62, "1 hr 2 min read")]
    public void Reading_duration_retains_the_220_word_ceiling(int words, int minutes, string label)
    {
        var result = new DocumentStatistics(words, 0);
        Assert.Equal(minutes, result.ReadingMinutes);
        Assert.Equal(label, result.ReadingTime);
    }

    [Fact]
    public void Large_buffer_is_measured_without_truncating_its_last_words()
    {
        var result = DocumentStatisticsService.Measure("# Large\n\n" + string.Concat(Enumerable.Repeat("word ", 100_000)) + "final");
        Assert.Equal(100_002, result.Words);
        Assert.Equal(500_011, result.Characters);
    }

    [Fact]
    public void Near_limit_source_keeps_final_prose_and_original_bytes()
    {
        var source = "```text\n" + new string('x', DocumentReviewService.MaximumSourceCharacters - 40) + "\n```\n\nLast café 😀";
        var bytes = System.Text.Encoding.UTF8.GetBytes(source);
        Assert.Equal(new DocumentStatistics(2, 11), DocumentStatisticsService.Measure(source));
        Assert.Equal(bytes, System.Text.Encoding.UTF8.GetBytes(source));
    }

    [Fact]
    public async Task Cancellation_during_large_analysis_returns_no_metrics()
    {
        using var cancellation = new CancellationTokenSource();
        using var started = new ManualResetEventSlim();
        var source = string.Concat(Enumerable.Repeat("word ", 500_000));
        var work = Task.Run(() => { started.Set(); return DocumentStatisticsService.Measure(source, cancellationToken: cancellation.Token); });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
    }

    [Fact]
    public void Cancelled_or_oversized_analysis_never_returns_partial_metrics()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DocumentStatisticsService.Measure("# Title", cancellationToken: cancellation.Token));
        Assert.Throws<IOException>(() => DocumentStatisticsService.Measure(new string('a', DocumentReviewService.MaximumSourceCharacters + 1)));
        Assert.Equal(new DocumentStatistics(0, 0), DocumentStatisticsService.Measure("flowchart LR\nA --> B", true));
    }
}
