using System.Text.Json;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal sealed class ScreenNoteForm : Form
{
    internal sealed class NoteData
    {
        public string Text { get; set; } = "";
        public string RichText { get; set; } = "";
        public float ImageZoom { get; set; } = 1f;
        public float ImageOffsetX { get; set; }
        public float ImageOffsetY { get; set; }
        public string ImageBase64 { get; set; } = "";
        public bool TextVisible { get; set; } = true;
        public bool ImageVisible { get; set; } = true;
        public bool PreserveImageAspect { get; set; }
        public double SplitRatio { get; set; } = 0.55;
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;
    private readonly RichTextBox _text = new() { Multiline = true, Dock = DockStyle.Fill, ScrollBars = RichTextBoxScrollBars.Vertical, AcceptsTab = true };
    private readonly NotePictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage };
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Color.FromArgb(52, 140, 230), Panel1MinSize = 40, Panel2MinSize = 40 };
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(239, 243, 249), Visible = false };
    private readonly Label _caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(9, 0, 0, 0), Cursor = Cursors.SizeAll };
    private readonly System.Windows.Forms.Timer _headerTimer = new() { Interval = 3000 };
    private readonly ToolTip _tips = new();
    private bool _textEnabled = true;
    private bool _imageEnabled;
    private bool _inMoveOrResize;
    private Rectangle _boundsBeforeMove;
    private bool _applyingSections;
    private bool _splitterDragging;
    private double _splitRatio = 0.55;
    private string? _path;
    private readonly string _folder;
    private bool _dirty;
    internal string? SavedPath => _path;
    internal string NoteId = "";
    internal string GetNoteText() => _text.Text;
    internal void RefreshTitle() { Text = L("Заметка", "Note") + (NoteId.Length == 0 ? "" : " [" + NoteId + "]") + (_path is null ? "" : " — " + _path); _caption.Text = Text; }

    internal ScreenNoteForm(string text = "", string imagePath = "", string? folder = null)
    {
        _folder = folder ?? ProjectCompatibility.UserProjectDirectory;
        Text = L("Заметка — Kliker Blocks", "Note — Kliker Blocks");
        Width = 400; Height = 380; MinimumSize = new Size(270, 200); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(6); BackColor = Color.FromArgb(188, 201, 217);
        TopMost = true; ShowInTaskbar = true;
        var titleBar = new Panel { Dock = DockStyle.Top, Height = 32 };
        var windowButtons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 102, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var minimize = IconButton("−", L("Свернуть", "Minimize"));
        var maximize = IconButton("□", L("Развернуть или восстановить размер", "Maximize or restore"));
        var close = IconButton("✕", L("Закрыть заметку", "Close note"));
        windowButtons.Controls.AddRange([minimize, maximize, close]); titleBar.Controls.Add(_caption); titleBar.Controls.Add(windowButtons);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 0, 0), WrapContents = false };
        var save = IconButton("💾", L("Сохранить заметку", "Save note"));
        var format = IconButton("𝐀", L("Форматирование выделенного текста", "Format selected text"));
        var formatting = new ContextMenuStrip();
        void StyleItem(string ru, string en, FontStyle style)
        {
            formatting.Items.Add(L(ru, en), null, (_, _) => { var font = _text.SelectionFont ?? _text.Font; _text.SelectionFont = new Font(font, font.Style ^ style); _dirty = true; _text.Focus(); });
        }
        StyleItem("Жирный", "Bold", FontStyle.Bold);
        StyleItem("Курсив", "Italic", FontStyle.Italic);
        StyleItem("Подчёркнутый", "Underline", FontStyle.Underline);
        formatting.Items.Add(L("Шрифт и размер…", "Font and size…"), null, (_, _) => { using var dialog = new FontDialog { Font = _text.SelectionFont ?? _text.Font }; if (dialog.ShowDialog(this) == DialogResult.OK) { _text.SelectionFont = dialog.Font; _dirty = true; } _text.Focus(); });
        formatting.Items.Add(L("Цвет текста…", "Text color…"), null, (_, _) => { using var dialog = new ColorDialog { Color = _text.SelectionColor.IsEmpty ? _text.ForeColor : _text.SelectionColor }; if (dialog.ShowDialog(this) == DialogResult.OK) { _text.SelectionColor = dialog.Color; _dirty = true; } _text.Focus(); });
        format.Click += (_, _) => { _headerTimer.Stop(); formatting.Show(format, new Point(0, format.Height)); };
        formatting.Closed += (_, _) => ShowHeader();
        FormClosed += (_, _) => formatting.Dispose();
        var addText = IconButton("📝", L("Добавить текстовую часть", "Add text section"));
        var removeText = IconButton("🗑📝", L("Удалить текстовую часть", "Remove text section"));
        var image = IconButton("🖼", L("Добавить изображение", "Add image"));
        var remove = IconButton("🗑🖼", L("Удалить изображение", "Remove image"));
        var fit = IconButton("⤢", L("Вписать изображение с пропорциями или растянуть", "Fit image with its proportions or stretch it"));
        var delete = IconButton("🗑", L("Удалить заметку", "Delete note"));
        bar.Controls.AddRange([save, format, addText, removeText, image, remove, fit, delete]);
        _header.Controls.Add(bar); _header.Controls.Add(titleBar);
        _split.Panel1.BackColor = Color.White; _split.Panel2.BackColor = Color.White;
        _split.Panel1.Controls.Add(_text); _split.Panel2.Controls.Add(_picture);
        Controls.Add(_split); Controls.Add(_header);
        _picture.ViewChanged += (_, _) => _dirty = true;
        _tips.SetToolTip(_picture, L("Ctrl + плюс/минус — масштаб; перетаскивание — положение изображения", "Ctrl + plus/minus zooms; drag to position the image"));
        var pictureMenu = new ContextMenuStrip();
        pictureMenu.Items.Add(L("Сбросить масштаб и положение", "Reset zoom and position"), null,
            (_, _) => { _picture.SetView(1f, 0f, 0f); _dirty = true; });
        _picture.ContextMenuStrip = pictureMenu;
        FormClosed += (_, _) => pictureMenu.Dispose();
        var resizeGrip = new Label { Text = "◢", AutoSize = false, Width = 18, Height = 18,
            Location = new Point(ClientSize.Width - 18, ClientSize.Height - 18), Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(225, 233, 243), ForeColor = Color.FromArgb(77, 100, 129),
            TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.SizeNWSE,
            AccessibleName = L("Изменить размер заметки", "Resize note") };
        _tips.SetToolTip(resizeGrip, L("Потяните, чтобы изменить размер заметки", "Drag to resize the note"));
        resizeGrip.MouseDown += (_, e) => { if (e.Button != MouseButtons.Left || WindowState != FormWindowState.Normal) return; ShowHeader(); ReleaseCapture(); SendMessage(Handle, 0x00A1, (IntPtr)17, IntPtr.Zero); ShowHeader(); };
        Controls.Add(resizeGrip); resizeGrip.BringToFront();
        _text.MouseDown += (_, _) => ShowHeader();
        _text.KeyDown += (_, _) => ShowHeader();
        _picture.MouseDown += (_, _) => ShowHeader();
        _split.MouseDown += (_, _) => ShowHeader();
        _split.Panel1.MouseDown += (_, _) => ShowHeader();
        _split.Panel2.MouseDown += (_, _) => ShowHeader();
        MouseDown += (_, _) => ShowHeader();
        _split.SplitterMoving += (_, _) => _splitterDragging = true;
        _split.SplitterMoved += (_, _) => { if (_applyingSections || !_splitterDragging) return; _splitterDragging = false; if (_textEnabled && _imageEnabled && _split.Height > _split.SplitterWidth) { _splitRatio = (double)_split.SplitterDistance / (_split.Height - _split.SplitterWidth); _dirty = true; } ShowHeader(); };
        _caption.MouseDown += StartDrag; titleBar.MouseDown += StartDrag;
        _headerTimer.Tick += (_, _) =>
        {
            if (_splitterDragging && (Control.MouseButtons & MouseButtons.Left) == 0) _splitterDragging = false;
            if (!_inMoveOrResize && !_splitterDragging && !_picture.Capture) { _headerTimer.Stop(); _header.Visible = false; }
        };
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximize.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        close.Click += (_, _) => Close();
        fit.Click += (_, _) => SetImagePreserveAspect(_picture.SizeMode != PictureBoxSizeMode.Zoom);
        _text.Text = text;
        if (!string.IsNullOrEmpty(imagePath)) SetImage(imagePath);
        _textEnabled = text.Length > 0 || !_imageEnabled;
        UpdateSections();
        _dirty = text.Length > 0 || _picture.Image is not null;
        _text.TextChanged += (_, _) => { _dirty = true; if (_header.Visible) ShowHeader(); };
        save.Click += (_, _) => SaveNote();
        addText.Click += (_, _) => { _textEnabled = true; UpdateSections(); _dirty = true; _text.Focus(); };
        removeText.Click += (_, _) => { if (!_textEnabled) return; if (_text.TextLength > 0 && MessageBox.Show(this, L("Удалить текст из заметки?", "Remove the note text?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; RemoveTextSection(); };
        image.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = L("Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif") }; if (dialog.ShowDialog(this) == DialogResult.OK) try { SetImage(dialog.FileName); _dirty = true; } catch (Exception error) { MessageBox.Show(this, error.Message); } };
        remove.Click += (_, _) => { if (!_imageEnabled) return; if (MessageBox.Show(this, L("Удалить изображение из заметки?", "Remove the note image?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; RemoveImage(); _dirty = true; };
        delete.Click += (_, _) =>
        {
            var prompt = _path is null ? L("Удалить эту несохранённую заметку?", "Delete this unsaved note?") : L("Удалить заметку и её файл?\n", "Delete the note and its file?\n") + _path;
            if (MessageBox.Show(this, prompt, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { if (_path is not null && File.Exists(_path)) File.Delete(_path); _dirty = false; Close(); }
            catch (Exception error) { MessageBox.Show(this, error.Message, L("Ошибка удаления", "Delete error")); }
        };
        FormClosing += (_, e) =>
        {
            if (!_dirty) return;
            var answer = MessageBox.Show(this, L("Сохранить изменения заметки перед закрытием?", "Save changes to the note before closing?"), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !SaveNote())) e.Cancel = true;
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _headerTimer.Stop(); _headerTimer.Dispose(); _tips.Dispose();
            var image = _picture.Image; _picture.Image = null; image?.Dispose();
        }
        base.Dispose(disposing);
    }

    internal bool HasTextSection() => _textEnabled;
    internal bool HasImageSection() => _imageEnabled;
    internal bool PreservesImageAspect() => _picture.SizeMode == PictureBoxSizeMode.Zoom;

    internal void SetImagePreserveAspect(bool preserve)
    {
        _picture.SizeMode = preserve ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.StretchImage;
        _dirty = true;
    }

    internal void RemoveTextSection()
    {
        _text.Clear(); _textEnabled = false; UpdateSections(); _dirty = true;
    }

    internal void SetPlacement(int? x, int? y, int? width, int? height)
    {
        var before = Bounds;
        if (x.HasValue || y.HasValue) { StartPosition = FormStartPosition.Manual; Location = new Point(x ?? Left, y ?? Top); }
        if (width.HasValue || height.HasValue) Size = new Size(width ?? Width, height ?? Height);
        if (Bounds != before) _dirty = true;
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    private Button IconButton(string icon, string description)
    {
        var button = new Button { Text = icon, Width = 28, Height = 30, Margin = new Padding(1), FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Emoji", 9), AccessibleName = description, TabStop = true };
        _tips.SetToolTip(button, description);
        button.MouseMove += (_, _) => ShowHeader();
        return button;
    }

    private void ShowHeader()
    {
        if (IsDisposed) return;
        _header.Visible = true; _headerTimer.Stop(); _headerTimer.Start();
    }

    private void StartDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ShowHeader(); ReleaseCapture(); SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero); ShowHeader();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0231) { _inMoveOrResize = true; _boundsBeforeMove = Bounds; }
        if (m.Msg == 0x0232) { _inMoveOrResize = false; if (Bounds != _boundsBeforeMove) _dirty = true; ShowHeader(); }
        base.WndProc(ref m);
        if (m.Msg != 0x0084 || WindowState != FormWindowState.Normal || m.Result != (IntPtr)1) return;
        var point = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
        const int edge = 6;
        var left = point.X < edge; var right = point.X >= Width - edge;
        var top = point.Y < edge; var bottom = point.Y >= Height - edge;
        if (top && left) m.Result = (IntPtr)13; else if (top && right) m.Result = (IntPtr)14;
        else if (bottom && left) m.Result = (IntPtr)16; else if (bottom && right) m.Result = (IntPtr)17;
        else if (left) m.Result = (IntPtr)10; else if (right) m.Result = (IntPtr)11;
        else if (top) m.Result = (IntPtr)12; else if (bottom) m.Result = (IntPtr)15;
    }

    private void UpdateSections()
    {
        _applyingSections = true;
        try
        {
            _split.Panel1Collapsed = false; _split.Panel2Collapsed = false;
            _text.Visible = _textEnabled;
            _picture.Visible = _imageEnabled;
            if (!_imageEnabled) _split.Panel2Collapsed = true;
            else if (!_textEnabled) _split.Panel1Collapsed = true;
            else if (_split.Height >= 100)
                _split.SplitterDistance = Math.Clamp((int)((_split.Height - _split.SplitterWidth) * _splitRatio), 40, _split.Height - _split.SplitterWidth - 40);
        }
        finally { _applyingSections = false; }
    }

    private void RemoveImage()
    {
        var previous = _picture.Image; _picture.Image = null; previous?.Dispose();
        _imageEnabled = false; UpdateSections();
    }

    private void SetImage(string path)
    {
        using var source = Image.FromFile(path);
        var replacement = new Bitmap(source);
        var previous = _picture.Image; _picture.Image = replacement; previous?.Dispose();
        _picture.SetView(1f, 0f, 0f);
        _imageEnabled = true; UpdateSections();
    }

    internal void SetContents(string mode, string text, string imageMode, string imagePath)
    {
        if (imageMode == "Из файла") { SetImage(imagePath); _dirty = true; }
        else if (imageMode == "Убрать") { RemoveImage(); _dirty = true; }
        if (mode == "Заменить") { _textEnabled = true; _text.Text = text; UpdateSections(); }
        else if (mode == "Добавить") { _textEnabled = true; _text.AppendText(text); UpdateSections(); }
    }

    internal void SaveTo(string path)
    {
        if (!Path.GetExtension(path).Equals(".klnote", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(L("Файл заметки должен иметь расширение .klnote.", "Note files must use the .klnote extension."));
        if (ScreenNoteManager.IsFileOpen(path, this))
            throw new InvalidOperationException(L("Этот файл уже открыт в другой заметке.", "This file is already open in another note."));
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var data = new NoteData { Text = _text.Text, RichText = _text.Rtf, ImageZoom = _picture.ViewZoom, ImageOffsetX = _picture.ViewOffset.X, ImageOffsetY = _picture.ViewOffset.Y, TextVisible = _textEnabled, ImageVisible = _imageEnabled, SplitRatio = _splitRatio,
            PreserveImageAspect = _picture.SizeMode == PictureBoxSizeMode.Zoom,
            X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height };
        if (_picture.Image is not null) { using var bytes = new MemoryStream(); _picture.Image.Save(bytes, System.Drawing.Imaging.ImageFormat.Png); data.ImageBase64 = Convert.ToBase64String(bytes.ToArray()); }
        var serialized = JsonSerializer.Serialize(data);
        if (System.Text.Encoding.UTF8.GetByteCount(serialized) > 32 * 1024 * 1024)
            throw new InvalidOperationException(L("Заметка превышает 32 МиБ. Уменьшите изображение или текст.", "The note exceeds 32 MiB. Reduce the image or text."));
        var absolute = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(absolute)!, ".kliker-note-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temporary, serialized); File.Move(temporary, absolute, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        _path = absolute; _dirty = false; RefreshTitle();
    }

    internal static ScreenNoteForm LoadFrom(string path)
    {
        if (!Path.GetExtension(path).Equals(".klnote", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(L("Файл заметки должен иметь расширение .klnote.", "Note files must use the .klnote extension."));
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException(L("Файл заметки слишком большой.", "The note file is too large."));
        var data = JsonSerializer.Deserialize<NoteData>(File.ReadAllText(path)) ?? throw new InvalidDataException();
        var form = new ScreenNoteForm(data.Text) { _path = Path.GetFullPath(path) };
        try
        {
            if (!string.IsNullOrEmpty(data.RichText)) { form._text.Rtf = data.RichText; if (form._text.Text != data.Text) throw new InvalidDataException(L("Содержимое форматированного текста заметки повреждено.", "The note rich text is inconsistent.")); }
            form._picture.SetView(data.ImageZoom, data.ImageOffsetX, data.ImageOffsetY);
            if (!string.IsNullOrEmpty(data.ImageBase64)) { using var bytes = new MemoryStream(Convert.FromBase64String(data.ImageBase64)); using var source = Image.FromStream(bytes); form._picture.Image = new Bitmap(source); }
            form._textEnabled = data.TextVisible; form._imageEnabled = data.ImageVisible && form._picture.Image is not null;
            form._picture.SizeMode = data.PreserveImageAspect ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.StretchImage;
            form._splitRatio = double.IsFinite(data.SplitRatio) ? Math.Clamp(data.SplitRatio, 0.1, 0.9) : 0.55;
            if (data.Width is >= 270 and <= 3000 && data.Height is >= 200 and <= 3000)
                form.Size = new Size(data.Width.Value, data.Height.Value);
            if (data.X.HasValue && data.Y.HasValue && Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(new Rectangle(data.X.Value, data.Y.Value, 50, 50))))
            { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(data.X.Value, data.Y.Value); }
            form.UpdateSections(); form._headerTimer.Stop(); form._header.Visible = false;
            form._dirty = false; form.RefreshTitle();
            return form;
        }
        catch { form.Dispose(); throw; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_imageEnabled && keyData is (Keys.Control | Keys.Oemplus) or (Keys.Control | Keys.Shift | Keys.Oemplus) or (Keys.Control | Keys.Add)) { _picture.ZoomBy(1.2f); ShowHeader(); return true; }
        if (_imageEnabled && keyData is (Keys.Control | Keys.OemMinus) or (Keys.Control | Keys.Subtract)) { _picture.ZoomBy(1f / 1.2f); ShowHeader(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool SaveNote()
    {
        if (_path is not null)
        {
            try { SaveTo(_path); return true; }
            catch (Exception error) { MessageBox.Show(this, error.Message, L("Ошибка сохранения", "Save error")); return false; }
        }
        using var dialog = new SaveFileDialog { Filter = L("Заметка Kliker (*.klnote)|*.klnote", "Kliker note (*.klnote)|*.klnote"), DefaultExt = "klnote", FileName = _path is null ? L("Заметка", "Note") : Path.GetFileName(_path), InitialDirectory = _path is null ? _folder : Path.GetDirectoryName(_path) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        try
        {
            SaveTo(dialog.FileName);
            return true;
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, L("Ошибка сохранения", "Save error")); return false; }
    }

    internal static void OpenNote()
    {
        using var dialog = new OpenFileDialog { Filter = L("Заметка Kliker (*.klnote)|*.klnote", "Kliker note (*.klnote)|*.klnote") };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            var form = LoadFrom(dialog.FileName); ScreenNoteManager.Show(form, "");
        }
        catch (Exception error) { MessageBox.Show(error.Message, L("Ошибка открытия заметки", "Note open error")); }
    }
}
