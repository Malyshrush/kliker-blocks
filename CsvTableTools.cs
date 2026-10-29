using System.Globalization;
using System.Text;

namespace KlikerBlocks;

// RFC 4180-style quoted fields, including escaped quotes and embedded newlines.
internal static class CsvTableTools
{
    private const long MaxBytes = 8 * 1024 * 1024;
    private const int MaxRows = 10000;
    private const int MaxColumns = 256;

    internal static List<List<string>> Read(string path, char delimiter)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("CSV-файл не найден.", path);
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidOperationException("CSV-файл больше 8 МиБ.");
        var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
        return Parse(text, delimiter);
    }

    internal static List<List<string>> Parse(string text, char delimiter)
    {
        if (delimiter is not (',' or ';' or '\t'))
            throw new InvalidOperationException("Разделитель CSV: запятая, точка с запятой или табуляция.");
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var quoteClosed = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') { quoted = false; quoteClosed = true; }
                else field.Append(ch);
                continue;
            }
            if (ch == '"')
            {
                if (field.Length != 0 || quoteClosed) throw new InvalidDataException("Неверные кавычки в CSV.");
                quoted = true;
                continue;
            }
            if (ch == delimiter || ch is '\r' or '\n')
            {
                AddField();
                if (ch != delimiter)
                {
                    AddRow();
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                continue;
            }
            if (quoteClosed) throw new InvalidDataException("После закрывающей кавычки CSV ожидался разделитель.");
            field.Append(ch);
        }
        if (quoted) throw new InvalidDataException("Незакрытая кавычка в CSV.");
        if (field.Length > 0 || quoteClosed || row.Count > 0) { AddField(); AddRow(); }
        return rows;

        void AddField()
        {
            if (row.Count >= MaxColumns) throw new InvalidDataException("CSV содержит более 256 столбцов.");
            row.Add(field.ToString()); field.Clear(); quoteClosed = false;
        }
        void AddRow()
        {
            if (rows.Count >= MaxRows) throw new InvalidDataException("CSV содержит более 10000 строк.");
            rows.Add(row); row = [];
        }
    }

    internal static string Serialize(IEnumerable<IReadOnlyList<string>> rows, char delimiter)
    {
        var result = new StringBuilder();
        foreach (var row in rows)
        {
            if (row.Count > MaxColumns) throw new InvalidDataException("CSV содержит более 256 столбцов.");
            for (var i = 0; i < row.Count; i++)
            {
                if (i > 0) result.Append(delimiter);
                var value = row[i];
                if (value.IndexOfAny([delimiter, '"', '\r', '\n']) >= 0)
                    result.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
                else result.Append(value);
            }
            result.Append("\r\n");
            if (result.Length > MaxBytes) throw new InvalidDataException("CSV превысит 8 МиБ.");
        }
        if (Encoding.UTF8.GetByteCount(result.ToString()) > MaxBytes)
            throw new InvalidDataException("CSV превысит 8 МиБ.");
        return result.ToString();
    }

    internal static string Execute(string path, char delimiter, string action, int rowNumber,
        int columnNumber, string value, int filterColumn, string filterValue, string outputPath)
    {
        var rows = Read(path, delimiter);
        if (rowNumber < 1 || rowNumber > MaxRows || columnNumber < 1 || columnNumber > MaxColumns)
            throw new InvalidOperationException("Номер строки: 1–10000; столбца: 1–256.");
        switch (action)
        {
            case "Прочитать ячейку":
                return rowNumber <= rows.Count && columnNumber <= rows[rowNumber - 1].Count
                    ? rows[rowNumber - 1][columnNumber - 1] : "";
            case "Количество строк": return rows.Count.ToString(CultureInfo.InvariantCulture);
            case "Записать ячейку":
                while (rows.Count < rowNumber) rows.Add([]);
                while (rows[rowNumber - 1].Count < columnNumber) rows[rowNumber - 1].Add("");
                rows[rowNumber - 1][columnNumber - 1] = value;
                Write(path, rows, delimiter);
                return value;
            case "Отобрать строки":
                if (filterColumn < 1 || filterColumn > MaxColumns)
                    throw new InvalidOperationException("Столбец фильтра должен быть от 1 до 256.");
                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new InvalidOperationException("Укажите отдельный выходной CSV-файл.");
                if (Path.GetFullPath(path).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Выходной CSV не должен перезаписывать исходный файл.");
                var selected = rows.Where(row => row.Count >= filterColumn &&
                    row[filterColumn - 1].Contains(filterValue, StringComparison.OrdinalIgnoreCase)).ToList();
                Write(outputPath, selected, delimiter);
                return selected.Count.ToString(CultureInfo.InvariantCulture);
            default: throw new InvalidOperationException("Неизвестная операция с CSV.");
        }
    }

    private static void Write(string path, List<List<string>> rows, char delimiter)
    {
        var content = Serialize(rows, delimiter);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".kliker-writing-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
