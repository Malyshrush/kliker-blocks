using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal static class TextFileEditing
{
    private const int MaxBytes = 8 * 1024 * 1024;
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;
    private sealed record Line(string Text, string Ending);
    private sealed record Document(string Text, Encoding Encoding, byte[] Preamble);

    private static Document Read(string path, string choice)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException(L("Текстовый файл не найден.", "Text file not found."), path);
        if (info.Length > MaxBytes) throw new InvalidOperationException(L("Файл больше 8 МиБ.", "The file exceeds 8 MiB."));
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxBytes) throw new InvalidOperationException(L("Файл больше 8 МиБ.", "The file exceeds 8 MiB."));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = choice switch
        {
            "Авто" when bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe }) => new UnicodeEncoding(false, false, true),
            "Авто" when bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff }) => new UnicodeEncoding(true, false, true),
            "Авто" or "UTF-8" => new UTF8Encoding(false, true),
            "UTF-16 LE" => new UnicodeEncoding(false, false, true),
            "UTF-16 BE" => new UnicodeEncoding(true, false, true),
            "Windows-1251" => Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            _ => throw new InvalidOperationException(L("Неизвестная кодировка файла.", "Unknown file encoding."))
        };
        byte[] marker = encoding.CodePage switch
        {
            65001 => [0xef, 0xbb, 0xbf],
            1200 => [0xff, 0xfe],
            1201 => [0xfe, 0xff],
            _ => []
        };
        var hasMarker = marker.Length > 0 && bytes.AsSpan().StartsWith(marker);
        try { return new Document(encoding.GetString(bytes, hasMarker ? marker.Length : 0, bytes.Length - (hasMarker ? marker.Length : 0)), encoding, hasMarker ? marker : []); }
        catch (DecoderFallbackException) { throw new InvalidOperationException(L("Файл не соответствует выбранной кодировке.", "The file does not match the selected encoding.")); }
    }

    private static List<Line> Lines(string text)
    {
        var lines = new List<Line>();
        foreach (Match match in Regex.Matches(text, @"([^\r\n]*)(\r\n|\r|\n|$)"))
        {
            if (match.Length == 0) continue;
            lines.Add(new Line(match.Groups[1].Value, match.Groups[2].Value));
        }
        return lines;
    }

    private static (int Start, int End) Range(List<Line> lines, string scope, int first, int last)
    {
        if (scope == "Весь файл") return (0, lines.Count);
        if (scope == "Одна строка") last = first;
        else if (scope != "Диапазон строк") throw new InvalidOperationException(L("Неизвестная область файла.", "Unknown file scope."));
        if (first < 1 || last < first || last > lines.Count)
            throw new InvalidOperationException(L("Номера строк вне диапазона файла.", "Line numbers are outside the file."));
        return (first - 1, last);
    }

    private static HashSet<Rune> Characters(string values, bool sensitive)
    {
        var set = new HashSet<Rune>();
        foreach (var value in values.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var runes = value.EnumerateRunes().ToArray();
            if (runes.Length != 1) throw new InvalidOperationException(L("В списке укажите по одному символу в каждой строке.", "Enter one character per line in the list."));
            set.Add(sensitive ? runes[0] : Rune.ToUpperInvariant(runes[0]));
        }
        return set;
    }

    internal static int Count(string text, BlockNode node)
    {
        bool sensitive = node.Value("Case") == "Да";
        var include = Characters(node.Value("Include"), sensitive);
        var exclude = Characters(node.Value("Exclude"), sensitive);
        var mode = node.Value("Mode");
        if (mode is not ("Все символы" or "Только из списка")) throw new InvalidOperationException(L("Неизвестный режим подсчёта.", "Unknown counting mode."));
        int result = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var value = sensitive ? rune : Rune.ToUpperInvariant(rune);
            if (rune.Value == ' ' && node.Value("Spaces") != "Да") continue;
            if (rune.Value == '\t' && node.Value("Tabs") != "Да") continue;
            if (rune.Value is '\r' or '\n') { if (node.Value("Newlines") != "Да") continue; }
            else if (rune.Value != '\t' && (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format) &&
                     node.Value("Controls") != "Да") continue;
            if (mode == "Только из списка" && !include.Contains(value)) continue;
            if (exclude.Contains(value)) continue;
            result++;
        }
        return result;
    }

    internal static (int Total, string[] PerLine) CountFile(string path, BlockNode node)
    {
        var doc = Read(path, node.Value("Encoding"));
        var lines = Lines(doc.Text);
        var (start, end) = Range(lines, node.Value("Scope"), node.Int("FirstLine", 1), node.Int("LastLine", 1));
        var selected = lines.GetRange(start, end - start);
        if (selected.Count > 10000 && node.Value("Array").Length > 0)
            throw new InvalidOperationException(L("Массив результата ограничен 10000 строками.", "The output array is limited to 10000 lines."));
        var text = string.Concat(selected.Select(line => line.Text + line.Ending));
        return (Count(text, node), node.Value("Array").Length == 0 ? [] :
            selected.Select(line => Count(line.Text, node).ToString(CultureInfo.InvariantCulture)).ToArray());
    }

    internal static string Edit(string path, string outputPath, BlockNode node)
    {
        var doc = Read(path, node.Value("Encoding"));
        var lines = Lines(doc.Text);
        var scope = node.Kind == "EditFileLine" ? "Одна строка" : node.Value("Scope");
        var (start, end) = Range(lines, scope, node.Int("FirstLine", 1), node.Int("LastLine", 1));
        string action = node.Value("Action");
        var chars = Characters(node.Value("Exclude"), true);
        string find = node.Value("Find");
        string replacement = node.Value("Replace");
        int minimum = node.Int("MinRepeat", 2);
        var repeat = node.Value("RepeatSymbol").EnumerateRunes().ToArray();
        if (action == "Заменить текст" && find.Length == 0)
            throw new InvalidOperationException(L("Укажите текст для поиска.", "Enter the text to find."));
        if (action == "Удалить строки по условию" && (minimum < 1 || repeat.Length > 1))
            throw new InvalidOperationException(L("Минимум повторений должен быть не меньше 1; повторяемый символ — один.", "The repetition minimum must be at least 1; enter at most one repeated character."));
        if (action == "Удалить строки по условию" && node.Value("Empty") != "Да" && node.Value("Whitespace") != "Да" && node.Value("Repeated") != "Да")
            throw new InvalidOperationException(L("Выберите хотя бы одно условие удаления строк.", "Select at least one line deletion condition."));
        string Transform(string value) => action switch
        {
            "Убрать повторные пробелы" => Regex.Replace(value, " {2,}", " "),
            "Убрать пробелы по краям" => value.Trim(' '),
            "Заменить текст" => value.Replace(find, replacement, StringComparison.Ordinal),
            "Удалить символы" => string.Concat(value.EnumerateRunes().Where(r => !chars.Contains(r)).Select(r => r.ToString())),
            "Заменить полностью" => replacement,
            "Удалить строки по условию" => value,
            _ => throw new InvalidOperationException(L("Неизвестное действие с текстом.", "Unknown text action."))
        };
        bool Delete(string value)
        {
            if (value.Length == 0) return node.Value("Empty") == "Да";
            if (node.Value("Whitespace") == "Да" && value.All(ch => ch is ' ' or '\t')) return true;
            var runes = value.EnumerateRunes().ToArray();
            return node.Value("Repeated") == "Да" && runes.Length >= minimum &&
                   runes.All(r => r == runes[0]) && (repeat.Length == 0 || runes[0] == repeat[0]);
        }
        var selected = lines.GetRange(start, end - start);
        string transformed;
        if (action == "Удалить строки по условию")
            transformed = string.Concat(selected.Where(line => !Delete(line.Text)).Select(line => line.Text + line.Ending));
        else if (node.Kind == "EditFileText" && node.Value("Processing") == "Как единый текст")
            transformed = Transform(string.Concat(selected.Select(line => line.Text + line.Ending)));
        else
            transformed = string.Concat(selected.Select(line => Transform(line.Text) + line.Ending));
        var result = string.Concat(lines.Take(start).Select(line => line.Text + line.Ending)) + transformed +
                     string.Concat(lines.Skip(end).Select(line => line.Text + line.Ending));
        byte[] data;
        try { data = doc.Encoding.GetBytes(result); }
        catch (EncoderFallbackException) { throw new InvalidOperationException(L("Новый текст нельзя записать в выбранной кодировке.", "The new text cannot be written in the selected encoding.")); }
        if (data.Length + doc.Preamble.Length > MaxBytes) throw new InvalidOperationException(L("Результат больше 8 МиБ.", "The result exceeds 8 MiB."));
        var target = Path.GetFullPath(outputPath.Length == 0 ? path : outputPath);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, ".kliker-text-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(doc.Preamble); stream.Write(data); stream.Flush(true); }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return target;
    }
}
