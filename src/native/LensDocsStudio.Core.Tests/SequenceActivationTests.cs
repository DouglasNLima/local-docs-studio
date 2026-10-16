using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class SequenceActivationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Standalone_commands_preserve_activation_geometry_labels_and_original_source(bool dark)
    {
        const string source = "sequenceDiagram\r\n  participant Writer\r\n  participant Studio\r\n  Writer->>Studio: Open folder\r\n  %% Begin processing\r\n\r\n  activate Studio\r\n  Studio-->>Writer: Documents loaded\r\n  deactivate Studio\r\n  Note over Writer,Studio: Files remain local";
        var result = new DiagramService().Render(source, dark);
        Assert.True(result.Success, result.Error);
        Assert.Equal(source, result.Source);
        var xml = XDocument.Parse(result.Svg!);
        var activation = Assert.Single(xml.Descendants(), e => (string?)e.Attribute("class") == "activation");
        Assert.Equal("Studio", (string?)activation.Attribute("data-actor"));
        Assert.True((double)activation.Attribute("height")! > 0);
        Assert.Contains(xml.Descendants(), e => e.Value == "Documents loaded");
        Assert.Contains(xml.Descendants(), e => e.Value == "Files remain local");
    }

    [Theory]
    [InlineData("->>")]
    [InlineData("-->>")]
    [InlineData("->")]
    [InlineData("-->")]
    [InlineData("-)")]
    [InlineData("--)")]
    public void Supported_message_arrows_keep_standalone_activation(string arrow)
    {
        var result = new DiagramService().Render($"sequenceDiagram\nA{arrow}B: Request\nactivate B\nB-->>A: Response\ndeactivate B");
        Assert.True(result.Success, result.Error);
        Assert.Single(XDocument.Parse(result.Svg!).Descendants(), e => (string?)e.Attribute("class") == "activation");
    }

    [Theory]
    [InlineData("activate B\nA->>B: Request")]
    [InlineData("A->>B: Request\nactivate A")]
    [InlineData("A->>B: Request\nNote over B: Waiting\nactivate B")]
    [InlineData("A->>+B: Request\nactivate B")]
    [InlineData("A->>B: Request\nactivate B\nactivate B")]
    [InlineData("A->>B: Request\ndeactivate B")]
    [InlineData("A->>B: Request\nactivate B; deactivate B")]
    [InlineData("A->>B: Request\nactivate")]
    public void Unrepresentable_commands_report_a_visible_limit_instead_of_silently_losing_activation(string body)
    {
        var source = "sequenceDiagram\n" + body;
        var result = new DiagramService().Render(source);
        Assert.False(result.Success);
        Assert.Contains("sequence activation", result.Error!);
        Assert.Equal(source, result.Source);
    }

    [Fact]
    public void Shorthand_and_command_words_inside_message_labels_remain_unchanged()
    {
        const string source = "sequenceDiagram\nA->>+B: activate B\nB-->>-A: deactivate B";
        var result = new DiagramService().Render(source);
        Assert.True(result.Success, result.Error);
        Assert.Equal(source, result.Source);
        Assert.Single(XDocument.Parse(result.Svg!).Descendants(), e => (string?)e.Attribute("class") == "activation");
    }
}
