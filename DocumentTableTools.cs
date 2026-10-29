using ClosedXML.Excel;
using UglyToad.PdfPig;
using System.Globalization;
using System.Text;

namespace KlikerBlocks;

internal static class SpreadsheetTools
{
    internal static string Execute(string path, string sheetName, string action, int row, int column,
        string value, int filterColumn, string filterValue)
    {
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Таблица Excel должна иметь расширение .xlsx.");
        if (row is < 1 or > 1048576 || column is < 1 or > 16384 || filterColumn is < 1 or > 16384)
            throw new InvalidOperationException("Строка Excel: 1–1048576; столбец: 1–16384.");
        if (!File.Exists(path) && action != "Записать ячейку")
            throw new FileNotFoundException("Файл Excel не найден.", path);
        if (File.Exists(path) && new FileInfo(path).Length > 32 * 1024 * 1024)
            throw new InvalidOperationException("Файл Excel больше 32 МиБ.");
        using var workbook = File.Exists(path) ? new XLWorkbook(path) : new XLWorkbook();
        IXLWorksheet worksheet;
        if (string.IsNullOrWhiteSpace(sheetName))
            worksheet = workbook.Worksheets.FirstOrDefault() ?? workbook.AddWorksheet("Sheet1");
        else
            worksheet = workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)) ??
                (action == "Записать ячейку" ? workbook.AddWorksheet(sheetName) :
                    throw new InvalidOperationException("Лист Excel не найден."));
        switch (action)
        {
            case "Прочитать ячейку":
                return worksheet.Cell(row, column).GetFormattedString();
            case "Количество строк":
                return (worksheet.LastRowUsed()?.RowNumber() ?? 0).ToString(CultureInfo.InvariantCulture);
            case "Записать ячейку":
            {
                worksheet.Cell(row, column).SetValue(value);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var temporary = path + ".kliker-writing-" + Guid.NewGuid().ToString("N") + ".xlsx";
                try { workbook.SaveAs(temporary); File.Move(temporary, path, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return value;
            }
            case "Найти строки":
            {
                var last = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, 10000);
                var matches = new List<string>();
                for (var n = 1; n <= last; n++)
                    if (worksheet.Cell(n, filterColumn).GetFormattedString()
                        .Contains(filterValue, StringComparison.OrdinalIgnoreCase))
                        matches.Add(n.ToString(CultureInfo.InvariantCulture));
                return string.Join(",", matches);
            }
            default: throw new InvalidOperationException("Неизвестная операция Excel.");
        }
    }
}

internal static class PdfTextTools
{
    internal static string Execute(string path, string action, int pageNumber)
    {
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PDF-файл должен иметь расширение .pdf.");
        if (!File.Exists(path)) throw new FileNotFoundException("PDF-файл не найден.", path);
        if (new FileInfo(path).Length > 32 * 1024 * 1024)
            throw new InvalidOperationException("PDF-файл больше 32 МиБ.");
        using var document = PdfDocument.Open(path);
        if (action == "Количество страниц") return document.NumberOfPages.ToString(CultureInfo.InvariantCulture);
        if (action == "Текст страницы")
        {
            if (pageNumber < 1 || pageNumber > document.NumberOfPages)
                throw new InvalidOperationException("Номер страницы PDF вне диапазона.");
            return Limit(document.GetPage(pageNumber).Text);
        }
        if (action != "Весь текст") throw new InvalidOperationException("Неизвестная операция PDF.");
        if (document.NumberOfPages > 100)
            throw new InvalidOperationException("Для извлечения всего текста допускается не более 100 страниц.");
        var result = new StringBuilder();
        for (var page = 1; page <= document.NumberOfPages; page++)
        {
            if (page > 1) result.AppendLine().AppendLine();
            result.Append(document.GetPage(page).Text);
            if (result.Length > 100000) throw new InvalidOperationException("Извлечённый текст PDF длиннее 100000 символов.");
        }
        return result.ToString();
    }

    private static string Limit(string text) => text.Length <= 100000 ? text :
        throw new InvalidOperationException("Извлечённый текст PDF длиннее 100000 символов.");
}
