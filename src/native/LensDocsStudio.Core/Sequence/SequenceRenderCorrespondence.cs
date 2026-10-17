using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Sequence;

/// <summary>Checks pinned renderer metadata against lexical semantics. SVG never supplies source spans or edit identities.</summary>
public static class SequenceRenderCorrespondence
{
    public static XDocument Validate(SequenceDiagramModel model, DiagramResult render)
    {
        if (!render.Success || render.Source != model.Source) throw new ArgumentException(render.Error ?? "The canonical rendering is stale.");
        using var reader = XmlReader.Create(new StringReader(render.Svg!), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var xml = XDocument.Load(reader); var root = xml.Root ?? throw new ArgumentException("The canonical drawing is empty.");
        if (root.Descendants().Take(30_001).Count() > 30_000) throw new ArgumentException("The canonical drawing exceeds the geometry budget.");
        var actors = Elements(root, "actor").Where(item => !HasClass(item, "actor-bottom")).ToArray();
        if (actors.Length != model.Participants.Count) Fail();
        for (var i = 0; i < actors.Length; i++)
        {
            var actor = model.Participants[i];
            if (Attribute(actors[i], "data-id") != actor.Id || Attribute(actors[i], "data-label") != Metadata(SequenceSourceParser.NormaliseLabel(actor.Label))
                || Attribute(actors[i], "data-type") != (actor.Actor ? "actor" : "participant")) Fail();
        }
        var messages = Elements(root, "message").ToArray(); if (messages.Length != model.Messages.Count) Fail();
        var labels = DisplayLabels(model);
        for (var i = 0; i < messages.Length; i++)
        {
            var message = model.Messages[i]; var arrow = message.Arrow.TrimStart('<');
            if (Attribute(messages[i], "data-from") != message.Sender || Attribute(messages[i], "data-to") != message.Recipient
                || Attribute(messages[i], "data-label") != Metadata(labels[message.Identity])
                || Attribute(messages[i], "data-line-style") != (arrow.StartsWith("--", StringComparison.Ordinal) ? "dashed" : "solid")
                || Attribute(messages[i], "data-arrow-head") != (arrow.Contains(">>", StringComparison.Ordinal) || arrow.Contains('x') ? "filled" : "open")
                || Attribute(messages[i], "data-self") != (message.Sender == message.Recipient ? "true" : "false")) Fail();
            var bidirectional = messages[i].Elements().Any(item => item.Attribute("marker-start") is not null);
            // Self-message drawing omits the reverse marker even for bidirectional source.
            if (message.Sender != message.Recipient && bidirectional != message.Arrow.StartsWith("<<", StringComparison.Ordinal)) Fail();
        }
        var notes = Elements(root, "sequence-note").ToArray(); if (notes.Length != model.Notes.Count) Fail();
        for (var i = 0; i < notes.Length; i++)
        {
            var note = model.Notes[i];
            if (Attribute(notes[i], "data-actors") != string.Join(',', note.Participants)
                || Attribute(notes[i], "data-position") != (note.Placement == "left of" ? "left" : note.Placement == "right of" ? "right" : "over")) Fail();
            var text = string.Concat(notes[i].Descendants().Where(item => item.Name.LocalName == "text").Select(item => item.Value));
            if (text.Replace("\n", "", StringComparison.Ordinal) != SequenceSourceParser.NormaliseLabel(note.Text).Replace("\n", "", StringComparison.Ordinal)) Fail();
        }
        var activations = Elements(root, "activation").Select(item => Attribute(item, "data-actor")).Order().ToArray();
        if (!activations.SequenceEqual(model.Activations.Where(item => item.Activate).Select(item => item.Participant).Order())) Fail();
        var blocks = Elements(root, "block").ToArray();
        var expectedBlocks = model.Statements.Where(item => item.Kind == SequenceStatementKind.Block).ToArray();
        if (blocks.Length != expectedBlocks.Length) Fail();
        var expected = expectedBlocks.Select(item => item.Read(model.Source, "type") + ":" + Metadata(SequenceSourceParser.NormaliseLabel(item.Read(model.Source, "label")))).Order();
        if (!blocks.Select(item => Attribute(item, "data-type") + ":" + Attribute(item, "data-label")).Order().SequenceEqual(expected)) Fail();
        return xml;
    }
    public static IReadOnlyDictionary<string, string> DisplayLabels(SequenceDiagramModel model)
    {
        var labels = new Dictionary<string, string>(); long number = -1, step = 1;
        foreach (var statement in model.Statements)
        {
            if (statement.Kind == SequenceStatementKind.Autonumber)
            {
                number = statement.Tokens.ContainsKey("start") ? long.Parse(statement.Read(model.Source, "start"), CultureInfo.InvariantCulture) : 1;
                step = statement.Tokens.ContainsKey("step") ? long.Parse(statement.Read(model.Source, "step"), CultureInfo.InvariantCulture) : 1;
            }
            if (statement.Kind != SequenceStatementKind.Message) continue;
            var label = SequenceSourceParser.NormaliseLabel(statement.Read(model.Source, "label"));
            labels.Add(statement.Identity, number >= 0 ? number.ToString(CultureInfo.InvariantCulture) + ". " + label : label);
            if (number >= 0) number += step;
        }
        return labels;
    }
    public static IEnumerable<XElement> Elements(XElement root, string name) => root.Descendants().Where(item => HasClass(item, name));
    public static bool HasClass(XElement item, string name) => ((string?)item.Attribute("class") ?? "").Split(' ').Contains(name);
    private static string Attribute(XElement item, string name) => (string?)item.Attribute(name) ?? "";
    private static string Metadata(string value) => value.ReplaceLineEndings(" ").Replace('\t', ' ');
    private static void Fail() => throw new ArgumentException("The canonical Sequence rendering does not match the recognised source semantics. Use Edit source; the draft is unchanged.");
}
