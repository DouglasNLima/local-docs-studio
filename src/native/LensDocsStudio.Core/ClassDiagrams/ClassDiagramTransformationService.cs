using LensDocsStudio.Core.Mermaid;
using Mermaider.Models;

namespace LensDocsStudio.Core.ClassDiagrams;

public static class ClassDiagramCapabilityAnalysis
{
    public static ClassOperationCapability For(ClassDiagramModel model, ClassDiagramEditOperation operation)
    {
        try { _ = new ClassDiagramTransformationService().Plan(model, operation); return new(true, "Available for this exact source occurrence."); }
        catch (ArgumentException error) { return new(false, error.Message); }
    }
}

/// <summary>Exact source patches plus an independently constructed, complete expected semantic diagram.</summary>
public sealed class ClassDiagramTransformationService
{
    public ClassEditPlan Plan(ClassDiagramModel model, ClassDiagramEditOperation operation)
    {
        if (!model.IsValid) throw new ArgumentException("Repair invalid Class source using Edit source.");
        var patches = new List<MermaidDocumentPatch>(); var identities = model.Statements.Select(item => item.Identity).ToList();
        var classIdentities = model.Classes.Select(item => item.Identity).ToList(); var expected = model.Semantic;
        var classes = expected.Classes.ToList(); var relationships = expected.Relationships.ToList(); var namespaces = expected.Namespaces.ToList(); var notes = expected.Notes.ToList();
        void Patch(MermaidSourceSpan span, string text) { if (span.Read(model.Source) != text) patches.Add(new(span, span.Read(model.Source), text)); }
        void Token(ClassDiagramStatement statement, string name, string value) => Patch(statement.Tokens[name], value);
        void Safe(ClassDiagramStatement statement) { if (statement.Protected) throw new ArgumentException("This exact occurrence contains unsupported syntax. Retain it and use Edit source."); }
        void Global()
        { if (model.HasUnknownDependencies) throw new ArgumentException("Unknown source may contain dependencies. Structural changes require Edit source."); }
        void Local(ClassDiagramClass cls)
        {
            if (!ClassDiagramSourceParser.ValidId(cls.Id) || cls.Declarations.Count > 1 || cls.Declarations.Any(item => item.Protected)
                || model.Statements.Any(item => item.Protected && (item.Owner is null || item.Owner == cls.Id)))
                throw new ArgumentException("Repeated class declarations or unknown dependent source protect this class. Use Edit source.");
        }
        void ValidNew(string id)
        {
            Id(id); if (classes.Any(item => item.Id == id)) throw new ArgumentException("A class already uses that ID. Choose a unique class ID.");
            // An unresolved note is rendered as a diagram note by the pinned layout.
            // Reusing its target ID would silently turn it into a connected class note.
            if (model.Notes.Any(item => item.Semantic.TargetClassId == id))
                throw new ArgumentException("An unresolved note already references this ID. Creating, duplicating or renaming a class to it would change the note's target. Use Edit source to handle that dependency explicitly.");
        }
        void Insert(int at, int index, string text, int count)
        { Patch(new(at, 0), text); identities.InsertRange(index, Enumerable.Range(0, count).Select(_ => Guid.NewGuid().ToString("N"))); }
        void Append(string text, int count) => Insert(model.Source.Length, identities.Count, (model.Source.Length > 0 && !model.Source.EndsWith('\n') ? model.NewLine : "") + text, count);
        void Remove(ClassDiagramStatement statement) { Safe(statement); Patch(statement.Line, ""); identities.Remove(statement.Identity); }
        ClassDiagramStatement? End(ClassDiagramClass cls) => model.Statements.SingleOrDefault(item => item.Kind == ClassStatementKind.EndClass && item.Owner == cls.Id);
        void InsertMember(ClassDiagramClass cls, string content, bool method)
        {
            Local(cls);
            if (End(cls) is { } end)
            {
                // Appending to this compartment must remain after any source-backed
                // inline occurrences following the block. Never silently change their order.
                if (cls.Members.Any(item => item.Semantic.IsMethod == method && item.Statement.Span.Start > end.Span.Start))
                { Append("    " + cls.Id + " : " + content + model.NewLine, 1); return; }
                var at = end;
                if (!method && cls.Methods.FirstOrDefault(item => item.Statement.Owner == cls.Id) is { } firstMethod
                    && cls.Attributes.All(item => item.Statement.Span.Start < firstMethod.Statement.Span.Start))
                {
                    var previous = cls.Attributes.LastOrDefault()?.Statement ?? cls.Declarations.Single();
                    // Keep ordinary grouped attributes before methods, without moving or
                    // detaching interleaved comments or annotation statements.
                    if (string.IsNullOrWhiteSpace(model.Source[previous.Line.End..firstMethod.Statement.Line.Start])) at = firstMethod.Statement;
                }
                Insert(at.Line.Start, identities.IndexOf(at.Identity), "        " + content + model.NewLine, 1);
            }
            else if (cls.Implicit)
            {
                Global(); Append("    class " + cls.Id + " {" + model.NewLine + "        " + content + model.NewLine + "    }" + model.NewLine, 3);
            }
            else
            {
                // An ordinary inline member after existing occurrences preserves the
                // declaration, source ordering and namespace membership exactly.
                Append("    " + cls.Id + " : " + content + model.NewLine, 1);
            }
        }
        void ChangeMembers(string id, bool method, Func<List<ClassMember>, List<ClassMember>> edit)
        {
            var i = classes.FindIndex(item => item.Id == id); var cls = classes[i];
            classes[i] = method ? cls with { Methods = edit(cls.Methods.ToList()).ToArray() } : cls with { Attributes = edit(cls.Attributes.ToList()).ToArray() };
        }
        void MemberEdit(string identity, Action<ClassDiagramMember, ClassDiagramClass, int> edit)
        {
            var member = model.Member(identity); var cls = model.Class(member.ClassId); Local(cls); Safe(member.Statement);
            edit(member, cls, (member.Semantic.IsMethod ? cls.Methods : cls.Attributes).ToList().FindIndex(item => item.Identity == identity));
        }
        void Orphan(ClassDiagramRelationship current, ClassRelationship? replacement = null)
        {
            foreach (var id in new[] { current.Semantic.From, current.Semantic.To }.Distinct())
                if (model.Class(id).Implicit && !model.Class(id).Members.Any() && replacement?.From != id && replacement?.To != id
                    && !model.Relationships.Any(item => item.Identity != current.Identity && (item.Semantic.From == id || item.Semantic.To == id)))
                    throw new ArgumentException("This would remove the last authoritative occurrence of implicit class " + id + ". Materialise it with a member or class properties first.");
        }
        void ReferenceOrder(ClassDiagramRelationship changed, ClassRelationship? replacement)
        {
            // Relationship edits can change the first occurrence order of implicit
            // classes. Predict that order independently, retaining each class's GUID.
            var seen = new HashSet<string>(StringComparer.Ordinal); var order = new List<string>();
            var byStatement = model.Relationships.ToDictionary(item => item.Identity, item => item.Semantic);
            void Emit(string id) { if (seen.Add(id)) order.Add(id); }
            foreach (var statement in model.Statements)
            {
                if (statement.Identity == changed.Identity)
                { if (replacement is not null) { Emit(replacement.From); Emit(replacement.To); } continue; }
                if (statement.Kind is ClassStatementKind.Class or ClassStatementKind.Member && statement.Tokens.TryGetValue("id", out var id)) Emit(id.Read(model.Source));
                else if (byStatement.TryGetValue(statement.Identity, out var relationship)) { Emit(relationship.From); Emit(relationship.To); }
            }
            if (order.Count != classes.Count || order.Any(id => !classes.Any(item => item.Id == id)))
                throw new ArgumentException("The complete first-occurrence class references cannot be established. Use Edit source.");
            var nodes = classes.Select((node, index) => (node.Id, Node: node, Identity: classIdentities[index])).ToDictionary(item => item.Id, StringComparer.Ordinal);
            classes.Clear(); classIdentities.Clear(); foreach (var id in order) { classes.Add(nodes[id].Node); classIdentities.Add(nodes[id].Identity); }
        }
        void RelationshipUpdate(string identity, ClassRelationship value)
        {
            var current = model.Relationship(identity); Safe(current.Statement); ValidateRelationship(value); model.Class(value.From); model.Class(value.To);
            var endpoints = new[] { current.Semantic.From, current.Semantic.To, value.From, value.To };
            if (model.Statements.Any(item => item.Protected && (item.Owner is null || endpoints.Contains(item.Owner))))
                throw new ArgumentException("Unknown global source or protected endpoint contents may affect this relationship. Use Edit source for dependent relationship changes.");
            if (current.Semantic.From != value.From || current.Semantic.To != value.To) { Global(); ReferenceOrder(current, value); } Orphan(current, value);
            if (current.Semantic.Type != value.Type || current.Semantic.MarkerAt != value.MarkerAt) Token(current.Statement, "arrow", Notation(value.Type, value.MarkerAt));
            void Endpoint(string key, string endpoint, string id, string? before, string? after, bool first)
            {
                var token = current.Statement.Tokens[endpoint]; var has = current.Statement.Tokens.TryGetValue(key, out var card);
                var span = first ? new MermaidSourceSpan(token.Start, (has ? card.End + 1 : token.End) - token.Start)
                    : new MermaidSourceSpan(has ? card.Start - 1 : token.Start, token.End - (has ? card.Start - 1 : token.Start));
                var attachment = before == after ? (first ? model.Source[token.End..span.End] : model.Source[span.Start..token.Start])
                    : after is null ? "" : first ? " \"" + after + "\"" : "\"" + after + "\" ";
                Patch(span, first ? id + attachment : attachment + id);
            }
            Endpoint("first", "from", value.From, current.Semantic.FromCardinality, value.FromCardinality, true);
            Endpoint("second", "to", value.To, current.Semantic.ToCardinality, value.ToCardinality, false);
            if (current.Semantic.Label != value.Label)
            {
                if (current.Statement.Tokens.TryGetValue("label", out var label) && value.Label is not null)
                {
                    var raw = label.Read(model.Source);
                    Patch(raw is ['"', .., '"'] ? new(label.Start + 1, label.Length - 2) : label, value.Label);
                }
                else { var at = current.Statement.Tokens["to"].End; Patch(new(at, current.Statement.Span.End - at), value.Label is null ? "" : " : " + value.Label); }
            }
            relationships[model.Relationships.ToList().FindIndex(item => item.Identity == identity)] = value;
        }
        switch (operation)
        {
            case AddClass add:
                Global(); ValidNew(add.Id); Word(add.Generic, "generic parameter"); Word(add.Annotation, "annotation");
                Append("    class " + add.Id + GenericText(add.Generic) + " {" + model.NewLine + (add.Annotation is null ? "" : "        <<" + add.Annotation + ">>" + model.NewLine) + "    }" + model.NewLine, add.Annotation is null ? 2 : 3);
                classes.Add(new() { Id = add.Id, Label = Label(add.Id, add.Generic), Annotation = add.Annotation, Attributes = [], Methods = [] }); classIdentities.Add(Guid.NewGuid().ToString("N")); break;
            case UpdateClass update:
            {
                var cls = model.Class(update.Id); Local(cls); Id(update.NewId); Word(update.Generic, "generic parameter"); Word(update.Annotation, "annotation");
                var generic = Generic(cls); var renamed = update.Id != update.NewId;
                if (renamed) { Global(); ValidNew(update.NewId); }
                if (cls.Implicit)
                {
                    Global(); Append("    class " + update.NewId + GenericText(update.Generic) + " {" + model.NewLine + (update.Annotation is null ? "" : "        <<" + update.Annotation + ">>" + model.NewLine) + "    }" + model.NewLine, update.Annotation is null ? 2 : 3);
                }
                else
                {
                    var declaration = cls.Declarations.Single();
                    if (generic != update.Generic)
                    {
                        if (declaration.Tokens.ContainsKey("annotation")) throw new ArgumentException("Generic changes to an inline annotated declaration require Edit source.");
                        if (declaration.Tokens.TryGetValue("generic", out var span)) Patch(new(span.Start - 1, span.Length + 2), GenericText(update.Generic));
                        else Patch(new(declaration.Tokens["id"].End, 0), GenericText(update.Generic));
                    }
                    if (cls.Annotation != update.Annotation)
                    {
                        var annotations = model.Statements.Where(item => item.Owner == cls.Id && item.Kind == ClassStatementKind.Annotation).ToArray();
                        if (annotations.Length > 1) throw new ArgumentException("Repeated annotations require Edit source.");
                        if (declaration.Tokens.TryGetValue("annotation", out var inline))
                        { if (update.Annotation is null) throw new ArgumentException("Remove an inline annotation using Edit source."); Patch(inline, update.Annotation); }
                        else if (annotations.Length == 1) { if (update.Annotation is null) Remove(annotations[0]); else Token(annotations[0], "annotation", update.Annotation); }
                        else if (End(cls) is { } end) Insert(end.Line.Start, identities.IndexOf(end.Identity), "        <<" + update.Annotation + ">>" + model.NewLine, 1);
                        else throw new ArgumentException("Add an annotation to this declaration using Edit source.");
                    }
                }
                if (renamed) foreach (var statement in model.Statements)
                {
                    foreach (var key in new[] { "id", "from", "to" }) if (statement.Tokens.TryGetValue(key, out var span) && span.Read(model.Source) == update.Id) Patch(span, update.NewId);
                }
                var index = classes.FindIndex(item => item.Id == update.Id); classes[index] = classes[index] with { Id = update.NewId, Label = Label(update.NewId, update.Generic), Annotation = update.Annotation };
                for (var i = 0; i < relationships.Count; i++) relationships[i] = relationships[i] with { From = relationships[i].From == update.Id ? update.NewId : relationships[i].From, To = relationships[i].To == update.Id ? update.NewId : relationships[i].To };
                for (var i = 0; i < namespaces.Count; i++) namespaces[i] = namespaces[i] with { ClassIds = namespaces[i].ClassIds.Select(id => id == update.Id ? update.NewId : id).ToArray() };
                for (var i = 0; i < notes.Count; i++) if (notes[i].TargetClassId == update.Id) notes[i] = notes[i] with { TargetClassId = update.NewId };
                break;
            }
            case DeleteClass delete:
            {
                Global(); var cls = model.Class(delete.Id); Local(cls);
                if (model.Relationships.Any(item => item.Semantic.From == cls.Id || item.Semantic.To == cls.Id) || model.Notes.Any(item => item.Semantic.TargetClassId == cls.Id)) throw new ArgumentException("This class has relationships or notes. Handle each dependency explicitly before deleting it.");
                if (model.Namespaces.Any(item => item.ClassIds.Contains(cls.Id))) throw new ArgumentException("Deleting a namespace member requires Edit source.");
                if (cls.Implicit) throw new ArgumentException("An implicit class has no independent declaration to remove.");
                var declaration = cls.Declarations.Single();
                if (End(cls) is { } close)
                {
                    Patch(new(declaration.Line.Start, close.Line.End - declaration.Line.Start), "");
                    identities.RemoveAll(id => model.Statements.Any(item => item.Identity == id && item.Span.Start >= declaration.Span.Start && item.Span.End <= close.Span.End));
                }
                else Remove(declaration);
                foreach (var member in cls.Members.Where(item => item.Statement.Owner is null)) Remove(member.Statement);
                classes.RemoveAll(item => item.Id == cls.Id); classIdentities.Remove(cls.Identity); break;
            }
            case DuplicateClass duplicate:
            {
                Global(); var cls = model.Class(duplicate.Id); Local(cls); ValidNew(duplicate.NewId);
                var text = "    class " + duplicate.NewId + GenericText(Generic(cls)) + " {" + model.NewLine;
                if (cls.Annotation is not null) text += "        <<" + cls.Annotation + ">>" + model.NewLine;
                text += string.Concat(cls.Members.Select(item => "        " + item.Statement.Read(model.Source, "member") + model.NewLine)) + "    }" + model.NewLine;
                Append(text, 2 + cls.Members.Count + (cls.Annotation is null ? 0 : 1)); classes.Add(cls.Semantic with { Id = duplicate.NewId, Label = Label(duplicate.NewId, Generic(cls)) }); classIdentities.Add(Guid.NewGuid().ToString("N")); break;
            }
            case AddClassMember add:
                var memberText = MemberText(add.Member); InsertMember(model.Class(add.ClassId), memberText, add.Member.IsMethod);
                ChangeMembers(add.ClassId, add.Member.IsMethod, items => { items.Add(add.Member); return items; }); break;
            case UpdateClassMember update:
                MemberEdit(update.Identity, (member, cls, index) =>
                {
                    _ = MemberText(update.Member); if (member.Semantic.IsMethod != update.Member.IsMethod) throw new ArgumentException("Changing member classification requires Edit source.");
                    var statement = member.Statement; var edits = new List<(MermaidSourceSpan Span, string Text)>();
                    void Edit(string token, string value) { var span = statement.Tokens[token]; if (span.Read(model.Source) != value) edits.Add((span, value)); }
                    Edit("visibility", Visibility(update.Member.Visibility)); Edit("name", update.Member.Name); Edit("modifier", Modifier(update.Member));
                    if (update.Member.IsMethod && member.Semantic.Params != update.Member.Params) Edit("parameters", update.Member.Params ?? "");
                    if (member.Semantic.Type != update.Member.Type)
                    {
                        if (statement.Tokens.TryGetValue("type", out var type))
                        {
                            if (update.Member.Type is not null) edits.Add((type, update.Member.Type));
                            else if (update.Member.IsMethod) { var at = statement.Tokens["parameters"].End + 1; edits.Add((new(at, type.End - at), "")); }
                            else edits.Add((new(type.Start, statement.Tokens["name"].Start - type.Start), ""));
                        }
                        else edits.Add((new(update.Member.IsMethod ? statement.Tokens["parameters"].End + 1 : statement.Tokens["name"].Start, 0), update.Member.IsMethod ? " " + update.Member.Type : update.Member.Type + " "));
                    }
                    // Plan one signature patch from independent token replacements. Adjacent insertions
                    // share a lexical boundary (visibility/type/name); their explicit order is retained.
                    var signature = statement.Tokens["member"]; var original = signature.Read(model.Source); var output = original;
                    foreach (var group in edits.GroupBy(item => item.Span.Start).OrderByDescending(item => item.Key))
                    {
                        var replacements = group.Where(item => item.Span.Length > 0).ToArray(); if (replacements.Length > 1) throw new ArgumentException("Member token edits overlap.");
                        var length = replacements.FirstOrDefault().Span.Length; var replacement = string.Concat(group.Where(item => item.Span.Length == 0).Select(item => item.Text)) + (replacements.Length == 0 ? "" : replacements[0].Text);
                        output = output.Remove(group.Key - signature.Start, length).Insert(group.Key - signature.Start, replacement);
                    }
                    var prefix = 0; while (prefix < original.Length && prefix < output.Length && original[prefix] == output[prefix]) prefix++;
                    var suffix = 0; while (suffix < original.Length - prefix && suffix < output.Length - prefix && original[^(suffix + 1)] == output[^(suffix + 1)]) suffix++;
                    Patch(new(signature.Start + prefix, original.Length - prefix - suffix), output.Substring(prefix, output.Length - prefix - suffix));
                    ChangeMembers(cls.Id, member.Semantic.IsMethod, items => { items[index] = update.Member; return items; });
                }); break;
            case DeleteClassMember delete:
                MemberEdit(delete.Identity, (member, cls, index) => { Remove(member.Statement); ChangeMembers(cls.Id, member.Semantic.IsMethod, items => { items.RemoveAt(index); return items; }); }); break;
            case DuplicateClassMember duplicate:
                MemberEdit(duplicate.Identity, (member, cls, index) =>
                {
                    var raw = member.Statement.Line.Read(model.Source); if (!raw.EndsWith('\n')) raw += model.NewLine;
                    Insert(member.Statement.Line.End, identities.IndexOf(member.Identity) + 1, raw, 1);
                    ChangeMembers(cls.Id, member.Semantic.IsMethod, items => { items.Insert(index + 1, member.Semantic); return items; });
                }); break;
            case MoveClassMember move:
                MemberEdit(move.Identity, (member, cls, index) =>
                {
                    var members = member.Semantic.IsMethod ? cls.Methods : cls.Attributes;
                    if (move.Delta is not (-1 or 1) || index + move.Delta < 0 || index + move.Delta >= members.Count) throw new ArgumentException("This member cannot move further in that direction.");
                    var other = members[index + move.Delta]; Safe(other.Statement);
                    var first = member.Statement.Line.Start < other.Statement.Line.Start ? member.Statement : other.Statement; var second = ReferenceEquals(first, member.Statement) ? other.Statement : member.Statement;
                    // A comment before the first swapped row can belong to that row,
                    // even when the gap between the two rows contains only whitespace.
                    // Keep v1 movement conservative across the complete owning span.
                    var scopeStart = cls.Members.Select(item => item.Statement.Line.Start).Concat(cls.Declarations.Select(item => item.Line.Start)).Min();
                    var scopeEnd = cls.Members.Select(item => item.Statement.Line.End).Concat(cls.Declarations.Select(item => item.Line.End)).Append(End(cls)?.Line.End ?? 0).Max();
                    if (!cls.Declarations.Any(item => item.Line.Start == scopeStart))
                    {
                        while (scopeStart > 0)
                        {
                            var previousStart = model.Source.LastIndexOf('\n', Math.Max(0, scopeStart - 2)) + 1;
                            if (previousStart >= scopeStart) break;
                            var previous = model.Source[previousStart..scopeStart].Trim();
                            if (previous.Length > 0 && !previous.StartsWith("%%", StringComparison.Ordinal)) break;
                            scopeStart = previousStart;
                        }
                    }
                    if (model.Source[scopeStart..scopeEnd].Split('\n').Any(line => line.TrimStart().StartsWith("%%", StringComparison.Ordinal)))
                        throw new ArgumentException("Comments in this class or member span may be attached to a row. Reorder using Edit source; member properties remain available.");
                    if (!string.IsNullOrWhiteSpace(model.Source[first.Line.End..second.Line.Start])) throw new ArgumentException("Comments or intervening statements make member attachment ambiguous. Reorder using Edit source.");
                    Patch(member.Statement.Span, other.Statement.Span.Read(model.Source)); Patch(other.Statement.Span, member.Statement.Span.Read(model.Source));
                    var a = identities.IndexOf(member.Identity); var b = identities.IndexOf(other.Identity); (identities[a], identities[b]) = (identities[b], identities[a]);
                    ChangeMembers(cls.Id, member.Semantic.IsMethod, items => { (items[index], items[index + move.Delta]) = (items[index + move.Delta], items[index]); return items; });
                }); break;
            case AddClassRelationship add:
                Global(); ValidateRelationship(add.Relationship); model.Class(add.Relationship.From); model.Class(add.Relationship.To);
                Append("    " + RelationshipText(add.Relationship) + model.NewLine, 1); relationships.Add(add.Relationship); break;
            case UpdateClassRelationship update: RelationshipUpdate(update.Identity, update.Relationship); break;
            case ReverseClassRelationship reverse:
                var reversed = model.Relationship(reverse.Identity);
                if (reversed.Statement.Read(model.Source, "arrow") == "--") throw new ArgumentException("A portable solid link has no marker to reverse, although the pinned renderer models it as an association. Choose an explicit relationship type/marker or use Edit source.");
                var value = reversed.Semantic;
                RelationshipUpdate(reverse.Identity, value with { From = value.To, To = value.From, MarkerAt = value.MarkerAt == ClassMarkerAt.From ? ClassMarkerAt.To : ClassMarkerAt.From, FromCardinality = value.ToCardinality, ToCardinality = value.FromCardinality }); break;
            case DeleteClassRelationship delete:
                Global(); var deleted = model.Relationship(delete.Identity); Orphan(deleted); ReferenceOrder(deleted, null); Remove(deleted.Statement); relationships.RemoveAt(model.Relationships.ToList().FindIndex(item => item.Identity == delete.Identity)); break;
            case DuplicateClassRelationship duplicate:
                Global(); var duplicated = model.Relationship(duplicate.Identity); Safe(duplicated.Statement);
                Append("    " + duplicated.Statement.Span.Read(model.Source) + model.NewLine, 1); relationships.Add(duplicated.Semantic); break;
            default: throw new ArgumentException("This Class operation is protected. Use Edit source.");
        }
        _ = MermaidPatches.Apply(model.Source, patches);
        return new(model.Revision, patches, identities, classIdentities, expected with { Classes = classes.ToArray(), Relationships = relationships.ToArray(), Namespaces = namespaces.ToArray(), Notes = notes.ToArray() });
    }
    public static string? Generic(ClassDiagramClass cls) => cls.Label == cls.Id ? null : cls.Label.StartsWith(cls.Id + "<", StringComparison.Ordinal) && cls.Label.EndsWith('>') ? cls.Label[(cls.Id.Length + 1)..^1] : throw new ArgumentException("This generic class label requires Edit source.");
    private static string Label(string id, string? generic) => generic is null ? id : id + "<" + generic + ">";
    private static string GenericText(string? generic) => generic is null ? "" : "~" + generic + "~";
    private static void Id(string id) { if (!ClassDiagramSourceParser.ValidId(id)) throw new ArgumentException("Use a class ID beginning with a letter or underscore, followed by letters, digits or underscores (maximum 200 characters)."); }
    private static void Word(string? text, string kind) { if (text is not null && !ClassDiagramSourceParser.ValidId(text)) throw new ArgumentException("Use one identifier for the " + kind + ". Other forms require Edit source."); }
    public static string Visibility(ClassVisibility visibility) => visibility switch { ClassVisibility.None => "", ClassVisibility.Public => "+", ClassVisibility.Private => "-", ClassVisibility.Protected => "#", ClassVisibility.Package => "~", _ => throw new ArgumentException("Choose a supported visibility.") };
    private static string Modifier(ClassMember member) => member.IsStatic && member.IsAbstract ? throw new ArgumentException("Combined static and abstract markers require Edit source.") : member.IsStatic ? "$" : member.IsAbstract ? "*" : "";
    public static string MemberText(ClassMember member)
    {
        if (member.IsAbstract || member.IsMethod && member.IsStatic) throw new ArgumentException("The pinned parser cannot safely author portable method classifiers or abstract attributes. Existing markers remain protected and byte-exact; use Edit source.");
        Word(member.Name, "member name"); if (member.Name.Length == 0) throw new ArgumentException("A member name is required.");
        var text = Visibility(member.Visibility) + (member.IsMethod ? member.Name + Modifier(member) + "(" + member.Params + ")" + (member.Type is null ? "" : " " + member.Type) : (member.Type is null ? "" : member.Type + " ") + member.Name + Modifier(member));
        var parsed = new ClassDiagramSourceParser().Parse("classDiagram\nclass A {\n" + text + "\n}\n");
        if (!parsed.IsValid || parsed.HasUnknownDependencies || parsed.Classes.Count != 1 || parsed.Classes[0].Members.Count != 1 || parsed.Classes[0].Members[0].Semantic != member)
            throw new ArgumentException("Use bounded single-line member names, types and parameter text without syntax delimiters. Unfamiliar modifier placement requires Edit source.");
        return text;
    }
    public static string Notation(ClassRelationType type, ClassMarkerAt marker)
    {
        if (!Enum.IsDefined(marker)) throw new ArgumentException("Choose the class endpoint owning the marker.");
        var first = marker == ClassMarkerAt.From;
        return type switch { ClassRelationType.Inheritance => first ? "<|--" : "--|>", ClassRelationType.Composition => first ? "*--" : "--*", ClassRelationType.Aggregation => first ? "o--" : "--o", ClassRelationType.Association => first ? "<--" : "-->", ClassRelationType.Dependency => first ? "<.." : "..>", ClassRelationType.Realization => first ? "<|.." : "..|>", _ => throw new ArgumentException("Lollipop relationships have synthetic renderer identities and remain protected for authoring.") };
    }
    private static void ValidateRelationship(ClassRelationship value)
    {
        Id(value.From); Id(value.To); _ = Notation(value.Type, value.MarkerAt);
        if (!ClassDiagramSourceParser.SafeText(value.Label) || !ClassDiagramSourceParser.SafeText(value.FromCardinality) || !ClassDiagramSourceParser.SafeText(value.ToCardinality) || value.Label is "" || value.Label != value.Label?.Trim())
            throw new ArgumentException("Use bounded single-line relationship labels/cardinalities without quotes, HTML or source delimiters. Empty labels use no label.");
    }
    private static string RelationshipText(ClassRelationship value) => value.From + (value.FromCardinality is null ? "" : " \"" + value.FromCardinality + "\"") + " " + Notation(value.Type, value.MarkerAt) + (value.ToCardinality is null ? "" : " \"" + value.ToCardinality + "\"") + " " + value.To + (value.Label is null ? "" : " : " + value.Label);
}
