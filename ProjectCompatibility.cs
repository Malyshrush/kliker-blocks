using System.Text.Json;

namespace KlikerBlocks;

internal static class ProjectCompatibility
{
    internal const int CurrentSchemaVersion = 1;

    internal static ProjectModel Read(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(UiLanguage.T("Файл графа должен содержать объект JSON."));
        var version = root.TryGetProperty("Version", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32() : 1;
        if (version > CurrentSchemaVersion)
            throw new InvalidDataException(UiLanguage.Format(
                "Граф создан в более новой версии формата ({0}). Обновите Kliker Blocks.", version));
        if (version < 1)
            throw new InvalidDataException(UiLanguage.T("Этот формат графа пока не поддерживается. Исходный файл не изменён."));
        var project = JsonSerializer.Deserialize<ProjectModel>(json)
            ?? throw new InvalidDataException(UiLanguage.T("Файл графа пуст или повреждён."));
        if (project.Nodes is null || project.Nodes.Select(node => node.Id).Distinct().Count() != project.Nodes.Count ||
            project.Nodes.Any(node => !BlockCatalog.All.Any(spec => spec.Kind == node.Kind)))
            throw new InvalidDataException(UiLanguage.T("Граф содержит неизвестные блоки или повторяющиеся идентификаторы. Исходный файл не изменён."));
        project.VariableNamesAsciiOnly = true;
        return project;
    }

    internal static string UserProjectDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kliker Blocks");
}
