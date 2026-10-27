using System.Drawing;

namespace KlikerBlocks;

internal static class BlockClipboard
{
    public static List<BlockNode> Copy(IEnumerable<BlockNode> selected)
    {
        var nodes = selected.Where(node => node.Kind != "Start").ToList();
        var ids = nodes.Select(node => node.Id).ToHashSet();
        return nodes.Select(node => new BlockNode
        {
            Id = node.Id,
            Kind = node.Kind,
            X = node.X,
            Y = node.Y,
            LogActions = node.LogActions,
            Values = new Dictionary<string, string>(node.Values),
            Links = node.Links.ToDictionary(link => link.Key,
                link => link.Value is Guid target && ids.Contains(target) ? (Guid?)target : null),
            LinkStyles = node.LinkStyles.ToDictionary(style => style.Key, style => style.Value.Copy()),
            Appearance = node.Appearance?.Copy()
        }).ToList();
    }

    public static List<BlockNode> Paste(IReadOnlyList<BlockNode> snapshot, Point topLeft, ProjectModel project)
    {
        if (snapshot.Count == 0) return [];
        var originX = snapshot.Min(node => node.X);
        var originY = snapshot.Min(node => node.Y);
        var ids = snapshot.ToDictionary(node => node.Id, _ => Guid.NewGuid());
        var copies = snapshot.Select(node => new BlockNode
        {
            Id = ids[node.Id],
            Kind = node.Kind,
            X = topLeft.X + node.X - originX,
            Y = topLeft.Y + node.Y - originY,
            LogActions = node.LogActions,
            Values = new Dictionary<string, string>(node.Values),
            Links = node.Links.ToDictionary(link => link.Key,
                link => link.Value is Guid target && ids.TryGetValue(target, out var mapped) ? (Guid?)mapped : null),
            LinkStyles = node.LinkStyles.ToDictionary(style => style.Key, style => style.Value.Copy()),
            Appearance = node.Appearance?.Copy()
        }).ToList();
        project.Nodes.AddRange(copies);
        return copies;
    }
}
