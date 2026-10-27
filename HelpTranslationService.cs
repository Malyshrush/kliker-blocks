using System.Collections.Concurrent;
using System.Text;

namespace KlikerBlocks;

internal static class HelpTranslationService
{
    private static readonly ConcurrentDictionary<(string Language, string Text), string> Cache = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<string> TranslateAsync(string russianText, string language,
        string pythonPath, CancellationToken cancel)
    {
        if (language == "ru") return russianText;
        var key = (language, russianText);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        await Gate.WaitAsync(cancel);
        try
        {
            if (Cache.TryGetValue(key, out cached)) return cached;
            var result = new StringBuilder(russianText.Length);
            foreach (var chunk in Split(russianText))
            {
                cancel.ThrowIfCancellationRequested();
                var start = 0;
                while (start < chunk.Length && char.IsWhiteSpace(chunk[start])) start++;
                var end = chunk.Length;
                while (end > start && char.IsWhiteSpace(chunk[end - 1])) end--;
                result.Append(chunk.AsSpan(0, start));
                if (end > start)
                    result.Append(await ArgosBridge.TranslateAsync(chunk[start..end], "ru", language,
                        pythonPath, 60000, cancel));
                result.Append(chunk.AsSpan(end));
            }
            return Cache[key] = result.ToString();
        }
        finally { Gate.Release(); }
    }

    internal static IReadOnlyList<string> Split(string text)
    {
        const int maxChunk = 4000;
        var chunks = new List<string>();
        for (var offset = 0; offset < text.Length;)
        {
            var length = Math.Min(maxChunk, text.Length - offset);
            if (offset + length < text.Length)
            {
                var breakAt = text.LastIndexOf('\n', offset + length - 1, length);
                if (breakAt < offset + length / 2)
                    breakAt = text.LastIndexOf(' ', offset + length - 1, length);
                if (breakAt >= offset + length / 2) length = breakAt - offset + 1;
            }
            chunks.Add(text.Substring(offset, length));
            offset += length;
        }
        return chunks;
    }
}
