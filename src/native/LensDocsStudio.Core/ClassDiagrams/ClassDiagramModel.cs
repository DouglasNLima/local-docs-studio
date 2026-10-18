using System.Security.Cryptography;
using System.Text;
using LensDocsStudio.Core.Mermaid;
using Mermaider.Models;

namespace LensDocsStudio.Core.ClassDiagrams;

public enum ClassStatementKind { Header, Direction, Class, EndClass, Member, Annotation, Relationship, Namespace, EndNamespace, Note, Unknown }
public enum ClassCompatibility { Editable, PartiallyEditable, Protected, Invalid }
public sealed record ClassDiagramStatement(string Identity, ClassStatementKind Kind, MermaidSourceSpan Span, MermaidSourceSpan Line,
    string? Owner, string? NamespaceIdentity, IReadOnlyDictionary<string, MermaidSourceSpan> Tokens, bool Protected = false)
{
    public string Read(string source, string token) => Tokens.TryGetValue(token, out var span) ? span.Read(source) : "";
}
public sealed record ClassDiagramMember(string Identity, string ClassId, ClassMember Semantic, ClassDiagramStatement Statement);
public sealed record ClassDiagramClass(string Identity, string Id, string Label, string? Annotation,
    IReadOnlyList<ClassDiagramStatement> Declarations, IReadOnlyList<ClassDiagramMember> Members)
{
    public bool Implicit => Declarations.Count == 0;
    public IReadOnlyList<ClassDiagramMember> Attributes => Members.Where(item => !item.Semantic.IsMethod).ToArray();
    public IReadOnlyList<ClassDiagramMember> Methods => Members.Where(item => item.Semantic.IsMethod).ToArray();
    public ClassNode Semantic => new() { Id = Id, Label = Label, Annotation = Annotation, Attributes = Attributes.Select(item => item.Semantic).ToArray(), Methods = Methods.Select(item => item.Semantic).ToArray() };
}
public sealed record ClassDiagramRelationship(string Identity, ClassRelationship Semantic, ClassDiagramStatement Statement);
public sealed record ClassDiagramNamespace(string Identity, string Name, IReadOnlyList<string> ClassIds, ClassDiagramStatement Statement)
{
    public ClassNamespace Semantic => new(Name, ClassIds);
}
public sealed record ClassDiagramNote(string Identity, ClassNote Semantic, ClassDiagramStatement Statement);
public sealed record ClassDiagramModel(string Source, IReadOnlyList<ClassDiagramStatement> Statements, IReadOnlyList<ClassDiagramClass> Classes,
    IReadOnlyList<ClassDiagramRelationship> Relationships, IReadOnlyList<ClassDiagramNamespace> Namespaces, IReadOnlyList<ClassDiagramNote> Notes,
    Direction? Direction, IReadOnlyList<string> Diagnostics)
{
    public string Revision => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Source)));
    public bool IsValid => Diagnostics.Count == 0;
    public bool HasUnknownDependencies => Statements.Any(item => item.Protected);
    public ClassCompatibility Compatibility => !IsValid ? ClassCompatibility.Invalid : !HasUnknownDependencies && Classes.All(item => item.Declarations.Count <= 1) ? ClassCompatibility.Editable
        : Statements.Any(item => !item.Protected && item.Kind is ClassStatementKind.Class or ClassStatementKind.Member or ClassStatementKind.Relationship) ? ClassCompatibility.PartiallyEditable : ClassCompatibility.Protected;
    public string NewLine => Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    public ClassDiagramClass Class(string id) => Classes.SingleOrDefault(item => item.Id == id) ?? throw new ArgumentException("Select an existing class.");
    public ClassDiagramMember Member(string identity) => Classes.SelectMany(item => item.Members).SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select an exact member occurrence.");
    public ClassDiagramRelationship Relationship(string identity) => Relationships.SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select an exact relationship occurrence.");
    public ClassDiagram Semantic => new() { Classes = Classes.Select(item => item.Semantic).ToArray(), Relationships = Relationships.Select(item => item.Semantic).ToArray(), Namespaces = Namespaces.Select(item => item.Semantic).ToArray(), Notes = Notes.Select(item => item.Semantic).ToArray(), Direction = Direction };
}

public abstract record ClassDiagramEditOperation;
public sealed record AddClass(string Id, string? Generic = null, string? Annotation = null) : ClassDiagramEditOperation;
public sealed record UpdateClass(string Id, string NewId, string? Generic, string? Annotation) : ClassDiagramEditOperation;
public sealed record DeleteClass(string Id) : ClassDiagramEditOperation;
public sealed record DuplicateClass(string Id, string NewId) : ClassDiagramEditOperation;
public sealed record AddClassMember(string ClassId, ClassMember Member) : ClassDiagramEditOperation;
public sealed record UpdateClassMember(string Identity, ClassMember Member) : ClassDiagramEditOperation;
public sealed record DeleteClassMember(string Identity) : ClassDiagramEditOperation;
public sealed record DuplicateClassMember(string Identity) : ClassDiagramEditOperation;
public sealed record MoveClassMember(string Identity, int Delta) : ClassDiagramEditOperation;
public sealed record AddClassRelationship(ClassRelationship Relationship) : ClassDiagramEditOperation;
public sealed record UpdateClassRelationship(string Identity, ClassRelationship Relationship) : ClassDiagramEditOperation;
public sealed record DeleteClassRelationship(string Identity) : ClassDiagramEditOperation;
public sealed record DuplicateClassRelationship(string Identity) : ClassDiagramEditOperation;
/// <summary>Reverse the route, retaining marker and cardinality ownership at the same actual class.</summary>
public sealed record ReverseClassRelationship(string Identity) : ClassDiagramEditOperation;
public sealed record ClassEditResult(bool Success, string? Error);
public sealed record ClassOperationCapability(bool Allowed, string Reason);
public sealed record ClassEditPlan(string Revision, IReadOnlyList<MermaidDocumentPatch> Patches, IReadOnlyList<string> StatementIdentities,
    IReadOnlyList<string> ClassIdentities, ClassDiagram Expected);
