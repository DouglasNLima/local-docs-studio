using System.Security.Cryptography;
using System.Text;
using LensDocsStudio.Core.Mermaid;
using Mermaider.Models;

namespace LensDocsStudio.Core.ErDiagrams;

public enum ErStatementKind { Header, Direction, Entity, End, Attribute, Relationship, Unknown }
public enum ErCompatibility { Editable, PartiallyEditable, Protected, Invalid }
public sealed record ErStatement(string Identity, ErStatementKind Kind, MermaidSourceSpan Span, MermaidSourceSpan Line,
    string? Owner, IReadOnlyDictionary<string, MermaidSourceSpan> Tokens, bool Protected = false)
{
    public string Read(string source, string token) => Tokens.TryGetValue(token, out var span) ? span.Read(source) : "";
}
public sealed record ErAttributeModel(string Identity, string EntityId, string Type, string Name, IReadOnlyList<ErKeyType> Keys, string? Comment, ErStatement Statement)
{
    public ErAttributeInfo Semantic => new(Type, Name, Keys, Comment);
}
public sealed record ErEntityModel(string Id, string Label, IReadOnlyList<ErStatement> Declarations, IReadOnlyList<ErAttributeModel> Attributes)
{
    public string Identity => "entity:" + Id;
    public bool Implicit => Declarations.Count == 0;
}
public sealed record ErRelationshipModel(string Identity, string Entity1, string Entity2, ErCardinality Cardinality1,
    ErCardinality Cardinality2, string Label, bool Identifying, ErStatement Statement)
{
    public ErRelationship Semantic => new(Entity1, Entity2, Cardinality1, Cardinality2, Label, Identifying);
}
public sealed record ErDiagramModel(string Source, IReadOnlyList<ErStatement> Statements, IReadOnlyList<ErEntityModel> Entities,
    IReadOnlyList<ErRelationshipModel> Relationships, Direction? Direction, IReadOnlyList<string> Diagnostics)
{
    public string Revision => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Source)));
    public bool IsValid => Diagnostics.Count == 0;
    public bool HasUnknownDependencies => Statements.Any(item => item.Protected);
    public ErCompatibility Compatibility => !IsValid ? ErCompatibility.Invalid : !HasUnknownDependencies && Entities.All(item => item.Declarations.Count <= 1) ? ErCompatibility.Editable
        : Statements.Any(item => !item.Protected && item.Kind is ErStatementKind.Entity or ErStatementKind.Relationship) ? ErCompatibility.PartiallyEditable : ErCompatibility.Protected;
    public string NewLine => Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    public ErEntityModel Entity(string id) => Entities.SingleOrDefault(item => item.Id == id) ?? throw new ArgumentException("Select an existing entity.");
    public ErAttributeModel Attribute(string identity) => Entities.SelectMany(item => item.Attributes).SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select an exact attribute occurrence.");
    public ErRelationshipModel Relationship(string identity) => Relationships.SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select an exact relationship occurrence.");
    public ErDiagram Semantic => new() { Direction = Direction, Entities = Entities.Select(item => new ErEntity { Id = item.Id, Label = item.Label, Attributes = item.Attributes.Select(attribute => attribute.Semantic).ToArray() }).ToArray(), Relationships = Relationships.Select(item => item.Semantic).ToArray() };
}

public abstract record ErEditOperation;
public sealed record AddErEntity(string Id, string Label) : ErEditOperation;
public sealed record UpdateErEntity(string Id, string NewId, string Label) : ErEditOperation;
public sealed record DeleteErEntity(string Id) : ErEditOperation;
public sealed record DuplicateErEntity(string Id, string NewId) : ErEditOperation;
public sealed record AddErAttribute(string EntityId, string Type, string Name, IReadOnlyList<ErKeyType> Keys, string? Comment = null) : ErEditOperation;
public sealed record UpdateErAttribute(string Identity, string Type, string Name, IReadOnlyList<ErKeyType> Keys, string? Comment = null) : ErEditOperation;
public sealed record DeleteErAttribute(string Identity) : ErEditOperation;
public sealed record MoveErAttribute(string Identity, int Delta) : ErEditOperation;
public sealed record DuplicateErAttribute(string Identity) : ErEditOperation;
public sealed record AddErRelationship(string Entity1, string Entity2, ErCardinality Cardinality1 = ErCardinality.One, ErCardinality Cardinality2 = ErCardinality.ZeroMany, bool Identifying = true, string Label = "") : ErEditOperation;
public sealed record UpdateErRelationship(string Identity, string Entity1, string Entity2, ErCardinality Cardinality1, ErCardinality Cardinality2, bool Identifying, string Label) : ErEditOperation;
public sealed record DeleteErRelationship(string Identity) : ErEditOperation;
public sealed record DuplicateErRelationship(string Identity) : ErEditOperation;
/// <summary>Reverse the route while retaining each entity's own cardinality.</summary>
public sealed record ReverseErRelationship(string Identity) : ErEditOperation;
public sealed record ErEditResult(bool Success, string? Error);
public sealed record ErOperationCapability(bool Allowed, string Reason);
public sealed record ErEditPlan(string Revision, IReadOnlyList<MermaidDocumentPatch> Patches, IReadOnlyList<string> Identities, ErDiagram Expected);
