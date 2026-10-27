namespace KlikerBlocks;

// Graph code stores newlines as JSON \n escapes; Windows text boxes display CRLF.
internal static class MultilineFieldText
{
    public static bool IsMultiline(string kind, string key) =>
        kind == "RadioDialog" && key == "Options" || kind == "HttpGet" && key == "Headers";

    public static string ForStorage(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static string ForDisplay(string text) => ForStorage(text).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
