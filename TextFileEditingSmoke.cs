using System.Text;

namespace KlikerBlocks;

internal static class TextFileEditingSmoke
{
    internal static void Run()
    {
        static void Check(bool ok, string description)
        {
            if (!ok) throw new InvalidOperationException("Text file test failed: " + description);
        }
        var count = BlockCatalog.Create("CountTextChars", 0, 0);
        count.Values["Mode"] = "Только из списка";
        count.Values["Include"] = "1\n2";
        Check(TextFileEditing.Count("111 22 A", count) == 5, "selected characters");
        count.Values["Exclude"] = "2";
        Check(TextFileEditing.Count("111 22 A", count) == 3, "exclusions override inclusions");
        count.Values["Include"] = "😀\n,";
        count.Values["Exclude"] = "";
        Check(TextFileEditing.Count("😀,😀", count) == 3, "Unicode and punctuation lists");
        count.Values["Include"] = "a";
        count.Values["Case"] = "Нет";
        Check(TextFileEditing.Count("Aa", count) == 2, "case insensitive");
        count.Values["Mode"] = "Все символы";
        count.Values["Spaces"] = "Нет";
        count.Values["Tabs"] = "Нет";
        count.Values["Newlines"] = "Нет";
        Check(TextFileEditing.Count("a \t\r\nb", count) == 2, "whitespace filters");

        var folder = Path.Combine(Path.GetTempPath(), "kliker-text-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "источник.txt");
        var output = Path.Combine(folder, "результат.txt");
        try
        {
            var original = "one  two\r\n\r\n   \r\n-----\r\n111\r\n-\r\nlast";
            File.WriteAllText(source, original, new UTF8Encoding(true));
            var edit = BlockCatalog.Create("EditFileText", 0, 0);
            edit.Values["Action"] = "Удалить строки по условию";
            edit.Values["Empty"] = "Да";
            edit.Values["Whitespace"] = "Да";
            edit.Values["Repeated"] = "Да";
            edit.Values["RepeatSymbol"] = "-";
            edit.Values["MinRepeat"] = "2";
            TextFileEditing.Edit(source, output, edit);
            Check(File.ReadAllText(output) == "one  two\r\n111\r\n-\r\nlast", "combined line deletion and minimum");
            Check(File.ReadAllText(source) == original, "separate output keeps original");
            Check(File.ReadAllBytes(output).AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }), "UTF-8 BOM retained");

            var fileCount = BlockCatalog.Create("CountFileChars", 0, 0);
            fileCount.Values["Scope"] = "Диапазон строк";
            fileCount.Values["FirstLine"] = "1";
            fileCount.Values["LastLine"] = "2";
            fileCount.Values["Array"] = "counts";
            fileCount.Values["Mode"] = "Только из списка";
            fileCount.Values["Include"] = "1\n2";
            var (total, perLine) = TextFileEditing.CountFile(output, fileCount);
            Check(total == 3 && perLine.SequenceEqual(["0", "3"]), "range total and per-line array");

            var line = BlockCatalog.Create("EditFileLine", 0, 0);
            line.Values["FirstLine"] = "1";
            TextFileEditing.Edit(output, "", line);
            Check(File.ReadAllText(output) == "one two\r\n111\r\n-\r\nlast", "collapse spaces in one line");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cp = Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            File.WriteAllText(source, "Привет   мир\n", cp);
            line.Values["Encoding"] = "Windows-1251";
            TextFileEditing.Edit(source, output, line);
            Check(File.ReadAllText(output, cp) == "Привет мир\n", "Windows-1251 and LF preservation");
            File.WriteAllText(source, "Привет   мир\r\n", new UnicodeEncoding(false, true, true));
            line.Values["Encoding"] = "Авто";
            TextFileEditing.Edit(source, output, line);
            Check(File.ReadAllBytes(output).AsSpan().StartsWith(new byte[] { 0xff, 0xfe }) &&
                  File.ReadAllText(output, Encoding.Unicode) == "Привет мир\r\n", "UTF-16 BOM preserved");

            edit.Values["Action"] = "Заменить текст";
            edit.Values["Processing"] = "Как единый текст";
            edit.Values["Find"] = "мир\r\n";
            edit.Values["Replace"] = "world";
            edit.Values["Encoding"] = "Авто";
            TextFileEditing.Edit(output, "", edit);
            Check(File.ReadAllText(output, Encoding.Unicode) == "Привет world", "whole text replacement across line ending");

            var start = BlockCatalog.Create("Start", 0, 0);
            count = BlockCatalog.Create("CountTextChars", 100, 0);
            count.Values["Text"] = "111 22";
            count.Values["Mode"] = "Только из списка";
            count.Values["Include"] = "1\n2";
            var logBlock = BlockCatalog.Create("Log", 200, 0);
            logBlock.Values["Text"] = "${character_count}";
            start.Links["Далее"] = count.Id;
            count.Links["Далее"] = logBlock.Id;
            var messages = new List<string>();
            new Runner(new ProjectModel { Nodes = [start, count, logBlock], VariableNamesAsciiOnly = true },
                messages.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(messages.Contains("5"), "graph executes count block");

            File.WriteAllText(source, "111  22\r\n-----\r\nkept", new UTF8Encoding(false));
            var countFile = BlockCatalog.Create("CountFileChars", 100, 0);
            countFile.Values["Path"] = source;
            countFile.Values["Mode"] = "Только из списка";
            countFile.Values["Include"] = "1\n2";
            countFile.Values["Array"] = "line_counts";
            var editLine = BlockCatalog.Create("EditFileLine", 200, 0);
            editLine.Values["Path"] = source;
            editLine.Values["OutputPath"] = output;
            var editFile = BlockCatalog.Create("EditFileText", 300, 0);
            editFile.Values["Path"] = output;
            editFile.Values["Action"] = "Удалить строки по условию";
            editFile.Values["Repeated"] = "Да";
            editFile.Values["RepeatSymbol"] = "-";
            start = BlockCatalog.Create("Start", 0, 0);
            start.Links["Далее"] = countFile.Id;
            countFile.Links["Далее"] = editLine.Id;
            editLine.Links["Далее"] = editFile.Id;
            messages.Clear();
            new Runner(new ProjectModel { Nodes = [start, countFile, editLine, editFile], VariableNamesAsciiOnly = true },
                messages.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(File.ReadAllText(output) == "111 22\r\nkept" && File.ReadAllText(source) == "111  22\r\n-----\r\nkept",
                "all three file blocks run in a graph and preserve input when writing a copy");

        }
        finally
        {
            if (File.Exists(source)) File.Delete(source);
            if (File.Exists(output)) File.Delete(output);
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
        Console.WriteLine("Text counting and file editing tests passed.");
    }
}
