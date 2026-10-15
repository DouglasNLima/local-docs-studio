namespace LensDocsStudio.Core.Mermaid;

public readonly record struct MermaidLayoutBox(double X, double Y, double Width, double Height)
{
    public double CentreX => X + Width / 2;
    public double CentreY => Y + Height / 2;
}

/// <summary>Deterministic compound graph layout. Session dragging never writes coordinates into Mermaid.</summary>
public static class MermaidGraphLayout
{
    public static IReadOnlyDictionary<string, MermaidLayoutBox> Arrange(MermaidDiagramModel model)
    {
        var boxes = new Dictionary<string, MermaidLayoutBox>(StringComparer.Ordinal);
        ArrangeScope(null, model.Direction);
        return boxes;

        (double Width, double Height) ArrangeScope(string? scope, string direction)
        {
            var members = model.Nodes.Where(node => node.SubgraphId == scope).Select(node => "node:" + node.Id)
                .Concat(model.Subgraphs.Where(group => group.ParentId == scope).Select(group => "group:" + group.Id)).ToArray();
            var dimensions = new Dictionary<string, (double Width, double Height)>();
            foreach (var member in members)
            {
                if (member.StartsWith("group:", StringComparison.Ordinal))
                {
                    var group = model.Subgraph(member[6..]); var size = ArrangeScope(group.Id, group.Direction ?? direction);
                    dimensions[member] = (Math.Max(220, size.Width), Math.Max(120, size.Height));
                }
                else
                {
                    var node = model.Node(member[5..]); var lines = node.Label.Split('\n');
                    dimensions[member] = (190, Math.Clamp(44 + lines.Sum(line => Math.Max(1, (line.Length + 21) / 22)) * 17, 72, 150));
                }
            }
            var links = model.Edges.Select(edge => (From: DirectMember(edge.Source.Id), To: DirectMember(edge.Target.Id)))
                .Where(edge => edge.From is not null && edge.To is not null && edge.From != edge.To).Distinct().ToArray();
            var ranks = members.ToDictionary(member => member, _ => 0);
            var incoming = members.ToDictionary(member => member, member => links.Count(edge => edge.To == member));
            var pending = new Queue<string>(members.Where(member => incoming[member] == 0)); var visited = new HashSet<string>();
            while (pending.TryDequeue(out var member))
            {
                if (!visited.Add(member)) continue;
                foreach (var edge in links.Where(edge => edge.From == member))
                { ranks[edge.To!] = Math.Max(ranks[edge.To!], ranks[member] + 1); if (--incoming[edge.To!] == 0) pending.Enqueue(edge.To!); }
            }
            // Cycles remain in a deterministic shared layer, avoiding unbounded rank relaxation.
            var horizontal = direction is "LR" or "RL"; var cursor = 36d; var crossMaximum = 0d;
            foreach (var rank in members.GroupBy(member => ranks[member]).OrderBy(group => group.Key))
            {
                var cross = 48d; var mainSize = 0d;
                foreach (var member in rank)
                {
                    var size = dimensions[member]; var x = horizontal ? cursor : cross; var y = horizontal ? cross : cursor;
                    if (member.StartsWith("group:", StringComparison.Ordinal)) TranslateDescendants(member[6..], x, y);
                    boxes[member] = new(x, y, size.Width, size.Height);
                    cross += (horizontal ? size.Height : size.Width) + 40; mainSize = Math.Max(mainSize, horizontal ? size.Width : size.Height);
                }
                cursor += mainSize + 90; crossMaximum = Math.Max(crossMaximum, cross);
            }
            var width = horizontal ? cursor : crossMaximum + 12; var height = horizontal ? crossMaximum + 12 : cursor;
            if (direction is "RL" or "BT")
            {
                foreach (var member in members)
                {
                    var box = boxes[member]; var x = direction == "RL" ? width - box.X - box.Width : box.X;
                    var y = direction == "BT" ? height - box.Y - box.Height : box.Y;
                    if (member.StartsWith("group:", StringComparison.Ordinal)) TranslateDescendants(member[6..], x - box.X, y - box.Y);
                    boxes[member] = box with { X = x, Y = y };
                }
            }
            return (Math.Max(240, width), Math.Max(140, height));

            string? DirectMember(string nodeId)
            {
                var nodeScope = model.Node(nodeId).SubgraphId;
                if (nodeScope == scope) return "node:" + nodeId;
                while (nodeScope is not null)
                {
                    var group = model.Subgraph(nodeScope);
                    if (group.ParentId == scope) return "group:" + group.Id;
                    nodeScope = group.ParentId;
                }
                return null;
            }
        }
        void TranslateDescendants(string groupId, double deltaX, double deltaY)
        {
            foreach (var node in model.Nodes.Where(node => node.SubgraphId == groupId))
                if (boxes.TryGetValue("node:" + node.Id, out var nodeBox)) boxes["node:" + node.Id] = nodeBox with { X = nodeBox.X + deltaX, Y = nodeBox.Y + deltaY };
            foreach (var child in model.Subgraphs.Where(group => group.ParentId == groupId))
            {
                if (boxes.TryGetValue("group:" + child.Id, out var childBox)) boxes["group:" + child.Id] = childBox with { X = childBox.X + deltaX, Y = childBox.Y + deltaY };
                TranslateDescendants(child.Id, deltaX, deltaY);
            }
        }
    }
}
