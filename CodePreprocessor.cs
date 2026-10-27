using System.Text;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal sealed record PreprocessedLine(string Text, string? SourceFile, int SourceLine, int RootLine);

internal static class CodePreprocessor
{
    private const int MaxLines = 500;
    private const int MaxDepth = 5;
    private const int MaxFileBytes = 256 * 1024;
    private const int MaxLineLength = 4096;
    private static readonly Regex IncludePattern = new("^#include\\s+\"([^\"]+)\"\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DefinePattern = new("^#define\\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<PreprocessedLine> Process(string code, string includeDirectory)
    {
        var root = Path.GetFullPath(includeDirectory);
        if (!Directory.Exists(root)) throw new CodeImportException(1, "Папка проекта для #include не существует.");
        var result = new List<PreprocessedLine>();
        var definitions = new List<KeyValuePair<string, string>>();
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Expand(code, null, root, 0, 0, result, definitions, active);
        return result;
    }

    private static void Expand(string code, string? sourceFile, string root, int rootLine, int depth,
        List<PreprocessedLine> output, List<KeyValuePair<string, string>> definitions, HashSet<string> active)
    {
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var sourceLine = i + 1;
            var visibleLine = sourceFile is null ? sourceLine : rootLine;
            var text = lines[i].Trim();
            if (text.StartsWith("#include", StringComparison.OrdinalIgnoreCase))
            {
                var match = IncludePattern.Match(text);
                if (!match.Success) throw Error(visibleLine, sourceFile, sourceLine,
                    "#include требует имя файла в кавычках.");
                if (depth >= MaxDepth) throw Error(visibleLine, sourceFile, sourceLine,
                    "Превышена глубина #include (максимум 5).");
                var path = Path.GetFullPath(Path.Combine(root, match.Groups[1].Value));
                if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                    throw Error(visibleLine, sourceFile, sourceLine,
                        "#include разрешён только внутри папки проекта.");
                if (!File.Exists(path)) throw Error(visibleLine, sourceFile, sourceLine,
                    $"Файл #include не найден: {match.Groups[1].Value}.");
                if (HasReparsePoint(root, path)) throw Error(visibleLine, sourceFile, sourceLine,
                    "#include через ссылку или точку соединения запрещён.");
                if (new FileInfo(path).Length > MaxFileBytes) throw Error(visibleLine, sourceFile, sourceLine,
                    "Файл #include больше 256 КиБ.");
                if (!active.Add(path)) throw Error(visibleLine, sourceFile, sourceLine,
                    "Обнаружен цикл #include.");
                try
                {
                    try
                    {
                        Expand(CmsFileReader.Read(path), path, root, visibleLine, depth + 1,
                            output, definitions, active);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
                    {
                        throw Error(visibleLine, sourceFile, sourceLine,
                            $"Не удалось прочитать #include: {error.Message}");
                    }
                }
                finally { active.Remove(path); }
                continue;
            }
            if (text.StartsWith("#define", StringComparison.OrdinalIgnoreCase))
            {
                var match = DefinePattern.Match(text);
                var colon = match.Success ? match.Groups[1].Value.IndexOf(':') : -1;
                if (colon <= 0) throw Error(visibleLine, sourceFile, sourceLine,
                    "#define требует форму «искомое:замена».");
                var search = match.Groups[1].Value[..colon].Trim();
                var replacement = match.Groups[1].Value[(colon + 1)..].Trim();
                if (search.Length is < 1 or > 128 || replacement.Length > 1024 || definitions.Count >= 32)
                    throw Error(visibleLine, sourceFile, sourceLine, "Превышен предел #define: 32 правила, поиск 128 и замена 1024 символа.");
                definitions.Add(new KeyValuePair<string, string>(search, replacement));
                continue;
            }
            if (text.StartsWith("#ps2_", StringComparison.OrdinalIgnoreCase))
                throw Error(visibleLine, sourceFile, sourceLine, "Режим PS/2 отложен и не импортируется.");
            var expanded = lines[i];
            foreach (var definition in definitions)
                expanded = expanded.Replace(definition.Key, definition.Value, StringComparison.Ordinal);
            if (expanded.Length > MaxLineLength) throw Error(visibleLine, sourceFile, sourceLine,
                "Строка после #define длиннее 4096 символов.");
            output.Add(new PreprocessedLine(expanded, sourceFile, sourceLine, visibleLine));
            if (output.Count > MaxLines) throw Error(visibleLine, sourceFile, sourceLine,
                "Слишком много строк после #include (максимум 500).");
        }
    }

    private static bool HasReparsePoint(string root, string path)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return true;
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, path).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

    private static CodeImportException Error(int rootLine, string? sourceFile, int sourceLine, string reason) =>
        new(rootLine, sourceFile is null ? reason :
            $"Во включённом файле «{Path.GetFileName(sourceFile)}», строка {sourceLine}: {reason}");
}
