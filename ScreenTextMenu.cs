using System.Runtime.InteropServices;
using System.Text;

namespace KlikerBlocks;

internal sealed class ScreenMenuGraph
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string NameRu { get; set; } = "";
    public string NameEn { get; set; } = "";
    public override string ToString() => (UiLanguage.Code == "ru" && NameRu.Length > 0 ? NameRu : UiLanguage.Code == "en" && NameEn.Length > 0 ? NameEn : Name) + " — " + Path;
}

internal static class ScreenTextMenu
{
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;

    // Copy before showing any window, so Ctrl+C is delivered to the selected application's focus.
    internal static async Task ShowAsync(AppSettings settings,
        Func<string, string, string, CancellationToken, Task> runGraph,
        Func<string, CancellationToken, Task<string?>> ensureOcr)
    {
        var target = GetForegroundWindow();
        for (var i = 0; i < 40 && (NativeMouse.IsKeyDown(0x11) || NativeMouse.IsKeyDown(0x12) || NativeMouse.IsKeyDown(0x10) || NativeMouse.IsKeyDown(0x5b) || NativeMouse.IsKeyDown(0x5c)); i++)
            await Task.Delay(50);
        if (NativeMouse.IsKeyDown(0x11) || NativeMouse.IsKeyDown(0x12) || NativeMouse.IsKeyDown(0x10) || NativeMouse.IsKeyDown(0x5b) || NativeMouse.IsKeyDown(0x5c) || GetForegroundWindow() != target)
        {
            MessageBox.Show(L("Не удалось скопировать выделение: отпустите клавиши и повторите.", "Could not copy the selection. Release the keys and try again."));
            return;
        }
        var before = GetClipboardSequenceNumber();
        try
        {
            NativeMouse.KeyDown(0x11);
            try { NativeMouse.KeyDown(0x43); NativeMouse.KeyUp(0x43); }
            finally { NativeMouse.KeyUp(0x11); }
            for (var i = 0; i < 30 && GetClipboardSequenceNumber() == before; i++) await Task.Delay(50);
            string text = "";
            for (var i = 0; GetClipboardSequenceNumber() != before && i < 10; i++)
            {
                try { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; break; }
                catch (ExternalException) when (i < 9) { await Task.Delay(50); }
            }
            using var menu = new ScreenTextMenuForm(text, settings.ScreenMenuFolder,
                settings.ScreenMenuGraphs ?? [], runGraph, ensureOcr);
            menu.ShowDialog();
        }
        catch (Exception error) { MessageBox.Show(error.Message, L("Экранное меню", "Screen menu"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    internal static string SaveText(string folder, string text)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
            throw new InvalidOperationException(L("Укажите абсолютный путь к папке в настройках экранного меню.", "Set an absolute folder path in screen menu settings."));
        Directory.CreateDirectory(folder);
        var prefix = L("Текст", "Text") + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        for (var i = 0; i < 1000; i++)
        {
            var path = Path.Combine(folder, prefix + (i == 0 ? "" : "_" + i) + ".txt");
            try
            {
                using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(file, new UTF8Encoding(true));
                writer.Write(text);
                return path;
            }
            catch (IOException) when (File.Exists(path)) { }
        }
        throw new IOException(L("Не удалось подобрать свободное имя файла.", "Could not choose an unused file name."));
    }
}

internal sealed class ScreenTextMenuForm : Form
{
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;
    private string _text;
    private readonly string _folder;
    private string _imagePath = "";
    private readonly ComboBox _voice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly TextBox _ocrLanguages = new() { Text = "eng+rus", Width = 140 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(460, 0) };
    private CancellationTokenSource? _speech;
    private CancellationTokenSource? _operation;

    internal ScreenTextMenuForm(string text, string folder, IReadOnlyList<ScreenMenuGraph> graphs,
        Func<string, string, string, CancellationToken, Task> runGraph,
        Func<string, CancellationToken, Task<string?>> ensureOcr)
    {
        _text = text; _folder = folder;
        Text = L("Экранное меню — текст", "Screen menu — text");
        Width = 560; Height = 570; MinimumSize = new Size(500, 420); TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Clamp(Cursor.Position.X, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Cursor.Position.Y, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = text.Length > 0 ? L("Выделенный текст скопирован в буфер обмена.", "The selected text was copied to the clipboard.") : L("Текст не скопирован. Можно выбрать область экрана.", "No text was copied. You can select a screen area."), AutoSize = true });
        var preview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 470, Height = 90, Text = text };
        layout.Controls.Add(preview);
        _voice.Items.Add(new VoiceOption(L("Автоматически", "Automatic"), ""));
        foreach (var voice in WindowsSpeech.InstalledVoices()) _voice.Items.Add(new VoiceOption($"{voice.Name} ({voice.Language})", voice.Name));
        _voice.SelectedIndex = 0;
        layout.Controls.Add(_voice);
        var buttons = new FlowLayoutPanel { Width = 480, Height = 42, WrapContents = false };
        layout.Controls.Add(buttons);
        var speak = new Button { Text = L("Озвучить", "Speak"), AutoSize = true };
        var stop = new Button { Text = L("Остановить", "Stop"), AutoSize = true };
        var save = new Button { Text = L("Сохранить в TXT", "Save as TXT"), AutoSize = true };
        buttons.Controls.AddRange([speak, stop, save]);
        var capture = new Button { Text = L("Выбрать область экрана…", "Select screen area…"), AutoSize = true };
        var ocr = new Button { Text = L("Распознать текст снимка", "Recognize screenshot text"), AutoSize = true };
        var imageButtons = new FlowLayoutPanel { Width = 500, Height = 42, WrapContents = false };
        imageButtons.Controls.AddRange([capture, ocr]); layout.Controls.Add(imageButtons);
        var languageRow = new FlowLayoutPanel { Width = 480, Height = 34, WrapContents = false };
        languageRow.Controls.Add(new Label { Text = L("Языки OCR (например eng+rus)", "OCR languages (e.g. eng+rus)"), AutoSize = true, Margin = new Padding(3, 7, 3, 0) });
        languageRow.Controls.Add(_ocrLanguages); layout.Controls.Add(languageRow);
        var note = new Button { Text = L("Создать заметку", "Create note"), AutoSize = true }; layout.Controls.Add(note);
        note.Click += (_, _) => { try { ScreenNoteManager.Show(new ScreenNoteForm(_text, _imagePath, _folder), ""); } catch (Exception error) { MessageBox.Show(error.Message); } };
        var graphChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 460 };
        foreach (var graph in graphs) graphChoice.Items.Add(graph);
        if (graphChoice.Items.Count > 0) graphChoice.SelectedIndex = 0;
        layout.Controls.Add(new Label { Text = L("Графы меню (добавляются в настройках)", "Menu graphs (add them in settings)"), AutoSize = true });
        layout.Controls.Add(graphChoice);
        var run = new Button { Text = L("Запустить граф", "Run graph"), AutoSize = true }; layout.Controls.Add(run);
        layout.Controls.Add(_status);
        speak.Click += async (_, _) =>
        {
            if (_speech is not null) return;
            var cancel = new CancellationTokenSource(); _speech = cancel;
            try { _status.Text = L("Озвучивание…", "Speaking…"); await WindowsSpeech.SpeakAsync(_text, ((VoiceOption)_voice.SelectedItem!).Name, 0, 100, cancel.Token); _status.Text = L("Готово", "Done"); }
            catch (OperationCanceledException) { _status.Text = L("Остановлено", "Stopped"); }
            catch (Exception error) { _status.Text = error.Message; }
            finally { _speech = null; cancel.Dispose(); }
        };
        stop.Click += (_, _) => { _speech?.Cancel(); _operation?.Cancel(); };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_text)) { _status.Text = L("Нет текста для сохранения.", "There is no text to save."); return; }
            try { _status.Text = L("Сохранено: ", "Saved: ") + ScreenTextMenu.SaveText(_folder, _text); }
            catch (Exception error) { MessageBox.Show(this, error.Message, L("Ошибка сохранения", "Save error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        capture.Click += async (_, _) =>
        {
            Hide();
            try
            {
                await Task.Delay(150);
                using var overlay = new CaptureOverlay(rectangleOnly: true);
                if (overlay.ShowDialog() != DialogResult.OK) return;
                await Task.Delay(150);
                Directory.CreateDirectory(_folder);
                var path = Path.Combine(_folder, "Screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N")[..8] + ".png");
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                using var image = ScreenTools.Capture(overlay.SelectedArea);
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                _imagePath = path;
                _status.Text = L("Снимок: ", "Screenshot: ") + path;
            }
            catch (Exception error) { MessageBox.Show(error.Message); }
            finally { Show(); Activate(); }
        };
        ocr.Click += async (_, _) =>
        {
            if (_imagePath.Length == 0) { _status.Text = L("Сначала выберите область экрана.", "Select a screen area first."); return; }
            if (_operation is not null) return;
            var operation = new CancellationTokenSource(); _operation = operation;
            try
            {
                TopMost = false;
                var requested = _ocrLanguages.Text.Trim();
                var required = TesseractInstaller.ParseLanguages(requested);
                var engine = "";
                try
                {
                    var languages = await OcrTools.ListLanguagesAsync(engine, operation.Token);
                    if (required.Any(language => !languages.Contains(language))) throw new FileNotFoundException();
                }
                catch (FileNotFoundException)
                {
                    engine = await ensureOcr(requested, operation.Token) ?? "";
                    if (engine.Length == 0) return;
                }
                _status.Text = L("Распознавание…", "Recognizing…");
                _text = await OcrTools.ReadAsync(_imagePath, requested, engine, 30000, operation.Token);
                preview.Text = _text; _status.Text = L("Распознавание завершено.", "Recognition complete.");
            }
            catch (OperationCanceledException) { _status.Text = L("Остановлено", "Stopped"); }
            catch (Exception error) { _status.Text = error.Message; }
            finally { _operation = null; operation.Dispose(); if (!IsDisposed) TopMost = true; }
        };
        run.Click += async (_, _) =>
        {
            if (graphChoice.SelectedItem is not ScreenMenuGraph graph) { _status.Text = L("Добавьте граф в настройках меню.", "Add a graph in menu settings."); return; }
            if (_operation is not null) return;
            var operation = new CancellationTokenSource(); _operation = operation;
            run.Enabled = false;
            try { TopMost = false; _status.Text = L("Граф выполняется…", "Graph running…"); await runGraph(graph.Path, _text, _imagePath, operation.Token); _status.Text = L("Граф завершён.", "Graph complete."); }
            catch (OperationCanceledException) { _status.Text = L("Остановлено", "Stopped"); }
            catch (Exception error) { _status.Text = error.Message; }
            finally { _operation = null; operation.Dispose(); if (!IsDisposed) { run.Enabled = true; TopMost = true; } }
        };
        FormClosing += (_, _) => { _speech?.Cancel(); _operation?.Cancel(); };
    }
    private sealed record VoiceOption(string Label, string Name) { public override string ToString() => Label; }
}

