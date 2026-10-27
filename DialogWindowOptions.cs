using System.Drawing;
using System.Globalization;

namespace KlikerBlocks;

internal readonly record struct DialogWindowOptions(string Title, string X, string Y, string Width, string Height)
{
    public static bool IsOptionalNumericField(string kind, string key, string value) =>
        kind is "InputDialog" or "ChoiceDialog" or "RadioDialog" &&
        key is "WindowX" or "WindowY" or "WindowWidth" or "WindowHeight" &&
        string.IsNullOrWhiteSpace(value);

    public static DialogWindowOptions From(BlockNode node) => new(
        node.Value("WindowTitle"), node.Value("WindowX"), node.Value("WindowY"),
        node.Value("WindowWidth"), node.Value("WindowHeight"));

    public Point? Location
    {
        get
        {
            var hasX = !string.IsNullOrWhiteSpace(X);
            var hasY = !string.IsNullOrWhiteSpace(Y);
            if (!hasX && !hasY) return null;
            if (!hasX || !hasY || !int.TryParse(X, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(Y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                throw new InvalidOperationException(UiLanguage.T("Для позиции окна укажите целые X и Y вместе либо оставьте оба поля пустыми."));
            return new Point(x, y);
        }
    }

    public Size ClientSize(int defaultWidth, int defaultHeight, int minimumWidth, int minimumHeight)
    {
        var width = Dimension(Width, defaultWidth, minimumWidth);
        var height = Dimension(Height, defaultHeight, minimumHeight);
        return new Size(width, height);
    }

    private static int Dimension(string text, int fallback, int minimum)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > 3000)
            throw new InvalidOperationException(UiLanguage.Format(
                "Размер окна должен быть целым числом от {0} до 3000 или пустым полем.", minimum));
        return value;
    }
}
