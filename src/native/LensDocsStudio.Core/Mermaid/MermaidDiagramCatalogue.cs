using System.Globalization;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Core.StateDiagrams;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.ArchitectureDiagrams;
using LensDocsStudio.Core.C4Diagrams;
using LensDocsStudio.Core.BlockDiagrams;
using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.XyCharts;
using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.QuadrantDiagrams;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Core.VennDiagrams;
using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.TimelineDiagrams;
using LensDocsStudio.Core.JourneyDiagrams;
using LensDocsStudio.Core.KanbanDiagrams;
using LensDocsStudio.Core.RequirementDiagrams;
using LensDocsStudio.Core.GitGraphDiagrams;
using LensDocsStudio.Core.Mindmaps;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Core.PacketDiagrams;

namespace LensDocsStudio.Core.Mermaid;

public enum MermaidDiagramFamily { Flowchart, Sequence, State, Er, Class, Architecture, C4, Block, Pie, Xy, Radar, Sankey, Quadrant, Treemap, Venn, Gantt, Timeline, Journey, Kanban, Requirement, GitGraph, Mindmap, TreeView, Packet }

public sealed record MermaidStarterVariant(string Id, string Name, string Source, string Note);
public sealed record MermaidDiagramDefinition(MermaidDiagramFamily Family, string Name, string Category, string Description, string Aliases,
    IReadOnlyList<MermaidStarterVariant> Variants, Func<string, bool> Recognises, Func<string, DiagramService, CancellationToken, Task<string?>> ValidateAsync)
{
    public MermaidStarterVariant SuggestedVariant => Variants.FirstOrDefault(item => item.Id == "example") ?? Variants[0];
    public string Id => Family.ToString().ToLowerInvariant();
}

/// <summary>Bounded visual families, existing draft validation and exact menu starters. Rendering alone never grants editing permission.</summary>
public static class MermaidDiagramCatalogue
{
    // Exact existing native visual fixture: tests/fixtures/native-mermaid-visual/simple.mmd
    private const string FlowchartExample = "flowchart TD\n    A([Start]) -->|Ready| B[Process]\n    B --> C{Review}\n";
    private const string FlowchartStarter = "flowchart TD\n";
    private const string SequenceStarter = "sequenceDiagram\n    participant Author\n    participant Studio\n    Author->>Studio: Open documentation\n    Studio-->>Author: Display preview\n";
    private const string StateStarter = "stateDiagram-v2\n    state \"Draft\" as Draft\n    state \"Review\" as Review\n    [*] --> Draft\n    Draft --> Review : Submit\n    Review --> Draft : Request changes\n    Review --> [*]\n";
    private const string ErStarter = "erDiagram\n    CUSTOMER {\n        string customer_id PK\n        string email UK\n    }\n    ORDER {\n        string order_id PK\n        string customer_id FK\n    }\n    CUSTOMER ||--o{ ORDER : places\n";
    // Exact existing compatibility fixture: tests/fixtures/native-mermaid/expansion-v/c4-container.mmd
    private const string C4ContainerStarter = "C4Container\ntitle Documentation containers\nPerson(reader, \"Reader\", \"Authors documentation\")\nContainer_Boundary(studio, \"Studio\")\n{\n  Container(editor, \"Editor\", \"WPF\", \"Local authoring\")\n  ContainerDb(store, \"Workspace\", \"Files\", \"Portable source\")\n}\nRel(reader, editor, \"Authors\")\nBiRel(editor, store, \"Reads and saves\", \"UTF-8\")\n";
    // Exact existing compatibility fixture: tests/fixtures/native-mermaid/expansion-v/c4-component.mmd
    private const string C4ComponentStarter = "C4Component\ntitle Documentation components\nBoundary(core, \"Core\", \"Library\") {\n  Component(parser, \"Parser\", \".NET\", \"Portable Markdown\")\n  ComponentDb(catalogue, \"Catalogue\", \"JSON\", \"Local content\")\n  ComponentQueue(queue, \"Render queue\", \"Memory\", \"Bounded work\")\n}\nRel(parser, catalogue, \"Looks up\", \"Local\")\nRel(parser, queue, \"Schedules\")\n";
    // Exact existing compatibility fixture: tests/fixtures/native-mermaid/expansion-vi/treeview-box.mmd
    private const string TreeViewBoxStarter = "treeView\n├── source/\n│   ├── main.cs\n│   └── \"utility Ω.cs\"\n└── README.md\n";

