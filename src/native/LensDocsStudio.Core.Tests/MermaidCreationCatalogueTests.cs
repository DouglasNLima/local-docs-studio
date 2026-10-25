using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidCreationCatalogueTests
{
    [Fact]
    public void Bundled_help_explains_context_selection_variants_and_explicit_persistence()
    {
        var topic = Assert.Single(LensDocsStudio.Core.Content.NativeHelpGuide.Topics, item => item.Title == "Mermaid creation hub");
        foreach (var phrase in new[] { "24 families", "after any selected text", "Cancel", "Save", "Redo", "C4", "box drawing", "source-only", "read-only", "Insert Mermaid as code" })
            Assert.Contains(phrase, topic.Source, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("C4Deployment\n")]
    [InlineData("flowchart TD\n%%{init: {}}%%\nA --> B\n")]
    [InlineData("unknownDiagram\n")]
    public async Task Protected_or_unknown_source_cannot_gain_permission_from_rendering(string source)
    {
        var result = await MermaidDiagramCatalogue.InspectAsync(source, new());
        Assert.NotNull(result.Error);
    }
    [Fact]
    public async Task Existing_source_is_bounded_before_inspecting_any_family()
    {
        var characters = await MermaidDiagramCatalogue.InspectAsync("flowchart TD\n" + new string(' ', 100_000), new());
        var lines = await MermaidDiagramCatalogue.InspectAsync("flowchart TD\n" + string.Concat(Enumerable.Repeat("%% comment\n", 1501)), new());
        Assert.Null(characters.Definition); Assert.NotNull(characters.Error);
        Assert.Null(lines.Definition); Assert.NotNull(lines.Error);
    }
    public static IEnumerable<object[]> Starters => MermaidDiagramCatalogue.All.SelectMany(family => family.Variants.Select(variant => new object[] { family.Family, variant.Id }));

    [Fact]
    public void Catalogue_contains_exactly_the_existing_24_unique_families_and_28_variants()
    {
        Assert.Equal(24, MermaidDiagramCatalogue.All.Count);
        Assert.Equal(Enum.GetValues<MermaidDiagramFamily>().Order(), MermaidDiagramCatalogue.All.Select(item => item.Family).Order());
        Assert.Equal(24, MermaidDiagramCatalogue.All.Select(item => item.Id).Distinct().Count());
        Assert.Equal(new[] { 8, 7, 6, 3 }, MermaidDiagramCatalogue.All.GroupBy(item => item.Category).Select(group => group.Count()));
        Assert.Equal(28, Starters.Count());
        Assert.Throws<InvalidOperationException>(() => MermaidDiagramCatalogue.Get((MermaidDiagramFamily)999));
    }

    [Theory]
    [MemberData(nameof(Starters))]
    public async Task Every_exact_starter_is_accepted_by_its_real_visual_draft_and_canonical_renderer(MermaidDiagramFamily family, string variantId)
    {
        var definition = MermaidDiagramCatalogue.Get(family);
        var variant = definition.Variants.Single(item => item.Id == variantId);
        Assert.Equal(definition, MermaidDiagramCatalogue.Recognise(variant.Source));
        var diagrams = new DiagramService();
        Assert.Null(await definition.ValidateAsync(variant.Source, diagrams, CancellationToken.None));
        var rendered = diagrams.Render(variant.Source);
        Assert.True(rendered.Success, rendered.Error);
        Assert.Equal(variant.Source, rendered.Source);
        var fenced = MermaidCreationTarget.FormatInsertion(variant.Source, "", new(0, 0));
        var mapped = Assert.Single(MermaidBlockSource.Find(new MarkdownService().Parse(fenced)));
        Assert.Equal(variant.Source.TrimEnd('\r', '\n'), mapped.Source.TrimEnd('\r', '\n'));
    }

    [Theory]
    [InlineData("ENTITY", MermaidDiagramFamily.Er)]
    [InlineData("énergy", MermaidDiagramFamily.Sankey)]
    [InlineData("spider", MermaidDiagramFamily.Radar)]
    [InlineData("c4 component", MermaidDiagramFamily.C4)]
    [InlineData("mind map", MermaidDiagramFamily.Mindmap)]
    public void Search_matches_names_aliases_and_descriptions_without_case_or_accent(string query, MermaidDiagramFamily expected)
        => Assert.Contains(MermaidDiagramCatalogue.Search(query), item => item.Family == expected);

    [Fact]
    public void Search_filter_and_clear_are_deterministic_and_do_not_invent_families()
    {
        Assert.Empty(MermaidDiagramCatalogue.Search("no such family xyz"));
        Assert.Equal(8, MermaidDiagramCatalogue.Search("", "Flows and systems").Count);
        Assert.Equal(MermaidDiagramCatalogue.All, MermaidDiagramCatalogue.Search("  "));
        Assert.Null(MermaidDiagramCatalogue.Recognise("C4Deployment\n"));
        Assert.Null(MermaidDiagramCatalogue.Recognise("unknownDiagram\n"));
    }

    [Theory]
    [InlineData(MermaidDiagramFamily.C4, "container", "creation/c4-container.mmd")]
    [InlineData(MermaidDiagramFamily.C4, "component", "creation/c4-component.mmd")]
    [InlineData(MermaidDiagramFamily.TreeView, "box", "creation/treeview-box.mmd")]
    [InlineData(MermaidDiagramFamily.Flowchart, "example", "native-mermaid-visual/simple.mmd")]
    public void Additional_starters_are_exact_existing_independent_compatibility_fixtures(MermaidDiagramFamily family, string id, string path)
    {
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", path)).ReplaceLineEndings("\n");
        Assert.Equal(fixture, MermaidDiagramCatalogue.Get(family).Variants.Single(item => item.Id == id).Source);
    }
}
