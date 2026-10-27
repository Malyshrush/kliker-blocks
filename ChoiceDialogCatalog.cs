namespace KlikerBlocks;

// The button-set code and the clicked-button result are different namespaces.
// Keep the Windows DialogResult values in one place so the editor and runtime agree.
internal static class ChoiceDialogCatalog
{
    internal static readonly (int Code, string[] Buttons)[] Sets =
    [
        (0, ["ОК"]),
        (1, ["ОК", "Отмена"]),
        (2, ["Прервать", "Повторить", "Игнорировать"]),
        (3, ["Да", "Нет", "Отмена"]),
        (4, ["Да", "Нет"]),
        (5, ["Повторить", "Отмена"])
    ];

    internal static int ResultCode(string button) => button switch
    {
        "ОК" => (int)DialogResult.OK,
        "Отмена" => (int)DialogResult.Cancel,
        "Прервать" => (int)DialogResult.Abort,
        "Повторить" => (int)DialogResult.Retry,
        "Игнорировать" => (int)DialogResult.Ignore,
        "Да" => (int)DialogResult.Yes,
        "Нет" => (int)DialogResult.No,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    internal static string[] ButtonsFor(int code) =>
        Sets.FirstOrDefault(set => set.Code == code).Buttons ??
        throw new ArgumentOutOfRangeException(nameof(code), "DIALOGBOX: набор кнопок 0–5.");

    internal static string SetLabel(int code) => $"{code} — {string.Join(" / ", ButtonsFor(code).Select(UiLanguage.T))}";

    internal static string ResultGuide(int code) =>
        string.Join("; ", ButtonsFor(code).Select(button => $"{UiLanguage.T(button)} → {ResultCode(button)}"));

    internal static string IconLabel(int code) => code switch
    {
        0 => $"0 — {UiLanguage.T("Без иконки")}",
        1 => $"1 — {UiLanguage.T("Ошибка")}",
        2 => $"2 — {UiLanguage.T("Вопрос")}",
        3 => $"3 — {UiLanguage.T("Предупреждение")}",
        4 => $"4 — {UiLanguage.T("Информация")}",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}
