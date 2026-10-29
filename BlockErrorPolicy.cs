namespace KlikerBlocks;

internal static class BlockErrorPolicy
{
    internal const string Stop = "Остановить";
    internal const string Skip = "Пропустить";
    internal const string Jump = "Перейти к блоку";

    internal static (bool Retry, Guid? Next, bool Throw) Decide(BlockNode node, int attempt)
    {
        var retries = node.Int("__ErrorRetries", 0);
        var delay = node.Int("__ErrorDelayMs", 0);
        if (retries is < 0 or > 10 || delay is < 0 or > 60000)
            throw new InvalidOperationException("Повторы: 0–10; задержка: 0–60000 мс.");
        if (attempt < retries) return (true, null, false);
        return node.Value("__ErrorPolicy", Stop) switch
        {
            Stop => (false, null, true),
            Skip => (false, node.Link("Далее"), false),
            Jump when Guid.TryParse(node.Value("__ErrorTarget"), out var target) =>
                (false, target, false),
            Jump => throw new InvalidOperationException("Для перехода после ошибки выберите целевой блок."),
            _ => throw new InvalidOperationException("Неизвестный режим ошибки блока.")
        };
    }
}
