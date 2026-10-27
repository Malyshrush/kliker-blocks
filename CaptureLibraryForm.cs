namespace KlikerBlocks;

internal sealed class CaptureLibraryForm : Form
{
    private readonly ListBox _images = new() { Dock = DockStyle.Fill };
    private readonly ListBox _points = new() { Dock = DockStyle.Fill };
    private readonly ListBox _recordings = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(235, 238, 243) };

    public CaptureLibraryForm(ProjectModel project, Action<CaptureAsset> useImage, Action<SavedPoint> usePoint,
        Action<MacroRecording> useRecording)
    {
        Text = UiLanguage.T("Библиотека точек и снимков Kliker Blocks");
        Width = 900;
        Height = 650;
        StartPosition = FormStartPosition.CenterParent;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        Controls.Add(tabs);
        var imagesTab = new TabPage("Снимки");
        var pointsTab = new TabPage("Координаты");
        var recordingsTab = new TabPage("Макросы");
        tabs.TabPages.Add(imagesTab);
        tabs.TabPages.Add(pointsTab);
        tabs.TabPages.Add(recordingsTab);

        var imageLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        imageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        imagesTab.Controls.Add(imageLayout);
        imageLayout.Controls.Add(_images, 0, 0);
        imageLayout.Controls.Add(_preview, 1, 0);
        var imageButton = new Button { Text = "Использовать в блоке «Найти изображение»", AutoSize = true, Margin = new Padding(12) };
        imageButton.Click += (_, _) =>
        {
            if (_images.SelectedItem is not CaptureAsset asset) return;
            useImage(asset);
            Close();
        };
        imageLayout.Controls.Add(imageButton, 0, 1);
        imageLayout.SetColumnSpan(imageButton, 2);
        _images.Items.AddRange(project.Captures.Cast<object>().ToArray());
        _images.SelectedIndexChanged += (_, _) => ShowPreview();
        if (_images.Items.Count > 0) _images.SelectedIndex = _images.Items.Count - 1;

        var pointLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        pointLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pointLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        pointsTab.Controls.Add(pointLayout);
        pointLayout.Controls.Add(_points, 0, 0);
        var pointButton = new Button { Text = "Вставить в выбранный блок", AutoSize = true, Margin = new Padding(12) };
        pointButton.Click += (_, _) =>
        {
            if (_points.SelectedItem is not SavedPoint point) return;
            usePoint(point);
            Close();
        };
        pointLayout.Controls.Add(pointButton, 0, 1);
        _points.Items.AddRange(project.Points.Cast<object>().ToArray());
        if (_points.Items.Count > 0) _points.SelectedIndex = _points.Items.Count - 1;

        var recordingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        recordingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        recordingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        recordingsTab.Controls.Add(recordingLayout);
        recordingLayout.Controls.Add(_recordings, 0, 0);
        var recordingButton = new Button { Text = "Добавить копию блоков в редактор", AutoSize = true, Margin = new Padding(12) };
        recordingButton.Click += (_, _) =>
        {
            if (_recordings.SelectedItem is not MacroRecording recording) return;
            useRecording(recording);
            Close();
        };
        recordingLayout.Controls.Add(recordingButton, 0, 1);
        _recordings.Items.AddRange(project.Recordings.Cast<object>().ToArray());
        if (_recordings.Items.Count > 0) _recordings.SelectedIndex = _recordings.Items.Count - 1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _preview.Image?.Dispose();
        base.Dispose(disposing);
    }

    private void ShowPreview()
    {
        _preview.Image?.Dispose();
        _preview.Image = null;
        if (_images.SelectedItem is not CaptureAsset asset || !File.Exists(asset.Path)) return;
        try
        {
            using var source = new Bitmap(asset.Path);
            _preview.Image = new Bitmap(source);
        }
        catch { /* Damaged or unsupported images remain listed for diagnostics. */ }
    }
}