internal sealed class ScreenTextSettingsForm : Form
{
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;
    private readonly TextBox _key = new() { Width = 360 };
    private readonly TextBox _folder = new() { Width = 360 };
    private readonly ListBox _graphs = new() { Width = 430, Height = 100 };
    internal List<ScreenMenuGraph> Graphs => _graphs.Items.Cast<ScreenMenuGraph>().ToList();
    internal string Hotkey => _key.Text.Trim();
    internal string Folder => _folder.Text.Trim();
    internal ScreenTextSettingsForm(AppSettings settings)
    {
        Text = L("Настройки экранного меню", "Screen menu settings"); Width = 500; Height = 480;
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(panel);
        panel.Controls.Add(new Label { Text = L("Горячая клавиша (например Ctrl+Alt+T)", "Hotkey (for example Ctrl+Alt+T)"), AutoSize = true });
        _key.Text = settings.ScreenMenuHotkey; panel.Controls.Add(_key);
        panel.Controls.Add(new Label { Text = L("Папка для сохранения текстов", "Folder for saved text"), AutoSize = true });
        var row = new FlowLayoutPanel { Width = 440, Height = 34, WrapContents = false };
        _folder.Text = settings.ScreenMenuFolder; row.Controls.Add(_folder);
        var browse = new Button { Text = "…", Width = 40 }; row.Controls.Add(browse); panel.Controls.Add(row);
        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog { SelectedPath = _folder.Text }; if (picker.ShowDialog(this) == DialogResult.OK) _folder.Text = picker.SelectedPath; };
        panel.Controls.Add(new Label { Text = L("Графы меню: откройте .klb в редакторе для изменения", "Menu graphs: open the .klb in the editor to edit it"), AutoSize = true });
        foreach (var graph in settings.ScreenMenuGraphs ?? []) _graphs.Items.Add(new ScreenMenuGraph { Name = graph.Name, Path = graph.Path, NameRu = graph.NameRu, NameEn = graph.NameEn });
        panel.Controls.Add(_graphs);
        var graphButtons = new FlowLayoutPanel { Width = 440, Height = 35, WrapContents = false };
        var add = new Button { Text = L("Добавить граф…", "Add graph…"), AutoSize = true };
        var remove = new Button { Text = L("Убрать из меню", "Remove from menu"), AutoSize = true };
        var examples = new Button { Text = L("Добавить примеры", "Add examples"), AutoSize = true };
        graphButtons.Controls.AddRange([add, remove, examples]); panel.Controls.Add(graphButtons);
        add.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "Kliker Blocks (*.klb)|*.klb", CheckFileExists = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try { _ = ProjectCompatibility.Read(picker.FileName); _graphs.Items.Add(new ScreenMenuGraph { Name = Path.GetFileNameWithoutExtension(picker.FileName), Path = picker.FileName }); }
            catch (Exception error) { MessageBox.Show(this, error.Message); }
        };
        remove.Click += (_, _) => { if (_graphs.SelectedIndex >= 0) _graphs.Items.RemoveAt(_graphs.SelectedIndex); };
        examples.Click += (_, _) =>
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kliker Blocks", "Screen Menu Examples");
                Directory.CreateDirectory(directory);
                foreach (var (name, titleRu, kinds) in new[] {
                    ("Speak selected text", "Озвучить выбранный текст", new[] { "SpeakText" }),
                    ("Recognize and speak screenshot", "Распознать и озвучить снимок", new[] { "OcrImage", "SpeakText" }),
                    ("Create note from text", "Создать заметку из текста", new[] { "Note" }),
                    ("Copy selected text", "Скопировать текст", new[] { "ClipboardSet" }) })
                {
                    var path = Path.Combine(directory, name + ".klb");
                    if (!File.Exists(path))
                    {
                        var nodes = new List<BlockNode> { BlockCatalog.Create("Start", 80, 100) };
                        foreach (var kind in kinds)
                        {
                            var node = BlockCatalog.Create(kind, 80 + nodes.Count * 230, 100);
                            if (kind == "OcrImage") { node.Values["Path"] = "${screenshot_path}"; node.Values["Result"] = "ocr_text"; }
                            if (kind == "SpeakText") node.Values["Text"] = kinds.Length == 1 ? "${selected_text}" : "${ocr_text}";
                            if (kind == "Note") { node.Values["Id"] = ""; node.Values["TextMode"] = "Заменить"; node.Values["Text"] = "${selected_text}"; }
                            if (kind == "ClipboardSet") node.Values["Text"] = "${selected_text}";
                            nodes[^1].Links["Далее"] = node.Id; nodes.Add(node);
                        }
                        var project = new ProjectModel { Nodes = nodes, VariableNamesAsciiOnly = true, ShowIndicator = false };
                        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                        System.Text.Json.JsonSerializer.Serialize(output, project, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    }
                    if (!Graphs.Any(graph => string.Equals(graph.Path, path, StringComparison.OrdinalIgnoreCase)))
                        _graphs.Items.Add(new ScreenMenuGraph { Name = name, NameRu = titleRu, NameEn = name, Path = path });
                }
            }
            catch (Exception error) { MessageBox.Show(this, error.Message); }
        };
        var save = new Button { Text = L("Сохранить", "Save"), AutoSize = true }; panel.Controls.Add(save);
        save.Click += (_, _) => { try { _ = HotkeyParser.Parse(Hotkey); if (!Path.IsPathFullyQualified(Folder)) throw new ArgumentException(L("Выберите абсолютный путь к папке.", "Choose an absolute folder path.")); DialogResult = DialogResult.OK; } catch (Exception error) { MessageBox.Show(this, error.Message); } };
    }
}
