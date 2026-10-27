namespace KlikerBlocks;

internal static class ScriptNameTools
{
    public static bool IsValid(string value) =>
        value.Length is >= 1 and <= 100 && !string.IsNullOrWhiteSpace(value) &&
        !value.Any(char.IsControl);
}