    public static IReadOnlyList<MermaidDiagramDefinition> All { get; } = Array.AsReadOnly(new MermaidDiagramDefinition[]
    {
        new(MermaidDiagramFamily.Flowchart, "Flowchart", "Flows and systems", "Connect steps, decisions and processes.", "flow graph process decision",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Empty canvas", FlowchartStarter, "Start with an empty Flowchart and add nodes in its visual editor."), new("example", "Simple process", FlowchartExample, "A process with a labelled connection and a review decision.") }),
            source => new MermaidSourceParser().Parse(source).DiagramType == "Flowchart", ValidateFlowchartAsync),
        new(MermaidDiagramFamily.Sequence, "Sequence", "Flows and systems", "Show messages between people and systems.", "interaction messages participants",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", SequenceStarter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            SequenceSourceParser.IsSequence, async (source, diagrams, token) => (await new SequenceVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.State, "State", "Flows and systems", "Describe states, transitions and lifecycles.", "state machine lifecycle",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", StateStarter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            StateDiagramSourceParser.IsState, async (source, diagrams, token) => (await new StateVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Er, "Entity relationship", "Flows and systems", "Model entities, attributes and relationships.", "ER database entity schema",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", ErStarter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            ErDiagramSourceParser.IsEr, async (source, diagrams, token) => (await new ErVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Class, "Class", "Flows and systems", "Describe classes, members and relationships.", "UML object inheritance",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", ClassDiagramSourceParser.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            ClassDiagramSourceParser.IsClass, async (source, diagrams, token) => (await new ClassDiagramVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Architecture, "Architecture", "Flows and systems", "Connect services, groups and junctions.", "system services infrastructure",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", ArchitectureVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            ArchitectureVisualDraft.IsArchitecture, async (source, diagrams, token) => (await new ArchitectureVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.C4, "C4", "Flows and systems", "Describe system context, containers or components.", "software architecture context container component",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Context", C4VisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection."), new("container", "Container", C4ContainerStarter, "Container-level elements; no conversion between C4 levels."), new("component", "Component", C4ComponentStarter, "Component-level elements; Dynamic and Deployment remain source-only.") }),
            C4VisualDraft.IsC4, async (source, diagrams, token) => (await new C4VisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Block, "Block", "Flows and systems", "Arrange connected blocks in a bounded grid.", "layout blocks grid",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", BlockVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            BlockVisualDraft.IsBlock, async (source, diagrams, token) => (await new BlockVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Pie, "Pie", "Data and visualisation", "Compare values as parts of a whole.", "chart proportions slices",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", PieVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            PieVisualDraft.IsPie, async (source, diagrams, token) => (await new PieVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Xy, "XY Chart", "Data and visualisation", "Plot bars and lines against chart axes.", "xychart chart bar line",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", XyVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            XyVisualDraft.IsXy, async (source, diagrams, token) => (await new XyVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Radar, "Radar", "Data and visualisation", "Compare series across several axes.", "spider chart radial",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", RadarVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            RadarVisualDraft.IsRadar, async (source, diagrams, token) => (await new RadarVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Sankey, "Sankey", "Data and visualisation", "Show weighted flows between named items.", "flow values energy",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", SankeyVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            SankeyVisualDraft.IsSankey, async (source, diagrams, token) => (await new SankeyVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Quadrant, "Quadrant", "Data and visualisation", "Position items against two dimensions.", "matrix priority scatter",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", QuadrantVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            QuadrantVisualDraft.IsQuadrant, async (source, diagrams, token) => (await new QuadrantVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Treemap, "Treemap", "Data and visualisation", "Show hierarchical values as rectangles.", "tree map hierarchy size",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", TreemapVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            TreemapVisualDraft.IsTreemap, async (source, diagrams, token) => (await new TreemapVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Venn, "Venn", "Data and visualisation", "Show sets and their intersections.", "sets overlap intersection",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", VennVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            VennVisualDraft.IsVenn, async (source, diagrams, token) => (await new VennVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Gantt, "Gantt", "Planning and work", "Plan tasks, dates and dependencies.", "schedule project tasks",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", GanttVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            GanttVisualDraft.IsGantt, async (source, diagrams, token) => (await new GanttVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Timeline, "Timeline", "Planning and work", "Arrange events across named periods.", "history dates events",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", TimelineVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            TimelineVisualDraft.IsTimeline, async (source, diagrams, token) => (await new TimelineVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Journey, "Journey", "Planning and work", "Map tasks, actors and experience scores.", "user experience actors",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", JourneyVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            JourneyVisualDraft.IsJourney, async (source, diagrams, token) => (await new JourneyVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Kanban, "Kanban", "Planning and work", "Organise cards into workflow columns.", "board cards work",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", KanbanVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            KanbanVisualDraft.IsKanban, async (source, diagrams, token) => (await new KanbanVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Requirement, "Requirement", "Planning and work", "Link requirements, elements and verification.", "requirements traceability verification",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", RequirementVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            RequirementVisualDraft.IsRequirement, async (source, diagrams, token) => (await new RequirementVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.GitGraph, "GitGraph", "Planning and work", "Illustrate commits, branches and merges.", "git version control history",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", GitGraphVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            GitGraphVisualDraft.IsGitGraph, async (source, diagrams, token) => (await new GitGraphVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Mindmap, "Mindmap", "Structures and technical", "Explore ideas as a branching hierarchy.", "mind map brainstorm ideas",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", MindmapVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            MindmapVisualDraft.IsMindmap, async (source, diagrams, token) => (await new MindmapVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.TreeView, "TreeView", "Structures and technical", "Describe a tree using indentation or box glyphs.", "tree files folders hierarchy",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Indentation", TreeViewVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection."), new("box", "Box drawing", TreeViewBoxStarter, "Field edits preserve box glyphs; structural changes remain protected.") }),
            TreeViewVisualDraft.IsTreeView, async (source, diagrams, token) => (await new TreeViewVisualDraft(source, diagrams).ValidateAsync(token)).Error),
        new(MermaidDiagramFamily.Packet, "Packet", "Structures and technical", "Describe packet fields and bit ranges.", "network bits protocol",
            Array.AsReadOnly(new MermaidStarterVariant[] { new("default", "Standard", PacketVisualDraft.Starter, "Bounded visual editing; advanced syntax keeps its existing protection.") }),
            PacketVisualDraft.IsPacket, async (source, diagrams, token) => (await new PacketVisualDraft(source, diagrams).ValidateAsync(token)).Error),
    });

    public static MermaidDiagramDefinition Get(MermaidDiagramFamily family) => All.Single(item => item.Family == family);
    public static MermaidDiagramDefinition? Recognise(string source) => All.SingleOrDefault(item => item.Recognises(source));
    public static IReadOnlyList<MermaidDiagramDefinition> Search(string? query, string? category = null)
    {
        var words = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return All.Where(item => (category is null || item.Category == category) && words.All(word =>
            CultureInfo.InvariantCulture.CompareInfo.IndexOf(item.Name + " " + item.Category + " " + item.Description + " " + item.Aliases,
                word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).ToArray();
    }
    private static Task<string?> ValidateFlowchartAsync(string source, DiagramService diagrams, CancellationToken token) => Task.Run(() =>
    {
        var model = new MermaidSourceParser().Parse(source, token);
        if (!model.HasSafeTransformations || !model.HasCompleteSourceMap) return model.Diagnostic ?? "This Flowchart remains protected source.";
        var render = diagrams.Render(source, cancellationToken: token);
        return render.Success && MermaidSemanticValidation.RenderingAgrees(model, render) ? null : render.Error ?? "Canonical Flowchart correspondence failed.";
    }, token);
}
