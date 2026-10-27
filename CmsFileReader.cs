using System.Text;

namespace KlikerBlocks;

internal static class CmsFileReader
{
    private const int MaxFileBytes = 256 * 1024;

    public static string Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxFileBytes)
            throw new InvalidDataException("Файл CMS больше 256 КиБ.");

        string content;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
            content = new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2);
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
            content = new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2);
        else
        {
            var offset = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
            try
            {
                content = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException) when (offset == 0)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                content = Encoding.GetEncoding(1251).GetString(bytes);
            }
        }

        if (content.Contains('\0')) throw new InvalidDataException("Файл CMS содержит двоичные данные.");
        return content;
    }
}
