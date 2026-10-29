using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed class CheckpointState
{
    public int Version { get; set; } = 1;
    public string GraphHash { get; set; } = "";
    public Guid NextNode { get; set; }
    public Dictionary<string, string> Variables { get; set; } = [];
    public Dictionary<string, Dictionary<int, string>> Arrays { get; set; } = [];
}

internal static class CheckpointTools
{
    internal static string GraphHash(ProjectModel project) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(GraphCodeCodec.Serialize(project))));

    internal static void Save(string path, ProjectModel project, Guid next, VariableStore variables)
    {
        var state = new CheckpointState { GraphHash = GraphHash(project), NextNode = next,
            Variables = new Dictionary<string, string>(variables.Snapshot()),
            Arrays = variables.SnapshotArrays() };
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        if (json.Length > 4 * 1024 * 1024)
            throw new InvalidOperationException("Контрольная точка больше 4 МиБ.");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".kliker-writing-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, json); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static CheckpointState Load(string path, ProjectModel project)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Контрольная точка больше 4 МиБ.");
        var state = JsonSerializer.Deserialize<CheckpointState>(File.ReadAllText(path)) ??
            throw new InvalidDataException("Пустой файл контрольной точки.");
        if (state.Version != 1 || state.GraphHash != GraphHash(project) ||
            state.NextNode == Guid.Empty || !project.Nodes.Any(node => node.Id == state.NextNode) ||
            state.Variables is null || state.Arrays is null)
            throw new InvalidDataException("Контрольная точка не соответствует текущему графу.");
        return state;
    }
}
