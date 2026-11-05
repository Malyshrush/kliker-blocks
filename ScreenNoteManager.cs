namespace KlikerBlocks;

internal sealed record NoteRequest(string Action, string Id, string Path, string TextMode, string Text,
    string ImageMode, string ImagePath, int? X, int? Y, int? Width, int? Height);

internal static class ScreenNoteManager
{
    private static readonly Dictionary<string, ScreenNoteForm> OpenNotes = new(StringComparer.OrdinalIgnoreCase);
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;

    internal static bool IsFileOpen(string path, ScreenNoteForm? except = null)
    {
        var fullPath = Path.GetFullPath(path);
        return OpenNotes.Values.Any(note => note != except && !note.IsDisposed && note.SavedPath is not null &&
            string.Equals(Path.GetFullPath(note.SavedPath), fullPath, StringComparison.OrdinalIgnoreCase));
    }

    internal static string Show(ScreenNoteForm form, string id)
    {
        id = string.IsNullOrWhiteSpace(id) ? "note_" + Guid.NewGuid().ToString("N")[..8] : id.Trim();
        if (OpenNotes.ContainsKey(id)) { form.Dispose(); throw new InvalidOperationException(L("Заметка с таким идентификатором уже открыта.", "A note with that ID is already open.")); }
        if (form.SavedPath is not null && IsFileOpen(form.SavedPath)) { form.Dispose(); throw new InvalidOperationException(L("Этот файл заметки уже открыт.", "This note file is already open.")); }
        form.NoteId = id; form.RefreshTitle(); OpenNotes.Add(id, form);
        form.FormClosed += (_, _) => OpenNotes.Remove(id);
        try { form.Show(); }
        catch { OpenNotes.Remove(id); form.Dispose(); throw; }
        return id;
    }

    internal static string Execute(NoteRequest request)
    {
        var id = request.Id.Trim();
        if (request.TextMode is not ("Не менять" or "Заменить" or "Добавить") ||
            request.ImageMode is not ("Не менять" or "Из файла" or "Убрать"))
            throw new ArgumentException(L("Неизвестное действие с содержимым заметки.", "Unknown note content action."));
        if (request.Width is < 270 or > 3000 || request.Height is < 200 or > 3000)
            throw new ArgumentOutOfRangeException(nameof(request), L("Размер заметки: ширина 270–3000, высота 200–3000.", "Note size: width 270–3000, height 200–3000."));
        if (request.Action is "Изменить файл" or "Прочитать файл")
        {
            if (string.IsNullOrWhiteSpace(request.Path)) throw new ArgumentException(L("Укажите файл заметки.", "Specify a note file."));
            if (request.Action == "Изменить файл" && IsFileOpen(request.Path)) throw new InvalidOperationException(L("Файл уже открыт как заметка. Измените открытую заметку по её идентификатору.", "The file is already open as a note. Change the open note by its ID."));
            using var saved = ScreenNoteForm.LoadFrom(request.Path);
            if (request.Action == "Прочитать файл") return saved.GetNoteText();
            saved.SetContents(request.TextMode, request.Text, request.ImageMode, request.ImagePath);
            saved.SaveTo(request.Path);
            return request.Path;
        }
        if (request.Action is "Создать" or "Открыть")
        {
            var note = request.Action == "Открыть" ? ScreenNoteForm.LoadFrom(request.Path) : new ScreenNoteForm();
            try
            {
                note.SetContents(request.TextMode, request.Text, request.ImageMode, request.ImagePath);
                note.SetPlacement(request.X, request.Y, request.Width, request.Height);
                return Show(note, id);
            }
            catch { note.Dispose(); throw; }
        }
        if (!OpenNotes.TryGetValue(id, out var form) || form.IsDisposed)
            throw new InvalidOperationException(L("Открытая заметка с таким идентификатором не найдена: ", "No open note has this ID: ") + id);
        switch (request.Action)
        {
            case "Прочитать": return form.GetNoteText();
            case "Изменить":
                form.SetContents(request.TextMode, request.Text, request.ImageMode, request.ImagePath);
                form.SetPlacement(request.X, request.Y, request.Width, request.Height);
                break;
            case "Сохранить":
                var path = string.IsNullOrWhiteSpace(request.Path) ? form.SavedPath : request.Path;
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(L("Укажите путь сохранения заметки.", "Specify a note save path."));
                form.SaveTo(path);
                break;
            case "Свернуть": form.WindowState = FormWindowState.Minimized; break;
            case "Развернуть": form.WindowState = FormWindowState.Normal; form.Show(); form.Activate(); break;
            case "Закрыть": form.Close(); break;
            default: throw new ArgumentException(L("Неизвестное действие заметки.", "Unknown note action."));
        }
        return id;
    }
}
