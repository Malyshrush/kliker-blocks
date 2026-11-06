using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal static class GraphVariableCatalog
{
    private static readonly Regex Template = new(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}(\[)?");
    private static readonly Regex Expression = new(@"\$([A-Za-z_][A-Za-z0-9_]*)(\[)?");

    internal sealed record Usage(int BlockNumber, string BlockTitle, string FieldLabel);
    internal sealed record Entry(string Name, bool IsArray, IReadOnlyList<Usage> Usages);

    internal static bool IsArrayField(string kind, string key) => key is "Array" or "ArrayName" ||
        (kind is "ArraySet" or "ArrayAction" && key == "Name") ||
        (kind == "TranslateArray" && key is "Input" or "Output");

    internal static bool CanInsert(string kind, FieldSpec field) =>
        kind is not ("Description" or "ShowcaseStep") && field.Options is not { Length: > 0 };

    internal static IReadOnlyList<Entry> Collect(ProjectModel project)
    {
        var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < project.Nodes.Count; index++)
        {
            var node = project.Nodes[index];
            var spec = BlockCatalog.Get(node.Kind);
            foreach (var field in spec.Fields.Where(field => CanInsert(node.Kind, field)))
            {
                var usage = new Usage(index + 1, spec.Title, field.Label);
                var value = node.Value(field.Key, VariableNameRules.DefaultFor(node.Kind, field)).Trim();
                if (VariableNameRules.IsIdentifierField(node.Kind, field))
                {
                    var name = field.Key == "Reference" ? value.TrimStart('$') : value;
                    if (VariableNameRules.IsAscii(name)) Add(name, IsArrayField(node.Kind, field.Key), usage);
                }
                foreach (Match match in Template.Matches(value))
                    Add(match.Groups[1].Value, value.StartsWith('=') && match.Groups[2].Success, usage);
                if (value.StartsWith('='))
                    foreach (Match match in Expression.Matches(value))
                        Add(match.Groups[1].Value, match.Groups[2].Success, usage);
                if (node.Kind == "RunScenario" && field.Key == "Outputs")
                    foreach (var line in value.Split('\n'))
                    {
                        var separator = line.IndexOf('=');
                        if (separator <= 0) continue;
                        var name = line[..separator].Trim();
                        if (VariableNameRules.IsAscii(name)) Add(name, false, usage);
                    }
            }
        }
        return result.Values.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.IsArray).ToArray();

        void Add(string name, bool isArray, Usage usage)
        {
            var key = isArray ? name + "[]" : name;
            if (!result.TryGetValue(key, out var entry)) result.Add(key, entry = new Entry(name, isArray, new List<Usage>()));
            var usages = (List<Usage>)entry.Usages;
            if (!usages.Contains(usage)) usages.Add(usage);
        }
    }

    internal static string Insertion(Entry entry, bool identifier) => identifier ? entry.Name :
        "${" + entry.Name + "}" + (entry.IsArray ? "[0]" : "");
}
