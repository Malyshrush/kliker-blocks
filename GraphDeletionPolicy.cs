namespace KlikerBlocks;

internal static class GraphDeletionPolicy
{
    internal static List<BlockNode> Removable(IReadOnlyList<BlockNode> nodes, IReadOnlyList<BlockNode> selected)
    {
        var selectedIds = selected.Select(node => node.Id).ToHashSet();
        var preserved = nodes.Any(node => node.Kind == "Start" && !selectedIds.Contains(node.Id))
            ? null : selected.FirstOrDefault(node => node.Kind == "Start");
        return selected.Where(node => node != preserved).ToList();
    }
}
