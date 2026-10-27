namespace KlikerBlocks;

internal static class IndicatorConditionMode
{
    public const string Normal = "Да: включено, Нет: выключено";
    public const string Inverted = "Да: выключено, Нет: включено";
    public const string Ignore = "Не влияет";

    public static bool Supports(string kind) => kind is
        "IfKeyDown" or "IfVariable" or "IfExpression" or "IfMoved" or "IfPixel" or "IfWindow";

    public static string DefaultFor(string kind) => kind == "IfKeyDown" ? Normal : Ignore;

    public static bool? Resolve(string mode, bool yes) => mode switch
    {
        Normal => yes,
        Inverted => !yes,
        Ignore => null,
        _ => throw new InvalidOperationException($"Неизвестный режим индикатора: {mode}")
    };
}
