using System.Net.Http.Json;
using System.Text.Json;

namespace KlikerBlocks;

internal static class TranslationTools
{
    private static readonly HttpClient Client = new();
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase)
        { "ru", "en", "fr", "zh", "es", "de" };

    public static bool IsLanguage(string code) => Languages.Contains(code.Trim());
    internal sealed record LanguageInfo(string Code, string Name, IReadOnlyList<string> Targets);
    internal sealed record LanguageDetection(string Code, double Confidence);

    public static async Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync(string endpoint,
        int timeoutMs, CancellationToken cancel, HttpClient? client = null)
    {
        using var document = await RequestJsonAsync(endpoint, "languages", null, timeoutMs, cancel, client);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Сервис перевода вернул неверный список языков.");
        var result = new List<LanguageInfo>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String) continue;
            var targets = item.TryGetProperty("targets", out var values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? "").ToArray() : [];
            result.Add(new LanguageInfo(code.GetString() ?? "", item.TryGetProperty("name", out var name)
                ? name.GetString() ?? "" : "", targets));
        }
        return result;
    }

    public static async Task<LanguageDetection> DetectAsync(string text, string endpoint,
        int timeoutMs, CancellationToken cancel, HttpClient? client = null)
    {
        if (text.Length > 16000) throw new InvalidOperationException("Определение языка: текст длиннее 16000 символов.");
        using var document = await RequestJsonAsync(endpoint, "detect", new { q = text }, timeoutMs, cancel, client);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException("Сервис не смог определить язык текста.");
        var best = document.RootElement[0];
        if (!best.TryGetProperty("language", out var language) || language.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Сервис вернул неверный код языка.");
        var confidence = best.TryGetProperty("confidence", out var value) && value.TryGetDouble(out var number)
            ? number : 0;
        return new LanguageDetection(language.GetString() ?? "", confidence);
    }

    private static async Task<JsonDocument> RequestJsonAsync(string endpoint, string operation, object? data,
        int timeoutMs, CancellationToken cancel, HttpClient? client)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https") ||
            baseUri.UserInfo.Length > 0)
            throw new InvalidOperationException("Укажите адрес HTTP/HTTPS сервиса без пароля в URL.");
        if (timeoutMs is < 100 or > 60000) throw new InvalidOperationException("Таймаут должен быть 100–60000 мс.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(timeoutMs);
        using var request = new HttpRequestMessage(data is null ? HttpMethod.Get : HttpMethod.Post,
            new Uri(baseUri, operation));
        if (data is not null) request.Content = JsonContent.Create(data);
        using var response = await (client ?? Client).SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 256 * 1024)
            throw new InvalidOperationException("Ответ сервиса перевода слишком большой.");
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, deadline.Token);
            if (read == 0) break;
            if (buffer.Length + read > 256 * 1024)
                throw new InvalidOperationException("Ответ сервиса перевода слишком большой.");
            buffer.Write(chunk, 0, read);
        }
        try { return JsonDocument.Parse(buffer.ToArray()); }
        catch (JsonException error) { throw new InvalidOperationException("Сервис перевода вернул неверный JSON.", error); }
    }

    public static async Task<string> TranslateAsync(string text, string source, string target,
        string endpoint, int timeoutMs, CancellationToken cancel, HttpClient? client = null)
    {
        source = source.Trim().ToLowerInvariant();
        target = target.Trim().ToLowerInvariant();
        if (source != "auto" && !Languages.Contains(source) || !Languages.Contains(target))
            throw new InvalidOperationException("Перевод: языки — auto/ru/en/fr/zh/es для исходного и ru/en/fr/zh/es для целевого.");
        if (source == target) return text;
        if (text.Length > 16000) throw new InvalidOperationException("Перевод: текст длиннее 16000 символов.");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length > 0)
            throw new InvalidOperationException("Перевод: укажите адрес HTTP/HTTPS сервиса без пароля в URL.");
        if (timeoutMs is < 100 or > 60000) throw new InvalidOperationException("Перевод: таймаут должен быть 100–60000 мс.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(timeoutMs);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new { q = text, source, target, format = "text" })
        };
        using var response = await (client ?? Client).SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 256 * 1024)
            throw new InvalidOperationException("Перевод: ответ сервиса слишком большой.");
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, deadline.Token);
            if (read == 0) break;
            if (memory.Length + read > 256 * 1024)
                throw new InvalidOperationException("Перевод: ответ сервиса слишком большой.");
            memory.Write(buffer, 0, read);
        }
        try
        {
            using var document = JsonDocument.Parse(memory.ToArray());
            if (document.RootElement.TryGetProperty("translatedText", out var translated) &&
                translated.ValueKind == JsonValueKind.String)
                return translated.GetString() ?? "";
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("Перевод: сервис вернул неверный JSON.", error);
        }
        throw new InvalidOperationException("Перевод: в ответе сервиса нет translatedText. Проверьте адрес и языковые пакеты.");
    }
}
