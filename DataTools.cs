using System.Text;

namespace KlikerBlocks;

internal static class DataTools
{
    private const long MaxTextBytes = 8 * 1024 * 1024;
    private static readonly HttpClient Client = new();

    private static string[] ReadLines(string path)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > MaxTextBytes)
            throw new InvalidOperationException("Текстовый файл больше 8 МиБ. Операция отменена.");
        return File.ReadAllLines(path, Encoding.UTF8);
    }

    public static string[] ReadTextArray(string path)
    {
        var lines = ReadLines(path);
        if (lines.Length > 10000) throw new InvalidOperationException("TFREADARR: более 10000 строк в файле.");
        return lines;
    }

    public static void WriteTextArray(string path, IReadOnlyList<string> lines)
    {
        if (lines.Count > 10000) throw new InvalidOperationException("TFWRITEARR: более 10000 строк в массиве.");
        var newlineBytes = Encoding.UTF8.GetByteCount(Environment.NewLine);
        long bytes = 0;
        foreach (var line in lines)
        {
            if (line.Contains('\r') || line.Contains('\n'))
                throw new InvalidOperationException("TFWRITEARR: элемент массива содержит перевод строки.");
            bytes += Encoding.UTF8.GetByteCount(line) + newlineBytes;
            if (bytes > MaxTextBytes) throw new InvalidOperationException("TFWRITEARR: файл превысит 8 МиБ.");
        }
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    public static string TextFile(string path, string action, int line, string value)
    {
        if (action == "Удалить файл")
        {
            File.Delete(path);
            return "0";
        }
        if (line < 1) throw new InvalidOperationException("Номер строки должен начинаться с 1.");
        var lines = ReadLines(path).ToList();
        switch (action)
        {
            case "Прочитать строку": return line <= lines.Count ? lines[line - 1] : "";
            case "Количество строк": return lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case "Записать строку":
                while (lines.Count < line) lines.Add("");
                lines[line - 1] = value;
                break;
            case "Вставить строку": lines.Insert(Math.Min(line - 1, lines.Count), value); break;
            case "Добавить строку": lines.Add(value); break;
            case "Перезаписать строкой": lines.Clear(); lines.Add(value); break;
            case "Удалить строку": if (line <= lines.Count) lines.RemoveAt(line - 1); break;
            case "Очистить": lines.Clear(); break;
            default: throw new InvalidOperationException("Неизвестная файловая операция.");
        }
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string Ini(string path, string section, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(key) ||
            section.Contains(']') || key.Contains('=') || key.Contains('\n'))
            throw new InvalidOperationException("Неверное имя секции или ключа INI.");
        var lines = ReadLines(path).ToList();
        var sectionStart = -1;
        var sectionEnd = lines.Count;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']')) continue;
            if (sectionStart >= 0) { sectionEnd = i; break; }
            if (string.Equals(trimmed[1..^1], section, StringComparison.OrdinalIgnoreCase)) sectionStart = i;
        }
        if (sectionStart < 0)
        {
            if (value is null) return "";
            lines.Add($"[{section}]");
            lines.Add($"{key}={value}");
        }
        else
        {
            for (var i = sectionStart + 1; i < sectionEnd; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
                var equal = lines[i].IndexOf('=');
                if (equal < 0 || !string.Equals(lines[i][..equal].Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;
                if (value is null) return lines[i][(equal + 1)..].Trim();
                lines[i] = $"{key}={value}";
                Write(path, lines);
                return value;
            }
            if (value is null) return "";
            lines.Insert(sectionEnd, $"{key}={value}");
        }
        Write(path, lines);
        return value ?? "";
    }

    private static void Write(string path, List<string> lines)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    public static string[] ListFiles(string directory, string mask, bool folders)
    {
        if (!Directory.Exists(directory)) return [];
        return (folders ? Directory.EnumerateDirectories(directory, mask, SearchOption.TopDirectoryOnly)
            : Directory.EnumerateFiles(directory, mask, SearchOption.TopDirectoryOnly))
            .Take(10001).Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()!;
    }

    public static IReadOnlyList<KeyValuePair<string, string>> ParseHttpHeaders(string text)
    {
        if (text.Length > 8192) throw new InvalidOperationException("HTTP-заголовки длиннее 8192 символов.");
        var result = new List<KeyValuePair<string, string>>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) throw new InvalidOperationException("HTTP-заголовок должен иметь вид «Имя: значение».");
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Length == 0 || name.Any(ch => !char.IsAsciiLetterOrDigit(ch) &&
                !"!#$%&'*+-.^_`|~".Contains(ch)) || value.Any(char.IsControl))
                throw new InvalidOperationException("Недопустимые символы в HTTP-заголовке.");
            result.Add(new KeyValuePair<string, string>(name, value));
            if (result.Count > 32) throw new InvalidOperationException("Не более 32 HTTP-заголовков.");
        }
        return result;
    }

    public static HttpRequestMessage CreateHttpRequest(string url, string headers)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("HTTP-адрес должен начинаться с http:// или https://.");
        var parsedHeaders = ParseHttpHeaders(headers);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        try
        {
            foreach (var header in parsedHeaders)
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    throw new InvalidOperationException($"HTTP-заголовок «{header.Key}» нельзя добавить к GET-запросу.");
        }
        catch { request.Dispose(); throw; }
        return request;
    }

    public static async Task<string> HttpGetAsync(string url, int timeoutMs, string headers, CancellationToken cancel)
    {
        using var request = CreateHttpRequest(url, headers);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(Math.Clamp(timeoutMs, 100, 60000));
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxTextBytes)
            throw new InvalidOperationException("HTTP-ответ больше 8 МиБ.");
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, deadline.Token);
            if (count == 0) break;
            if (memory.Length + count > MaxTextBytes) throw new InvalidOperationException("HTTP-ответ больше 8 МиБ.");
            memory.Write(buffer, 0, count);
        }
        return Encoding.UTF8.GetString(memory.ToArray());
    }
}
