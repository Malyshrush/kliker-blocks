namespace KlikerBlocks;

internal static class BlockPathPicker
{
    internal enum Mode { Open, Save, Folder, FolderWithMask, ProjectFileName }

    internal static Mode? For(BlockNode node, string key)
    {
        var action = node.Value("Action");
        return (node.Kind, key) switch
        {
            ("DirectoryList", "Path") or ("FileQueue", "Folder") => Mode.Folder,
            ("DirectoryList", "Pattern") => Mode.FolderWithMask,
            ("LogFile", "Path") => Mode.ProjectFileName,
            ("Screenshot" or "BufferSave" or "Checkpoint", "Path") or ("CsvTable" or "EditFileLine" or "EditFileText", "OutputPath") or
                ("FileQueue", "StatePath") or ("WaitEvent", "Path") => Mode.Save,
            ("Note", "Path") when action == "Сохранить" => Mode.Save,
            ("TextFileArray", "Path") when action == "Записать массив" => Mode.Save,
            ("TextFile", "Path") when action is "Записать строку" or "Вставить строку" or "Добавить строку" or
                "Добавить или перезаписать строкой" or "Очистить" => Mode.Save,
            ("CsvTable" or "ExcelTable", "Path") when action == "Записать ячейку" => Mode.Save,
            ("IniFile", "Path") when action == "Записать" => Mode.Save,
            ("OcrImage", "EnginePath") or ("WaitScreenText", "Engine") or ("RunAhk", "Interpreter") or
                ("Note", "ImagePath") or ("FileQueue", "CurrentFile") => Mode.Open,
            ("CountFileChars" or "EditFileLine" or "EditFileText" or "TextFile" or "TextFileArray" or "CsvTable" or "ExcelTable" or "PdfText" or "IniFile" or
                "FindImage" or "BufferFindImage" or "BufferScanImage" or "OcrImage" or "ReadBarcode" or "Note" or
                "RunFile" or "RunAhk" or "RunScenario" or "PlaySound", "Path") => Mode.Open,
            _ => null
        };
    }

    internal static string? Pick(IWin32Window owner, BlockNode node, string key, string value, string projectFolder)
    {
        var mode = For(node, key);
        if (mode is null) return null;
        var ru = UiLanguage.Code == "ru";
        var directory = projectFolder;
        var name = "";
        if (value.Length > 0 && !VariableStore.HasReference(value))
        {
            try
            {
                var full = Path.GetFullPath(value, projectFolder);
                name = Path.GetFileName(full);
                if (Directory.Exists(full)) { directory = full; name = ""; }
                else if (Path.GetDirectoryName(full) is { } parent && Directory.Exists(parent)) directory = parent;
            }
            catch (ArgumentException) { /* Keep the project folder for invalid or incomplete paths. */ }
            catch (NotSupportedException) { }
        }
        if (mode is Mode.Folder or Mode.FolderWithMask)
        {
            using var picker = new FolderBrowserDialog { InitialDirectory = directory };
            if (picker.ShowDialog(owner) != DialogResult.OK) return null;
            if (mode == Mode.Folder) return picker.SelectedPath;
            var mask = name.Contains('*') || name.Contains('?') ? name : node.Value("Mask", "*");
            if (!(mask.Contains('*') || mask.Contains('?')) || Path.GetFileName(mask) != mask) mask = "*";
            return Path.Combine(picker.SelectedPath, mask);
        }

        using FileDialog dialog = mode is Mode.Save or Mode.ProjectFileName
            ? new SaveFileDialog { OverwritePrompt = false }
            : new OpenFileDialog { CheckFileExists = true };
        dialog.InitialDirectory = mode == Mode.ProjectFileName ? projectFolder : directory;
        dialog.FileName = name;
        dialog.Filter = Filter(node.Kind, key, ru);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        if (mode != Mode.ProjectFileName) return dialog.FileName;
        var selectedName = Path.GetFileName(dialog.FileName);
        if (string.Equals(Path.GetDirectoryName(dialog.FileName), projectFolder, StringComparison.OrdinalIgnoreCase) &&
            ScriptLogWriter.IsValidName(selectedName)) return selectedName;
        MessageBox.Show(owner, ru ? "Укажите имя файла в папке проекта." : "Choose a file name in the project folder.");
        return null;
    }

    private static string Filter(string kind, string key, bool ru)
    {
        var all = ru ? "Все файлы (*.*)|*.*" : "All files (*.*)|*.*";
        var filter = (kind, key) switch
        {
            ("OcrImage", "EnginePath") or ("WaitScreenText", "Engine") or ("RunAhk", "Interpreter") => "EXE (*.exe)|*.exe",
            ("RunAhk", "Path") => "AutoHotkey (*.ahk)|*.ahk",
            ("RunScenario", "Path") => "Kliker Blocks (*.klb)|*.klb",
            ("Note", "Path") => "Kliker note (*.klnote)|*.klnote",
            ("Note", "ImagePath") => "PNG, JPG, BMP, GIF|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            ("OcrImage", "Path") => "PNG, JPEG, BMP, TIFF, GIF, WebP, PDF|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif;*.webp;*.pdf",
            ("ReadBarcode", "Path") => "PNG, JPEG, BMP|*.png;*.jpg;*.jpeg;*.bmp",
            ("FindImage" or "BufferFindImage" or "BufferScanImage", "Path") => "PNG, BMP|*.png;*.bmp",
            ("Screenshot" or "BufferSave", "Path") => "PNG (*.png)|*.png",
            ("CsvTable", _) => "CSV (*.csv)|*.csv",
            ("ExcelTable", _) => "Excel (*.xlsx)|*.xlsx",
            ("PdfText", _) => "PDF (*.pdf)|*.pdf",
            ("PlaySound", _) => "WAV (*.wav)|*.wav",
            ("Checkpoint", _) or ("FileQueue", "StatePath") => "JSON (*.json)|*.json",
            ("IniFile", _) => "INI (*.ini)|*.ini",
            _ => ""
        };
        return filter.Length == 0 ? all : filter + "|" + all;
    }
}
