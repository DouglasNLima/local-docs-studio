using Mermaider.Models;

namespace LensDocsStudio.Core.Arrangement;

/// <summary>Bounded orthogonal visibility-grid routing for the separate arranged presentation.</summary>
internal static class ArrangementRouter
{
    private const double Clearance = 18;
    internal static bool Crosses(Point a, Point b, ArrangementBox box, double margin = 0)
    {
        var left = box.X - margin; var right = box.Right + margin; var top = box.Y - margin; var bottom = box.Bottom + margin;
        if (Math.Abs(a.X - b.X) < .001) return a.X > left + .001 && a.X < right - .001 && Math.Max(a.Y, b.Y) > top + .001 && Math.Min(a.Y, b.Y) < bottom - .001;
        if (Math.Abs(a.Y - b.Y) < .001) return a.Y > top + .001 && a.Y < bottom - .001 && Math.Max(a.X, b.X) > left + .001 && Math.Min(a.X, b.X) < right - .001;
        // Arrangement routes are orthogonal. Unknown segments cannot pass validation.
        return true;
    }

    internal static IReadOnlyList<Point> Route(string from, string to, IReadOnlyDictionary<string, ArrangementBox> boxes,
        int ordinal, int parallelIndex, bool rectangular, CancellationToken token, ArrangementBox? labelRail = null)
    {
        var first = boxes[from]; var last = boxes[to];
        var horizontal = Math.Abs(last.CentreX - first.CentreX) > Math.Abs(last.CentreY - first.CentreY);
        var firstSide = horizontal ? (last.CentreX >= first.CentreX ? 1 : 3) : (last.CentreY >= first.CentreY ? 2 : 0);
        var lastSide = (firstSide + 2) % 4;
        if (from == to) { firstSide = 1; lastSide = 2; }
        // ER boxes have multiple ports; other shapes use their exact cardinal extrema.
        var fraction = rectangular ? .25 + (ordinal % 7) / 12d : .5;
        static Point Port(ArrangementBox box, int side, double fraction) => side switch
        {
            0 => new(box.X + box.Width * fraction, box.Y), 1 => new(box.Right, box.Y + box.Height * fraction),
            2 => new(box.X + box.Width * fraction, box.Bottom), _ => new(box.X, box.Y + box.Height * fraction)
        };
        static Point Stub(Point port, int side, double length) => side switch
        { 0 => new(port.X, port.Y - length), 1 => new(port.X + length, port.Y), 2 => new(port.X, port.Y + length), _ => new(port.X - length, port.Y) };
        var length = Clearance + 10 + Math.Min(parallelIndex, 12) * 6;
        bool SafePort(ArrangementBox box, string owner, int side, double offset)
        {
            var port = Port(box, side, offset); var stub = Stub(port, side, length);
            return stub.X >= 2 && stub.Y >= 2 && !boxes.Any(pair => pair.Key != owner && Crosses(port, stub, pair.Value, Clearance - .01));
        }
        firstSide = Enumerable.Range(0, 4).Select(i => (firstSide + i) % 4).FirstOrDefault(side => SafePort(first, from, side, fraction), -1);
        lastSide = Enumerable.Range(0, 4).Select(i => (lastSide + i) % 4).FirstOrDefault(side => (from != to || side != firstSide) && SafePort(last, to, side, rectangular ? 1 - fraction : .5), -1);
        if (firstSide < 0 || lastSide < 0) throw new InvalidDataException("There is insufficient clearance for a connection port. Move the node further from its neighbours.");
        var start = Port(first, firstSide, fraction); var end = Port(last, lastSide, rectangular ? 1 - fraction : .5);
        var departure = Stub(start, firstSide, length); var arrival = Stub(end, lastSide, length);
        if (departure.X < 2 || departure.Y < 2 || arrival.X < 2 || arrival.Y < 2
            || boxes.Any(pair => pair.Key != from && Crosses(start, departure, pair.Value, 2))
            || boxes.Any(pair => pair.Key != to && Crosses(arrival, end, pair.Value, 2)))
            throw new InvalidDataException("There is insufficient clearance for a connection port. Move the node further from its neighbours or the canvas edge.");
        // Distinct occurrences need a visible lane, not just different stub lengths
        // which disappear when aligned segments are simplified. Reciprocal edges
        // share the same occurrence counter supplied by the engine.
        if (labelRail is null && parallelIndex > 0)
            labelRail = new ArrangementBox(boxes.Values.Max(b => b.Right) + 48 + parallelIndex * 18,
                boxes.Values.Max(b => b.Bottom) + 48 + parallelIndex * 18, 24, 4);
        var points = new List<Point> { start };
        if (labelRail is { } rail)
        {
            var railStart = new Point(rail.X - 24, rail.CentreY); var railEnd = new Point(rail.Right + 24, rail.CentreY);
            points.AddRange(Search(departure, railStart, boxes.Values.ToArray(), token));
            points.Add(railEnd); points.AddRange(Search(railEnd, arrival, boxes.Values.ToArray(), token));
        }
        else points.AddRange(Search(departure, arrival, boxes.Values.ToArray(), token));
        points.Add(end);
        static bool Between(Point a, Point middle, Point b) => middle.X >= Math.Min(a.X, b.X) && middle.X <= Math.Max(a.X, b.X)
            && middle.Y >= Math.Min(a.Y, b.Y) && middle.Y <= Math.Max(a.Y, b.Y);
        for (var i = points.Count - 2; i > 0; i--)
            if (points[i] == points[i - 1] || points[i] == points[i + 1]
                || Between(points[i - 1], points[i], points[i + 1]) &&
                (Math.Abs(points[i - 1].X - points[i].X) < .001 && Math.Abs(points[i].X - points[i + 1].X) < .001
                || Math.Abs(points[i - 1].Y - points[i].Y) < .001 && Math.Abs(points[i].Y - points[i + 1].Y) < .001)) points.RemoveAt(i);
        if (points.Count < 2) throw new InvalidDataException("The connection has no safe route.");
        return points.AsReadOnly();
    }

