using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal sealed class GraphCodeException(int line, string message) : Exception($"Строка {line}: {message}")
{
    public int LineNumber { get; } = line;
}

internal static class GraphCodeCodec
{
    private const int MaxTextLength = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
        { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly Regex Header = new(
        @"^block\s+(?<alias>[A-Za-z][A-Za-z0-9_]*)\s+(?<kind>[A-Za-z][A-Za-z0-9_]*)\s+(?<x>-?\d+)\s+(?<y>-?\d+)(?:\s+id\s+(?<id>[0-9A-Fa-f-]{36}))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Value = new(
        "^value\\s+(?<key>\"(?:\\\\.|[^\"\\\\])*\")\\s*=\\s*(?<value>\"(?:\\\\.|[^\"\\\\])*\")$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Link = new(
        "^link\\s+(?<key>\"(?:\\\\.|[^\"\\\\])*\")\\s*->\\s*(?<target>[A-Za-z][A-Za-z0-9_]*|none)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Serialize(ProjectModel project)
    {
        var aliases = project.Nodes.Select((node, index) => (node.Id, Name: $"b{index + 1}"))
            .ToDictionary(item => item.Id, item => item.Name);
        var result = new StringBuilder("# Kliker Blocks GRAPH 1\n");
        foreach (var node in project.Nodes)
        {
            result.Append("block ").Append(aliases[node.Id]).Append(' ').Append(node.Kind).Append(' ')
                .Append(node.X.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(node.Y.ToString(CultureInfo.InvariantCulture)).Append(" id ").Append(node.Id).AppendLine();
            if (node.LogActions) result.AppendLine("  trace true");
            foreach (var (key, value) in node.Values.OrderBy(item => item.Key, StringComparer.Ordinal))
                result.Append("  value ").Append(JsonSerializer.Serialize(key, JsonOptions)).Append(" = ")
                    .Append(JsonSerializer.Serialize(MultilineFieldText.IsMultiline(node.Kind, key)
                        ? MultilineFieldText.ForStorage(value) : value, JsonOptions)).AppendLine();
            foreach (var (key, target) in node.Links.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (target is Guid linkedId && !aliases.ContainsKey(linkedId))
                    throw new InvalidOperationException($"У блока {aliases[node.Id]} связь «{key}» ведёт на отсутствующий блок.");
                result.Append("  link ").Append(JsonSerializer.Serialize(key, JsonOptions)).Append(" -> ")
                    .Append(target is Guid id && aliases.TryGetValue(id, out var name) ? name : "none").AppendLine();
            }
            result.AppendLine("end").AppendLine();
        }
        return result.ToString();
    }

    public static List<BlockNode> Parse(string code)
    {
        if (code.Length > MaxTextLength) throw new GraphCodeException(1, "Текст графа больше 1 МиБ.");
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length > 200000) throw new GraphCodeException(1, "Слишком много строк (максимум 200000).");
        var nodes = new List<BlockNode>();
        var aliases = new Dictionary<string, BlockNode>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<Guid>();
        var pending = new List<(BlockNode Node, string Output, string Target, int Line)>();
        BlockNode? current = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var text = lines[i].Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            if (current is null)
            {
                var match = Header.Match(text);
                if (!match.Success) throw new GraphCodeException(lineNumber,
                    "Ожидается block <метка> <тип> <X> <Y> [id <GUID>].");
                var alias = match.Groups["alias"].Value;
                var kind = match.Groups["kind"].Value;
                if (alias.Equals("none", StringComparison.OrdinalIgnoreCase) || aliases.ContainsKey(alias))
                    throw new GraphCodeException(lineNumber, $"Метка «{alias}» зарезервирована или повторяется.");
                if (!BlockCatalog.All.Any(spec => spec.Kind == kind))
                    throw new GraphCodeException(lineNumber, $"Неизвестный тип блока «{kind}».");
                if (!int.TryParse(match.Groups["x"].Value, out var x) ||
                    !int.TryParse(match.Groups["y"].Value, out var y) ||
                    x is < -1000000 or > 1000000 || y is < -1000000 or > 1000000)
                    throw new GraphCodeException(lineNumber, "Координаты должны быть целыми числами от -1000000 до 1000000.");
                current = BlockCatalog.Create(kind, x, y);
                if (match.Groups["id"].Success)
                {
                    if (!Guid.TryParse(match.Groups["id"].Value, out var id) || id == Guid.Empty)
                        throw new GraphCodeException(lineNumber, "Недопустимый GUID блока.");
                    current.Id = id;
                    current.Values.Clear();
                    current.Links.Clear();
                }
                if (!ids.Add(current.Id)) throw new GraphCodeException(lineNumber, "GUID блока повторяется.");
                nodes.Add(current);
                aliases.Add(alias, current);
                if (nodes.Count > 10000) throw new GraphCodeException(lineNumber, "Слишком много блоков (максимум 10000).");
                continue;
            }
            if (text.Equals("end", StringComparison.OrdinalIgnoreCase)) { current = null; continue; }
            if (text.Equals("trace true", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("trace false", StringComparison.OrdinalIgnoreCase))
            {
                current.LogActions = text.EndsWith("true", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            var value = Value.Match(text);
            if (value.Success)
            {
                var key = ReadString(value.Groups["key"].Value, lineNumber);
                var data = ReadString(value.Groups["value"].Value, lineNumber);
                if (key.Length is < 1 or > 100) throw new GraphCodeException(lineNumber, "Недопустимое имя поля.");
                current.Values[key] = MultilineFieldText.IsMultiline(current.Kind, key)
                    ? MultilineFieldText.ForStorage(data) : data;
                continue;
            }
            var link = Link.Match(text);
            if (link.Success)
            {
                var output = ReadString(link.Groups["key"].Value, lineNumber);
                if (output.Length is < 1 or > 100) throw new GraphCodeException(lineNumber, "Недопустимое имя выхода.");
                if (pending.Any(item => item.Node == current && item.Output == output))
                    throw new GraphCodeException(lineNumber, $"Выход «{output}» указан повторно.");
                pending.Add((current, output, link.Groups["target"].Value, lineNumber));
                continue;
            }
            throw new GraphCodeException(lineNumber, "Ожидается trace true/false, value, link или end.");
        }
        if (current is not null) throw new GraphCodeException(lines.Length, "Не хватает end для последнего блока.");
        foreach (var (node, output, target, line) in pending)
        {
            if (target.Equals("none", StringComparison.OrdinalIgnoreCase)) { node.Links[output] = null; continue; }
            if (!aliases.TryGetValue(target, out var next))
                throw new GraphCodeException(line, $"Неизвестная метка назначения «{target}».");
            node.Links[output] = next.Id;
        }
        return nodes;
    }

    private static string ReadString(string token, int line)
    {
        try { return JsonSerializer.Deserialize<string>(token) ?? ""; }
        catch (JsonException) { throw new GraphCodeException(line, "Неверная строка в двойных кавычках."); }
    }
}
