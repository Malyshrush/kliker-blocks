namespace KlikerBlocks;

internal static class BlockTrace
{
    private static string Prefix(BlockNode node) =>
        $"[{Word("Блок", "Block", "Bloc", "Bloque", "积木", "Block")} {node.Id.ToString("N")[..8]}] {UiLanguage.T(BlockCatalog.Get(node.Kind).Title)}";

    private static string Word(string ru, string en, string fr, string es, string zh, string de) =>
        UiLanguage.Code switch { "en" => en, "fr" => fr, "es" => es, "zh" => zh, "de" => de, _ => ru };

    internal static string Details(BlockNode original, BlockNode resolved,
        IReadOnlyList<VariableStore.VariableChange> changes)
    {
        var labels = BlockCatalog.Get(original.Kind).Fields.ToDictionary(field => field.Key,
            field => UiLanguage.T(field.Label), StringComparer.Ordinal);
        var parameters = string.Join(", ", resolved.Values.Select(entry =>
            (labels.TryGetValue(entry.Key, out var label) ? label : entry.Key) + "=" + Quote(entry.Value)));
        var changesText = changes.Count == 0
            ? Word("нет", "none", "aucune", "ninguno", "无", "keine")
            : string.Join(", ", changes.Take(20).Select(change => change.Name + "=" +
                (change.Value is null ? Word("<удалено>", "<removed>", "<supprimé>", "<eliminado>", "<已删除>", "<entfernt>") : Quote(change.Value))))
                + (changes.Count > 20 ? $" (+{changes.Count - 20})" : "");
        return Prefix(original) + " — " + Word("параметры", "parameters", "paramètres", "parámetros", "参数", "Parameter")
            + ": " + parameters + "; " + Word("изменения", "changes", "modifications", "cambios", "变更", "Änderungen")
            + ": " + changesText + ".";
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        return "\"" + (escaped.Length > 180 ? escaped[..180] + "…" : escaped) + "\"";
    }

    public static string Completed(BlockNode node, Guid? next, string? chosenOutput = null)
    {
        var output = chosenOutput ?? node.Links.FirstOrDefault(link => link.Value == next && next is not null).Key;
        return Prefix(node) + (output is null
            ? Word(" — выполнен; ветка завершена.", " — completed; branch ended.", " — terminé ; branche terminée.", " — completado; rama terminada.", " — 已完成；分支结束。", " — abgeschlossen; Zweig beendet.")
            : " — " + Word("выполнен; выход", "completed; output", "terminé ; sortie", "completado; salida", "已完成；出口", "abgeschlossen; Ausgang")
                + $" «{UiLanguage.T(output)}»" + (next is null
                    ? Word("; ветка завершена.", "; branch ended.", "; branche terminée.", "; rama terminada.", "；分支结束。", "; Zweig beendet.") : "."));
    }

    public static string Interrupted(BlockNode node) => Prefix(node) + Word(" — прерван остановкой.",
        " — interrupted by stop.", " — interrompu par l'arrêt.", " — interrumpido al detener.", " — 已被停止中断。", " — durch Stopp unterbrochen.");
    public static string Failed(BlockNode node, Exception error) =>
        Prefix(node) + " — " + Word("ошибка", "error", "erreur", "error", "错误", "Fehler") + ": " + error.Message;
}
