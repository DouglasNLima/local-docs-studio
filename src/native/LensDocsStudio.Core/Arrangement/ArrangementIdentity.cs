using System.Security.Cryptography;
using System.Text;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Arrangement;

public sealed record ArrangementBlockIdentity(string Family, string SourceFingerprint, string ContextFingerprint, bool Standalone, bool UniqueSource = false);
public enum ArrangementMatchKind { Exact, Stale, Ambiguous, Missing }
public sealed record ArrangementBlockMatch(ArrangementMatchKind Kind, MermaidBlockSource? Block);

/// <summary>Conservative, path-local matching. Offsets and draft occurrence GUIDs never authorise a reload.</summary>
public static class ArrangementIdentity
{
    public static string Fingerprint(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static ArrangementBlockIdentity Capture(ParsedDocument document, MermaidBlockSource target)
    {
        var blocks = MermaidBlockSource.Find(document, target.Standalone);
        if (!blocks.Any(block => block.Body == target.Body && block.Source == target.Source))
            throw new InvalidDataException("The diagram does not belong to this document revision.");
        // Retain only a digest of the nearest authored heading. Never store source fragments or absolute paths.
        var heading = document.Syntax.OfType<Markdig.Syntax.HeadingBlock>()
            .LastOrDefault(item => item.Span.Start < target.Container.Start);
        var context = heading is null ? "document" : document.Source[heading.Span.Start..(heading.Span.End + 1)];
        var family = MermaidDiagramCatalogue.Recognise(target.Source)?.Family switch
        { MermaidDiagramFamily.Er => "ER", MermaidDiagramFamily.Flowchart => "Flowchart", _ => throw new InvalidDataException("Only ER and Flowchart blocks can own an arrangement.") };
        return new(family, Fingerprint(target.Source), Fingerprint(context), target.Standalone, blocks.Count(block => block.Source == target.Source) == 1);
    }

    public static ArrangementBlockMatch Match(ParsedDocument document, ArrangementBlockIdentity saved, bool standalone)
    {
        if (saved.Standalone != standalone) return new(ArrangementMatchKind.Missing, null);
        if (!saved.UniqueSource) return new(ArrangementMatchKind.Ambiguous, null);
        var candidates = MermaidBlockSource.Find(document, standalone)
            .Where(block => MermaidDiagramCatalogue.Recognise(block.Source)?.Family is MermaidDiagramFamily.Er or MermaidDiagramFamily.Flowchart)
            .Select(block => (Block: block, Identity: Capture(document, block)))
            .Where(item => item.Identity.Family == saved.Family).ToArray();
        var exact = candidates.Where(item => item.Identity.SourceFingerprint == saved.SourceFingerprint).ToArray();
        // Identical blocks are deliberately not disambiguated using offsets, order or nearby headings.
        // A copied/reordered duplicate cannot silently acquire another occurrence's saved arrangement.
        if (exact.Length > 1) return new(ArrangementMatchKind.Ambiguous, null);
        if (exact.Length == 1) return new(exact[0].Identity.ContextFingerprint == saved.ContextFingerprint ? ArrangementMatchKind.Exact : ArrangementMatchKind.Stale, exact[0].Block);
        var contextual = candidates.Where(item => item.Identity.ContextFingerprint == saved.ContextFingerprint).ToArray();
        return contextual.Length switch
        {
            1 => new(ArrangementMatchKind.Stale, contextual[0].Block),
            > 1 => new(ArrangementMatchKind.Ambiguous, null),
            _ => new(ArrangementMatchKind.Missing, null)
        };
    }
}
