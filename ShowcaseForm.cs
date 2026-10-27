using System.Text.Json;

namespace KlikerBlocks;

internal sealed class ShowcaseForm : Form
{
    private readonly ProjectModel _project = ShowcaseGraph.Create();
    private readonly GraphCanvas _graph = new();
    private readonly ListBox _log = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Button _start = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(148, 38) };
    private readonly Button _stop = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(148, 38), Enabled = false };
    private readonly Button _export = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(148, 38) };
    private CancellationTokenSource? _cancel;
    private ShowcaseSession? _session;
    private Rectangle? _originalBounds;
    public bool IsRunning => _cancel is not null;

    public ShowcaseForm(bool offerStartOnOpen = false)
    {
        Text = UiLanguage.T("Что я умею — интерактивная демонстрация");
        Width = 1250; Height = 810; MinimumSize = new Size(500, 540);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) StopDemo(); };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        Controls.Add(root);
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        _start.Text = UiLanguage.T("▶ Начать демонстрацию");
        _stop.Text = UiLanguage.T("■ Остановить демонстрацию");
        _start.Click += async (_, _) => await StartDemoAsync();
        _stop.Click += (_, _) => StopDemo();
        _export.Text = UiLanguage.T("Сохранить граф…");
        _export.Click += (_, _) => SaveGraph();
        buttons.Controls.AddRange([_start, _stop, _export]);
        top.Controls.Add(buttons, 0, 0);
        _status.Text = UiLanguage.T("Граф загружен. Нажмите «Начать», чтобы управлять Блокнотом, Paint и буфером обмена.");
        top.Controls.Add(_status, 0, 1);
        root.Controls.Add(top, 0, 0);
        var editor = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(247, 249, 252) };
        _graph.SetWorldSize(18000, 1200);
        _graph.Project = _project;
        _graph.Zoom = 0.74f;
        editor.Controls.Add(_graph);
        root.Controls.Add(editor, 0, 1);
        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logPanel.Controls.Add(new Label { Dock = DockStyle.Fill,
            Text = UiLanguage.T("Журнал демонстрации") }, 0, 0);
        logPanel.Controls.Add(_log, 0, 1);
        root.Controls.Add(logPanel, 0, 2);
        FormClosing += (_, e) =>
        {
            if (_cancel is null) return;
            e.Cancel = true;
            StopDemo();
            _status.Text = UiLanguage.T("Останавливаем демонстрацию; подождите…");
        };
        if (offerStartOnOpen) Shown += async (_, _) => await StartDemoAsync();
    }

    private async Task StartDemoAsync()
    {
        if (_cancel is not null) return;
        if (Owner is MainForm main && main.IsScenarioRunning)
        {
            MessageBox.Show(this, UiLanguage.T("Сначала остановите обычный сценарий Kliker Blocks."),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var consent = MessageBox.Show(this,
            ShowcaseText.Get("consent"),
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (consent != DialogResult.Yes) return;
        try
        {
            ShowcaseState.MarkFirstRun();
            if (Owner is MainForm mainForm) mainForm.UpdateShowcaseStatus();
        }
        catch (Exception error)
        {
            AppendLog(UiLanguage.Format("Не удалось сохранить отметку первого запуска демонстрации: {0}", error.Message));
        }
        _cancel = new CancellationTokenSource();
        _start.Enabled = false; _stop.Enabled = true; _export.Enabled = false;
        _status.Text = UiLanguage.T("Демонстрация выполняется; Esc или «Остановить» прервёт её.");
        _session = new ShowcaseSession(this, AppendLog);
        var finished = false;
        string? failure = null;
        try
        {
            var runner = new Runner(_project.Copy(), AppendLog, (_, _) => { },
                baseDirectory: ShowcaseState.DemoDirectory,
                inputDialog: (message, value, delay, window, cancel) => OnUiAsync(() =>
                    DialogTools.Input(this, message, value, delay, cancel, PromptLocation(435, 205), onlyOk: true, options: window)),
                choiceDialog: (message, buttons, icon, window, cancel) => OnUiAsync(() =>
                    DialogTools.Choice(this, message, buttons, icon, cancel, PromptLocation(470, 190), options: window)),
                radioDialog: (message, options, window, cancel) => OnUiAsync(() =>
                    DialogTools.Radio(this, message, options, cancel, PromptLocation(465, 410), onlyOk: true, window: window)),
                hintPopup: (message, title) => BeginInvoke((Action)(() =>
                {
                    if (Owner is MainForm mainForm) mainForm.ShowHintPopup(message, title);
                    AppendLog(message);
                })),
                argosPythonPath: AppSettings.Load().ArgosPythonPath,
                showcaseStep: _session.ExecuteAsync);
            await runner.RunAsync(_cancel.Token);
            _status.Text = UiLanguage.T("Демонстрация завершена. Граф можно изучить и сохранить.");
            finished = true;
        }
        catch (OperationCanceledException)
        {
            AppendLog(UiLanguage.T("Демонстрация остановлена пользователем."));
            _status.Text = UiLanguage.T("Демонстрация остановлена.");
        }
        catch (Exception error)
        {
            AppendLog(UiLanguage.Format("Ошибка демонстрации: {0}", error.Message));
            _status.Text = UiLanguage.T("Демонстрация прервана; подробности в журнале.");
            failure = error.Message;
        }
        finally
        {
            _session.Dispose(); _session = null;
            _cancel.Dispose(); _cancel = null;
            RestoreLayout();
            if (!IsDisposed) { _start.Enabled = true; _stop.Enabled = false; _export.Enabled = true; }
        }
        if (IsDisposed) return;
        if (finished)
        {
            Activate();
            MessageBox.Show(this, UiLanguage.T("Демонстрация завершена. Рисунок в Paint, снимок и отчёт сохранены; пути есть в журнале."),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else if (failure is not null)
        {
            Activate();
            MessageBox.Show(this, UiLanguage.Format("Демонстрация прервана: {0}", failure),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal void ArrangeBeside(Rectangle notepadBounds)
    {
        var area = Screen.FromControl(this).WorkingArea;
        var target = ShowcaseLayout.DemoBounds(area, notepadBounds, MinimumSize.Width);
        if (target is null) return;
        _originalBounds ??= Bounds;
        Bounds = target.Value;
    }

    private void RestoreLayout()
    {
        if (_originalBounds is not { } bounds) return;
        _originalBounds = null;
        Bounds = bounds;
    }

    private void StopDemo() => _cancel?.Cancel();

    private Point PromptLocation(int width, int height)
    {
        return ShowcaseLayout.PromptLocation(Screen.FromControl(this).WorkingArea, width, height);
    }

    private void AppendLog(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke((Action)(() => AppendLog(message))); return; }
        _log.Items.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        if (_log.Items.Count > 2000) _log.Items.RemoveAt(0);
        _log.TopIndex = _log.Items.Count - 1;
    }

    private Task<T> OnUiAsync<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke((Action)(() =>
        {
            try { result.TrySetResult(action()); }
            catch (Exception error) { result.TrySetException(error); }
        }));
        return result.Task;
    }

    private void SaveGraph()
    {
        if (IsRunning) return;
        using var picker = new SaveFileDialog { Filter = "Kliker Blocks (*.klb)|*.klb", DefaultExt = "klb",
            FileName = "Kliker-Blocks-showcase.klb" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(picker.FileName, JsonSerializer.Serialize(_project,
                new JsonSerializerOptions { WriteIndented = true }));
            AppendLog(UiLanguage.Format("Граф сохранён: {0}", picker.FileName));
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