    private static IReadOnlyList<Point> Search(Point start, Point end, ArrangementBox[] boxes, CancellationToken token)
    {
        var xs = boxes.SelectMany(b => new[] { b.X - Clearance, b.Right + Clearance }).Append(start.X).Append(end.X).Append(2).Where(x => x >= 2).Distinct().Order().ToArray();
        var ys = boxes.SelectMany(b => new[] { b.Y - Clearance, b.Bottom + Clearance }).Append(start.Y).Append(end.Y).Append(2).Where(y => y >= 2).Distinct().Order().ToArray();
        if ((long)xs.Length * ys.Length > 180_000) throw new InvalidDataException("This arrangement exceeds the bounded routing grid. Use canonical rendering.");
        var width = xs.Length; var count = checked(width * ys.Length);
        int Index(Point p) => Array.BinarySearch(ys, p.Y) * width + Array.BinarySearch(xs, p.X);
        Point At(int index) => new(xs[index % width], ys[index / width]);
        var first = Index(start); var target = Index(end);
        var costs = new double[count * 2]; Array.Fill(costs, double.PositiveInfinity);
        var parents = new int[count * 2]; Array.Fill(parents, -1);
        var queue = new PriorityQueue<int, (double Cost, int Identity)>();
        for (var direction = 0; direction < 2; direction++) { costs[first * 2 + direction] = 0; queue.Enqueue(first * 2 + direction, (0, first * 2 + direction)); }
        var visited = new bool[count * 2]; var expanded = 0;
        while (queue.TryDequeue(out var state, out _))
        {
            if (visited[state]) continue;
            visited[state] = true;
            if ((++expanded & 127) == 0) token.ThrowIfCancellationRequested();
            if (expanded > 180_000) throw new InvalidDataException("Connection routing reached its search limit; the previous arrangement is retained.");
            var current = state / 2; var point = At(current);
            if (current == target)
            {
                var result = new List<Point>();
                for (var step = state; step >= 0; step = parents[step]) result.Add(At(step / 2));
                result.Reverse(); return result;
            }
            var column = current % width; var row = current / width;
            Try(column > 0 ? current - 1 : -1, 0); Try(column + 1 < width ? current + 1 : -1, 0);
            Try(row > 0 ? current - width : -1, 1); Try(row + 1 < ys.Length ? current + width : -1, 1);
            void Try(int next, int direction)
            {
                if (next < 0 || visited[next * 2 + direction]) return;
                var destination = At(next);
                if (boxes.Any(box => Crosses(point, destination, box, Clearance - .01))) return;
                var cost = costs[state] + Math.Abs(point.X - destination.X) + Math.Abs(point.Y - destination.Y) + (state % 2 == direction ? 0 : 12);
                var identity = next * 2 + direction;
                if (cost >= costs[identity]) return;
                costs[identity] = cost; parents[identity] = state;
                queue.Enqueue(identity, (cost + Math.Abs(destination.X - end.X) + Math.Abs(destination.Y - end.Y), identity));
            }
        }
        throw new InvalidDataException("No safe connection route fits these positions. The previous arrangement is retained.");
    }

    internal static (Point Position, ArrangementBox Bounds) Label(IReadOnlyList<Point> route, double width, double height,
        IEnumerable<ArrangementBox> bodies, IReadOnlyList<ArrangementBox> previous)
    {
        var boxes = bodies.ToArray();
        var candidates = new List<(Point Point, double Length)>();
        for (var i = 1; i < route.Count; i++)
        {
            var a = route[i - 1]; var b = route[i];
            var length = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
            foreach (var fraction in new[] { .5, .25, .75 }) candidates.Add((new(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction), length));
        }
        foreach (var candidate in candidates.OrderByDescending(item => item.Length))
        {
            var p = candidate.Point; var bounds = new ArrangementBox(p.X - width / 2, p.Y - height / 2, width, height);
            if (bounds.X >= 2 && bounds.Y >= 2 && !boxes.Any(box => box.Intersects(bounds, 4)) && !previous.Any(box => box.Intersects(bounds, 2))) return (p, bounds);
        }
        throw new InvalidDataException("A relationship label cannot fit without overlapping a node or another label. Move the nodes further apart.");
    }
}
