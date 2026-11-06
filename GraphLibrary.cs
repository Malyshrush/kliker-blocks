using System.IO.Compression;

namespace KlikerBlocks;

internal static class GraphLibrary
{
    internal sealed record Entry(string Folder, string Russian, string English);
    internal sealed record Section(string Russian, string English, Entry[] Entries);

    internal static readonly Section[] Sections =
    [
        new("Экран и снимки", "Screen and screenshots", [
            new("11-redact-screenshot", "11 · Снимок со скрытыми полями", "11 · Redact a screenshot"),
            new("12-count-badges", "12 · Посчитать значки на экране", "12 · Count icons on screen")]),
        new("Тренировка и памятки", "Practice and guides", [
            new("15-interval-timer", "15 · Голосовой интервальный таймер", "15 · Spoken interval timer"),
            new("16-flashcards", "16 · Карточки для тренировки", "16 · Flashcards"),
            new("17-step-note", "17 · Пошаговая памятка", "17 · Step-by-step guide")]),
        new("Наблюдение", "Monitoring", [
            new("20-window-watchdog", "20 · Дежурный для окна", "20 · Watch a window"),
            new("21-service-health", "21 · Проверка локального сервиса", "21 · Check a local service")])
    ];

    internal static string OpenWorkingCopy(Entry entry, string root)
    {
        var ru = UiLanguage.Code == "ru";
        var folder = Path.Combine(root, ru ? "ru" : "en", entry.Folder);
        var graph = Path.Combine(folder, "Scenario.klb");
        if (File.Exists(graph)) return graph;

        var resourceName = ru ? "KlikerBlocks.GraphLibrary.zip" : "KlikerBlocks.GraphLibrary.en.zip";
        using var resource = typeof(GraphLibrary).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(ru ? "Встроенная база графов отсутствует." : "The built-in graph library is missing.");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        var prefix = entry.Folder + "/";
        var files = archive.Entries.Where(file => file.FullName == "Проверка.md" ||
            file.FullName == "Instructions.md" || file.FullName.StartsWith(prefix, StringComparison.Ordinal));
        Directory.CreateDirectory(folder);
        foreach (var file in files.OrderBy(file => file.Name == "Scenario.klb"))
        {
            if (file.Name.Length == 0 || file.Name != Path.GetFileName(file.Name)) continue;
            var destination = Path.Combine(folder, file.Name);
            if (File.Exists(destination)) continue;
            var temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                file.ExtractToFile(temporary);
                try { File.Move(temporary, destination); }
                catch (IOException) when (File.Exists(destination)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (!File.Exists(graph)) throw new FileNotFoundException(ru ? "Выбранный граф отсутствует в базе." : "The selected graph is missing from the library.");
        return graph;
    }
}
