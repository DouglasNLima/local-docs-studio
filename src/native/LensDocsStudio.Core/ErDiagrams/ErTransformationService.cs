using LensDocsStudio.Core.Mermaid;
using Mermaider.Models;

namespace LensDocsStudio.Core.ErDiagrams;

public static class ErCapabilityAnalysis
{
    public static ErOperationCapability For(ErDiagramModel model, ErEditOperation operation)
    {
        try { _ = new ErTransformationService().Plan(model, operation); return new(true, "Available for this source."); }
        catch (ArgumentException error) { return new(false, error.Message); }
    }
}

/// <summary>Plans exact bounded patches and an independent expected complete semantic diagram.</summary>
public sealed class ErTransformationService
{
    public ErEditPlan Plan(ErDiagramModel model, ErEditOperation operation)
    {
        if (!model.IsValid) throw new ArgumentException("Repair invalid ER source using Edit source.");
        var patches = new List<MermaidDocumentPatch>(); var identities = model.Statements.Select(item => item.Identity).ToList();
        var expected = model.Semantic; var entities = expected.Entities.ToList(); var relationships = expected.Relationships.ToList();
        void Patch(MermaidSourceSpan span, string text) { if (span.Read(model.Source) != text) patches.Add(new(span, span.Read(model.Source), text)); }
        void Token(ErStatement statement, string name, string value) => Patch(statement.Tokens[name], value);
        void Safe(ErStatement statement) { if (statement.Protected) throw new ArgumentException("This occurrence contains unrecognised syntax. Retain it exactly and use Edit source."); }
        void GlobalReferences()
        { if (model.Statements.Any(item => item.Protected)) throw new ArgumentException("Unknown source may contain references. Use Edit source for structural entity changes."); }
        void EntitySafe(ErEntityModel entity)
        {
            if (!ErDiagramSourceParser.ValidId(entity.Id)) throw new ArgumentException("This entity ID is outside the safe authoring grammar. Use Edit source to correct all of its references.");
            if (entity.Declarations.Count > 1 || model.Statements.Any(item => item.Owner == entity.Id && item.Protected) || entity.Declarations.Any(item => item.Protected))
                throw new ArgumentException("Repeated declarations or unknown entity contents protect this entity's structure. Use Edit source.");
        }
        void Insert(int at, int statementIndex, string text, int count)
        { Patch(new(at, 0), text); identities.InsertRange(statementIndex, Enumerable.Range(0, count).Select(_ => Guid.NewGuid().ToString("N"))); }
        void Append(string text, int count)
        { Insert(model.Source.Length, identities.Count, (model.Source.Length > 0 && !model.Source.EndsWith('\n') ? model.NewLine : "") + text, count); }
        void Remove(ErStatement statement)
        { Safe(statement); Patch(statement.Line, ""); identities.Remove(statement.Identity); }
        ErStatement End(ErEntityModel entity) => model.Statements.Single(item => item.Kind == ErStatementKind.End && item.Owner == entity.Id);
        void EnsureExplicit(ErEntityModel entity, string content, int count)
        {
            EntitySafe(entity);
            if (entity.Implicit) { GlobalReferences(); Append("    " + entity.Id + " {" + model.NewLine + content + "    }" + model.NewLine, count + 2); }
            else { var close = End(entity); Insert(close.Line.Start, identities.IndexOf(close.Identity), content, count); }
        }
        void ValidNew(string id)
        { Id(id); if (entities.Any(item => item.Id == id)) throw new ArgumentException("An entity already uses that ID. Choose a unique ID."); }
        void ChangeAttributes(string id, Func<List<ErAttributeInfo>, List<ErAttributeInfo>> edit)
        { var index = entities.FindIndex(item => item.Id == id); entities[index] = entities[index] with { Attributes = edit(entities[index].Attributes.ToList()).ToArray() }; }
        void AttributeEdit(string identity, Action<ErAttributeModel, ErEntityModel, int> edit)
        { var attribute = model.Attribute(identity); var entity = model.Entity(attribute.EntityId); EntitySafe(entity); Safe(attribute.Statement); edit(attribute, entity, entity.Attributes.ToList().FindIndex(item => item.Identity == identity)); }
        void RelationshipUpdate(string identity, string from, string to, ErCardinality first, ErCardinality second, bool identifying, string label)
        {
            var current = model.Relationship(identity); Safe(current.Statement); Id(from); Id(to); model.Entity(from); model.Entity(to);
            if (current.Label != label) Text(label, "relationship label", true);
            _ = ErDiagramSourceParser.Notation(first, true); _ = ErDiagramSourceParser.Notation(second, false);
            if ((current.Entity1 != from || current.Entity2 != to) && model.Statements.Any(item => item.Protected && item.Owner is null)) throw new ArgumentException("Unknown statements protect endpoint reconnection. Use Edit source.");
            CheckOrphan(current, from, to);
            Token(current.Statement, "from", from); Token(current.Statement, "to", to);
            if (current.Cardinality1 != first) Token(current.Statement, "left", ErDiagramSourceParser.Notation(first, true));
            if (current.Cardinality2 != second) Token(current.Statement, "right", ErDiagramSourceParser.Notation(second, false));
            if (current.Identifying != identifying) Token(current.Statement, "style", identifying ? "--" : "..");
            if (current.Label != label)
            {
                if (current.Statement.Tokens.TryGetValue("label", out var span) && label.Length > 0)
                { Patch(span, "\"" + label + "\""); }
                else { var at = current.Statement.Tokens["to"].End; Patch(new(at, current.Statement.Span.End - at), label.Length == 0 ? "" : " : \"" + label + "\""); }
            }
            relationships[model.Relationships.ToList().FindIndex(item => item.Identity == identity)] = new(from, to, first, second, label, identifying);
        }
        void CheckOrphan(ErRelationshipModel current, string? from = null, string? to = null)
        {
            foreach (var id in new[] { current.Entity1, current.Entity2 }.Distinct())
                if (model.Entity(id).Implicit && from != id && to != id && !model.Relationships.Any(item => item.Identity != current.Identity && (item.Entity1 == id || item.Entity2 == id)))
                    throw new ArgumentException("This change would remove the last declaration of implicit entity " + id + ". Add an attribute or update its label to make it explicit first.");
        }
        switch (operation)
        {
            case AddErEntity add:
                GlobalReferences(); ValidNew(add.Id); Text(add.Label, "entity label");
                Append("    " + Declaration(add.Id, add.Label) + model.NewLine + "    }" + model.NewLine, 2);
                entities.Add(new() { Id = add.Id, Label = add.Label, Attributes = [] }); break;
            case UpdateErEntity update:
            {
                var entity = model.Entity(update.Id); EntitySafe(entity); Id(update.NewId); if (entity.Label != update.Label) Text(update.Label, "entity label");
                if (update.Id != update.NewId) { GlobalReferences(); ValidNew(update.NewId); }
                if (entity.Implicit)
                {
                    GlobalReferences();
                    if (update.Id != update.NewId) foreach (var relationship in model.Relationships)
                    { if (relationship.Entity1 == update.Id) Token(relationship.Statement, "from", update.NewId); if (relationship.Entity2 == update.Id) Token(relationship.Statement, "to", update.NewId); }
                    Append("    " + Declaration(update.NewId, update.Label) + model.NewLine + "    }" + model.NewLine, 2);
                }
                else
                {
                    var declaration = entity.Declarations.Single();
                    Token(declaration, "id", update.NewId);
                    if (entity.Label != update.Label || update.Id != update.NewId && !declaration.Tokens.ContainsKey("label"))
                    {
                        if (declaration.Tokens.ContainsKey("label")) Token(declaration, "label", update.Label);
                        else Patch(new(declaration.Tokens["id"].End, 0), "[\"" + update.Label + "\"]");
                    }
                    if (update.Id != update.NewId) foreach (var relationship in model.Relationships)
                    { if (relationship.Entity1 == update.Id) Token(relationship.Statement, "from", update.NewId); if (relationship.Entity2 == update.Id) Token(relationship.Statement, "to", update.NewId); }
                }
                var index = entities.FindIndex(item => item.Id == update.Id); entities[index] = entities[index] with { Id = update.NewId, Label = update.Label };
                for (var i = 0; i < relationships.Count; i++) relationships[i] = relationships[i] with { Entity1 = relationships[i].Entity1 == update.Id ? update.NewId : relationships[i].Entity1, Entity2 = relationships[i].Entity2 == update.Id ? update.NewId : relationships[i].Entity2 };
                break;
            }
            case DeleteErEntity delete:
            {
                GlobalReferences(); var entity = model.Entity(delete.Id); EntitySafe(entity);
                if (model.Relationships.Any(item => item.Entity1 == entity.Id || item.Entity2 == entity.Id)) throw new ArgumentException("This entity has incident relationships. Inspect and remove or reconnect each relationship explicitly before deleting the entity.");
                var open = entity.Declarations.Single(); var close = End(entity); Patch(new(open.Line.Start, close.Line.End - open.Line.Start), "");
                identities.RemoveAll(identity => model.Statements.Any(item => item.Identity == identity && item.Span.Start >= open.Span.Start && item.Span.End <= close.Span.End));
                entities.RemoveAll(item => item.Id == entity.Id); break;
            }
            case DuplicateErEntity duplicate:
            {
                GlobalReferences(); var entity = model.Entity(duplicate.Id); EntitySafe(entity); ValidNew(duplicate.NewId);
                var content = "    " + Declaration(duplicate.NewId, entity.Label) + model.NewLine + string.Concat(entity.Attributes.Select(item => "        " + AttributeText(item.Type, item.Name, item.Keys, item.Comment) + model.NewLine)) + "    }" + model.NewLine;
                Append(content, entity.Attributes.Count + 2); entities.Add(new() { Id = duplicate.NewId, Label = entity.Label, Attributes = entity.Attributes.Select(item => item.Semantic).ToArray() }); break;
            }
            case AddErAttribute add:
                EnsureExplicit(model.Entity(add.EntityId), "        " + AttributeText(add.Type, add.Name, add.Keys, add.Comment) + model.NewLine, 1);
                ChangeAttributes(add.EntityId, values => { values.Add(new(add.Type, add.Name, add.Keys.ToArray(), add.Comment)); return values; }); break;
            case UpdateErAttribute update:
                AttributeEdit(update.Identity, (attribute, entity, index) =>
                {
                    _ = AttributeText(update.Type, update.Name, update.Keys, attribute.Comment == update.Comment ? null : update.Comment);
                    Token(attribute.Statement, "type", update.Type); Token(attribute.Statement, "name", update.Name);
                    if (!attribute.Keys.SequenceEqual(update.Keys) || attribute.Comment != update.Comment)
                    {
                        var at = attribute.Statement.Tokens["name"].End; var keys = attribute.Keys.SequenceEqual(update.Keys) ? attribute.Statement.Read(model.Source, "keys") : KeyText(update.Keys);
                        var hasComment = attribute.Statement.Tokens.TryGetValue("comment", out var originalComment);
                        var separator = hasComment ? model.Source[attribute.Statement.Tokens["keys"].End..(originalComment.Start - 1)] : " ";
                        var comment = attribute.Comment == update.Comment && hasComment
                            ? model.Source.Substring(originalComment.Start - 1, originalComment.Length + 2) : "\"" + update.Comment + "\"";
                        Patch(new(at, attribute.Statement.Span.End - at), keys + (update.Comment is null ? "" : separator + comment));
                    }
                    ChangeAttributes(entity.Id, values => { values[index] = new(update.Type, update.Name, update.Keys.ToArray(), update.Comment); return values; });
                }); break;
            case DeleteErAttribute delete:
                AttributeEdit(delete.Identity, (attribute, entity, index) => { Remove(attribute.Statement); ChangeAttributes(entity.Id, values => { values.RemoveAt(index); return values; }); }); break;
            case DuplicateErAttribute duplicate:
                AttributeEdit(duplicate.Identity, (attribute, entity, index) =>
                {
                    var text = attribute.Statement.Line.Read(model.Source); if (!text.EndsWith('\n')) text += model.NewLine;
                    Insert(attribute.Statement.Line.End, identities.IndexOf(attribute.Identity) + 1, text, 1);
                    ChangeAttributes(entity.Id, values => { values.Insert(index + 1, attribute.Semantic); return values; });
                }); break;
            case MoveErAttribute move:
                AttributeEdit(move.Identity, (attribute, entity, index) =>
                {
                    if (move.Delta is not (-1 or 1) || index + move.Delta < 0 || index + move.Delta >= entity.Attributes.Count) throw new ArgumentException("The attribute cannot move further in that direction.");
                    var other = entity.Attributes[index + move.Delta]; Safe(other.Statement);
                    // Swap declaration bytes only; interleaved comments, indentation and line endings stay in place.
                    Patch(attribute.Statement.Span, other.Statement.Span.Read(model.Source)); Patch(other.Statement.Span, attribute.Statement.Span.Read(model.Source));
                    var a = identities.IndexOf(attribute.Identity); var b = identities.IndexOf(other.Identity); (identities[a], identities[b]) = (identities[b], identities[a]);
                    ChangeAttributes(entity.Id, values => { (values[index], values[index + move.Delta]) = (values[index + move.Delta], values[index]); return values; });
                }); break;
            case AddErRelationship add:
                GlobalReferences(); model.Entity(add.Entity1); model.Entity(add.Entity2);
                Append("    " + RelationshipText(add.Entity1, add.Entity2, add.Cardinality1, add.Cardinality2, add.Identifying, add.Label) + model.NewLine, 1);
                relationships.Add(new(add.Entity1, add.Entity2, add.Cardinality1, add.Cardinality2, add.Label, add.Identifying)); break;
            case UpdateErRelationship update:
                RelationshipUpdate(update.Identity, update.Entity1, update.Entity2, update.Cardinality1, update.Cardinality2, update.Identifying, update.Label); break;
            case ReverseErRelationship reverse:
                var reversed = model.Relationship(reverse.Identity);
                RelationshipUpdate(reverse.Identity, reversed.Entity2, reversed.Entity1, reversed.Cardinality2, reversed.Cardinality1, reversed.Identifying, reversed.Label); break;
            case DeleteErRelationship delete:
                if (model.Statements.Any(item => item.Protected && item.Owner is null)) throw new ArgumentException("Unknown statements protect relationship removal. Use Edit source.");
                var deleted = model.Relationship(delete.Identity); CheckOrphan(deleted); Remove(deleted.Statement); relationships.RemoveAt(model.Relationships.ToList().FindIndex(item => item.Identity == delete.Identity)); break;
            case DuplicateErRelationship duplicate:
                var duplicated = model.Relationship(duplicate.Identity); Safe(duplicated.Statement); GlobalReferences();
                Append("    " + duplicated.Statement.Span.Read(model.Source) + model.NewLine, 1); relationships.Add(duplicated.Semantic); break;
            default: throw new ArgumentException("This ER operation is unsupported. Use Edit source.");
        }
        return new(model.Revision, patches, identities, expected with { Entities = entities.ToArray(), Relationships = relationships.ToArray() });
    }
    private static void Id(string id) { if (!ErDiagramSourceParser.ValidId(id)) throw new ArgumentException("Use an entity ID beginning with a letter or underscore, followed by letters, digits or underscores (maximum 200 characters)."); }
    private static void Text(string value, string name, bool empty = false)
    { if ((!empty && string.IsNullOrWhiteSpace(value)) || value.Length > 2000 || value.Any(character => char.IsControl(character) || character is '"' or '{' or '}' or '[' or ']') || value.Contains("<br", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use a single-line " + name + " without quotes, brackets, braces or HTML breaks (maximum 2,000 characters)."); }
    private static string Declaration(string id, string label) => id + (id == label ? "" : "[\"" + label + "\"]") + " {";
    private static string KeyText(IReadOnlyList<ErKeyType> keys)
    { if (keys.Any(key => !Enum.IsDefined(key)) || keys.Count != keys.Distinct().Count()) throw new ArgumentException("Choose each PK, FK or UK marker at most once."); return keys.Count == 0 ? "" : " " + string.Join(" , ", keys); }
    private static string AttributeText(string type, string name, IReadOnlyList<ErKeyType> keys, string? comment)
    {
        var candidate = type + " " + name + KeyText(keys) + (comment is null ? "" : " \"" + comment + "\"");
        if (comment is not null) Text(comment, "attribute comment", true);
        var parsed = new ErDiagramSourceParser().Parse("erDiagram\n    E {\n        " + candidate + "\n    }\n");
        if (!parsed.IsValid || parsed.HasUnknownDependencies || parsed.Entities.Count != 1 || parsed.Entities[0].Attributes.Count != 1 || parsed.Entities[0].Attributes[0].Type != type || parsed.Entities[0].Attributes[0].Name != name) throw new ArgumentException("Use supported single-token attribute types and names. Quotes and block delimiters require Edit source.");
        return candidate;
    }
    private static string RelationshipText(string from, string to, ErCardinality first, ErCardinality second, bool identifying, string label)
    { Id(from); Id(to); Text(label, "relationship label", true); return from + " " + ErDiagramSourceParser.Notation(first, true) + (identifying ? "--" : "..") + ErDiagramSourceParser.Notation(second, false) + " " + to + (label.Length == 0 ? "" : " : \"" + label + "\""); }
}
