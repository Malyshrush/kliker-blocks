using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal static class VariableNameRules
{
    private static readonly Regex Ascii = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex Reference = new(@"\$\{([\p{L}_][\p{L}\p{Nd}_]*)\}", RegexOptions.Compiled);
    private static readonly Regex ExpressionName = new(@"\$([\p{L}_][\p{L}\p{Nd}_]*)", RegexOptions.Compiled);
    public static bool IsAscii(string name) => Ascii.IsMatch(name);

    public static bool IsIdentifierField(string kind, FieldSpec field) => field.Key switch
    {
        "Name" => !field.Label.Contains("подпрограммы", StringComparison.OrdinalIgnoreCase) &&
            !field.Label.Contains("сценария", StringComparison.OrdinalIgnoreCase) &&
            !field.Label.Contains("файла", StringComparison.OrdinalIgnoreCase),
        "Reference" or "Result" or "Array" or "ArrayName" or "CountName" or
        "XName" or "YName" or "WidthName" or "HeightName" or "WName" or
        "LName" or "NewName" or "Confidence" or "Status" or "Error" => true,
        "Input" or "Output" => kind == "TranslateArray",
        "Count" => field.Label.Contains("переменн", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public static IReadOnlyList<string> FindInvalid(ProjectModel project)
    {
        var invalid = new List<string>();
        foreach (var node in project.Nodes)
        {
            var spec = BlockCatalog.Get(node.Kind);
            foreach (var field in spec.Fields)
            {
                var value = node.Value(field.Key, DefaultFor(node.Kind, field));
                if (IsIdentifierField(node.Kind, field))
                {
                    var name = value.Trim();
                    if (field.Key == "Reference" && name.StartsWith('$')) name = name[1..];
                    if (name.Length > 0 && !name.Contains("${", StringComparison.Ordinal) &&
                        !name.StartsWith('=') && !IsAscii(name))
                        invalid.Add($"{spec.Title} / {field.Label}: {name}");
                }
                foreach (Match match in Reference.Matches(value))
                    if (!IsAscii(match.Groups[1].Value))
                        invalid.Add($"{spec.Title} / {field.Label}: {match.Groups[1].Value}");
                if (value.StartsWith('='))
                    foreach (Match match in ExpressionName.Matches(value))
                        if (!IsAscii(match.Groups[1].Value))
                            invalid.Add($"{spec.Title} / {field.Label}: {match.Groups[1].Value}");
            }
        }
        return invalid.Distinct().Take(30).ToArray();
    }

    public static string DefaultFor(string kind, FieldSpec field)
    {
        if (field.Key == "Reference") return "$counter";
        if (field.Default.Contains("$счётчик", StringComparison.Ordinal))
            return field.Default.Replace("$счётчик", "$counter", StringComparison.Ordinal);
        if (field.Default.Contains("${окно}", StringComparison.Ordinal))
            return field.Default.Replace("${окно}", "${window}", StringComparison.Ordinal);
        if (field.Default.Contains("${элемент}", StringComparison.Ordinal))
            return field.Default.Replace("${элемент}", "${element}", StringComparison.Ordinal);
        if (!IsIdentifierField(kind, field)) return field.Default;
        if (IsAscii(field.Default)) return field.Default;
        return field.Key switch
        {
            "Name" => kind is "SetVariable" or "DefineVariable" or "UndefineVariable" or
                "MathVariable" or "IfVariable" ? "counter" : "value",
            "Result" => kind == "WindowFind" ? "window" : kind == "ElementAtPoint" ? "element" : "result",
            "Array" or "Input" => "items",
            "Output" => "translations", "Count" => "count", "Confidence" => "confidence",
            "ArrayName" => "points", "CountName" => "found_count",
            "XName" => "window_x", "YName" => "window_y",
            "WidthName" => "window_width", "HeightName" => "window_height",
            "WName" => "wparam", "LName" => "lparam", "NewName" => "is_new",
            _ => "value"
        };
    }
}
