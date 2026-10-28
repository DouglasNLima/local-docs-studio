using System.Collections.ObjectModel;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Arrangement;

public enum ArrangementStrategy { Canonical, Manual, Compact, FewerCrossings }
public readonly record struct ArrangementPosition(double X, double Y, bool Pinned = false);
public readonly record struct ArrangementBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CentreX => X + Width / 2;
    public double CentreY => Y + Height / 2;
    public bool Contains(ArrangementBox other) => other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
    public bool Intersects(ArrangementBox other, double clearance = 0) => X < other.Right + clearance && Right + clearance > other.X && Y < other.Bottom + clearance && Bottom + clearance > other.Y;
}

/// <summary>Immutable visual state. Source text and document history are deliberately absent.</summary>
public sealed class ArrangementState
{
    public const int MaximumNodes = 500;
    public const double MaximumCoordinate = 90_000;
    public IReadOnlyDictionary<string, ArrangementPosition> Positions { get; }
    public ArrangementStrategy Strategy { get; }
    public int LayoutVariant { get; }

    public ArrangementState(IEnumerable<KeyValuePair<string, ArrangementPosition>> positions, ArrangementStrategy strategy, int layoutVariant = 0)
    {
        if (!Enum.IsDefined(strategy) || layoutVariant is < 0 or > 3) throw new InvalidDataException("Unsupported arrangement strategy or routing variant.");
        var copy = new Dictionary<string, ArrangementPosition>(StringComparer.Ordinal);
        foreach (var (id, position) in positions)
        {
            if (copy.Count >= MaximumNodes || string.IsNullOrWhiteSpace(id) || id.Length > 1024 || id.Any(char.IsControl)
                || !double.IsFinite(position.X) || !double.IsFinite(position.Y)
                || Math.Abs(position.X) > MaximumCoordinate || Math.Abs(position.Y) > MaximumCoordinate || !copy.TryAdd(id, position))
                throw new InvalidDataException("The arrangement contains invalid, duplicate or excessive node positions.");
        }
        Positions = new ReadOnlyDictionary<string, ArrangementPosition>(copy); Strategy = strategy; LayoutVariant = layoutVariant;
    }

    public ArrangementState Move(string id, double x, double y)
    {
        if (!Positions.TryGetValue(id, out var previous)) throw new InvalidDataException("The selected node is no longer in this diagram.");
        return new(Positions.Select(pair => pair.Key == id ? new(pair.Key, previous with { X = x, Y = y }) : pair), ArrangementStrategy.Manual);
    }
    public ArrangementState TogglePin(string id)
    {
        if (!Positions.ContainsKey(id)) throw new InvalidDataException("The selected node is no longer in this diagram.");
        return new(Positions.Select(pair => pair.Key == id ? new(pair.Key, pair.Value with { Pinned = !pair.Value.Pinned }) : pair), Strategy, LayoutVariant);
    }
    public bool SameAs(ArrangementState other) => Strategy == other.Strategy && LayoutVariant == other.LayoutVariant && Positions.Count == other.Positions.Count
        && Positions.All(pair => other.Positions.TryGetValue(pair.Key, out var value) && value == pair.Value);
}

public sealed record ArrangementQuality(int BodyIntersections, int LabelBodyOverlaps, int Crossings, double Area, double AspectRatio, double ConnectionLength, int Bends);

/// <summary>Produced only after final geometry validation. Ordinary exports retain the original DiagramResult.</summary>
public sealed class ArrangementSnapshot
{
    public DiagramResult Render { get; }
    public ArrangementState State { get; }
    public ArrangementQuality Quality { get; }
    internal ArrangementSnapshot(DiagramResult render, ArrangementState state, ArrangementQuality quality)
    { Render = render; State = state; Quality = quality; }
}

/// <summary>Bounded layout-only history. Proposed work cannot mutate an accepted snapshot.</summary>
public sealed class ArrangementSession
{
    private readonly List<ArrangementSnapshot> history;
    private int cursor;
    private ArrangementState saved;
    public ArrangementSnapshot Canonical { get; private set; }
    public ArrangementSnapshot Current => history[cursor];
    public long Generation { get; private set; }
    public bool CanUndo => cursor > 0;
    public bool CanRedo => cursor + 1 < history.Count;
    public bool IsDirty => !Current.State.SameAs(saved);
    public ArrangementSession(ArrangementSnapshot canonical)
    { Canonical = canonical; history = [canonical]; saved = canonical.State; }
    public bool Accept(ArrangementSnapshot proposed, long expectedGeneration)
    {
        if (Generation != expectedGeneration || proposed.Render.Source != Canonical.Render.Source) return false;
        if (Current.State.SameAs(proposed.State)) return true;
        history.RemoveRange(cursor + 1, history.Count - cursor - 1); history.Add(proposed);
        while (history.Count > 1 && (history.Count > 100 || history.Sum(item => (long)(item.Render.Svg?.Length ?? 0)) > 16_000_000)) history.RemoveAt(0);
        cursor = history.Count - 1; Generation++; return true;
    }
    public void Undo() { if (CanUndo) { cursor--; Generation++; } }
    public void Redo() { if (CanRedo) { cursor++; Generation++; } }
    public void Reset() => Accept(Canonical, Generation);
    public void MarkSaved() => saved = Current.State;
    public void UpdatePresentation(ArrangementSnapshot replacement, ArrangementSnapshot canonical)
    {
        if (!replacement.State.SameAs(Current.State) || replacement.Render.Source != Canonical.Render.Source || canonical.Render.Source != Canonical.Render.Source)
            throw new InvalidDataException("An appearance update cannot change arrangement ownership or state.");
        history[cursor] = replacement; Canonical = canonical; Generation++;
    }
}
