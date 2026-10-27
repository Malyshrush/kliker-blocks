using System.Diagnostics;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed class LanguageSettingsForm : Form
{
    private readonly ComboBox _uiLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly TextBox _python = new() { Width = 490 };
    private readonly TextBox _endpoint = new() { Width = 490 };
    private readonly ListBox _models = new() { Dock = DockStyle.Fill };
    private readonly ListBox _service = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly CancellationTokenSource _cancel = new();
    private readonly AppSettings _settings;
    private bool _busy;

    public AppSettings Settings => _settings;

    public LanguageSettingsForm(AppSettings current)
    {
        _settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(current)) ?? new();
        Text = UiLanguage.T("Языки интерфейса и модели перевода");
        Width = 900; Height = 730; MinimumSize = new Size(760, 620);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 8, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);
        foreach (var (code, name) in UiLanguage.Available) _uiLanguage.Items.Add(new LanguageChoice(code, name));
        _uiLanguage.SelectedIndex = Math.Max(0, UiLanguage.Available.ToList()
            .FindIndex(item => item.Code == _settings.UiLanguageCode));
        root.Controls.Add(Field("Язык интерфейса (применится сразу)", _uiLanguage), 0, 0);
        _python.Text = _settings.ArgosPythonPath is "python" or "Встроенный"
            ? (File.Exists(ArgosBridge.InstalledPython) ? ArgosBridge.InstalledPython : "")
            : _settings.ArgosPythonPath;
        root.Controls.Add(Field("Python для Argos (автоматически после установки; свой путь необязателен)", _python), 0, 1);
        _endpoint.Text = _settings.TranslationEndpoint;
        root.Controls.Add(Field("LibreTranslate: адрес /translate (необязательно для Argos)", _endpoint), 0, 2);
        var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        root.Controls.Add(commands, 0, 3);
        Add(commands, "Скачать офлайн-перевод…", 205, InstallOnline);
        Add(commands, "Проверить Argos", 135, async () => await RefreshModels());
        Add(commands, "Установить .argosmodel…", 180, async () => await InstallModel());
        Add(commands, "Проверить LibreTranslate", 190, async () => await RefreshService());
        Add(commands, "Каталог моделей", 140, () =>
            Process.Start(new ProcessStartInfo("https://www.argosopentech.com/argospm/index/") { UseShellExecute = true }));
        Add(commands, "Как установить модель…", 180, ShowInstallHelp);
        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(lists, 0, 4);
        lists.Controls.Add(ListPanel("Установленные локальные модели Argos", _models), 0, 0);
        lists.Controls.Add(ListPanel("Языковые пары сервиса LibreTranslate", _service), 1, 0);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, AutoEllipsis = true,
            Text = UiLanguage.Code == "en"
                ? $"The base app contains no Python or models. Offline models are saved in {ArgosBridge.ModelsDirectory}. Translation is sent to a service only when a service block runs."
                : $"В базовой программе нет Python и моделей. Офлайн-модели хранятся в {ArgosBridge.ModelsDirectory}. Текст отправляется сервису только при запуске блока с этим движком." }, 0, 5);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        root.Controls.Add(bottom, 0, 7);
        Add(bottom, "Сохранить", 110, Save);
        Add(bottom, "Отмена", 100, () => { DialogResult = DialogResult.Cancel; Close(); });
        root.Controls.Add(_status, 0, 6);
        _status.Text = UiLanguage.Code == "en"
            ? "Translation requires either a configured LibreTranslate service or optional downloaded Argos models."
            : "Для перевода нужен настроенный сервис LibreTranslate либо отдельно загруженные модели Argos.";
        FormClosing += (_, _) => _cancel.Cancel();
    }

    private static Control Field(string label, Control input)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false };
        panel.Controls.Add(new Label { Text = UiLanguage.T(label), Width = 750, Height = 23 });
        panel.Controls.Add(input);
        return panel;
    }

    private static Control ListPanel(string label, ListBox list)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(0, 0, 8, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = UiLanguage.T(label), Dock = DockStyle.Fill }, 0, 0);
        panel.Controls.Add(list, 0, 1);
        return panel;
    }

    private static void Add(FlowLayoutPanel panel, string text, int width, Action action)
    {
        var button = new Button { Text = UiLanguage.T(text), Width = width, Height = 31 };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
    }
    private static void Add(FlowLayoutPanel panel, string text, int width, Func<Task> action)
    {
        var button = new Button { Text = UiLanguage.T(text), Width = width, Height = 31 };
        button.Click += async (_, _) => await action();
        panel.Controls.Add(button);
    }

    private async Task RefreshModels()
    {
        if (_busy) return;
        _busy = true; _status.Text = UiLanguage.T("Проверка локальных моделей…");
        try
        {
            var models = await ArgosBridge.ListAsync(_python.Text, _cancel.Token);
            if (IsDisposed) return;
            _models.Items.Clear();
            foreach (var model in models) _models.Items.Add($"{model.Source} → {model.Target}  {model.Name}");
            _status.Text = models.Count == 0 ? UiLanguage.T("Локальные модели не установлены.")
                : UiLanguage.Format("Локальных моделей: {0}.", models.Count);
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception error) { if (!IsDisposed) _status.Text = error.Message; }
        finally { _busy = false; }
    }

    private void InstallOnline()
    {
        using var setup = new OfflineTranslationSetupForm();
        if (setup.ShowDialog(this) != DialogResult.OK) return;
        _python.Text = setup.InstalledPythonPath;
        _settings.ArgosPythonPath = setup.InstalledPythonPath;
        _settings.Save();
        _ = RefreshModels();
    }

    private async Task InstallModel()
    {
        if (_busy) return;
        using var picker = new OpenFileDialog { Filter = UiLanguage.T("Модель Argos (*.argosmodel)") + "|*.argosmodel",
            CheckFileExists = true, Title = UiLanguage.T("Установить локальную языковую модель") };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        ArgosPackageInfo package;
        try { package = ArgosBridge.ReadPackageInfo(picker.FileName); }
        catch (Exception error) { _status.Text = error.Message; return; }
        if (!TranslationTools.IsLanguage(package.Source) || !TranslationTools.IsLanguage(package.Target))
        {
            _status.Text = UiLanguage.Format("Пакет {0} → {1} не используется блоками Kliker Blocks. Выберите пару ru/en/fr/zh/es/de.",
                package.Source, package.Target);
            return;
        }
        _busy = true;
        _status.Text = UiLanguage.Format("Устанавливается пакет {0} → {1}. Это может занять несколько минут…",
            package.Source, package.Target);
        try
        {
            await ArgosBridge.InstallFileAsync(_python.Text, picker.FileName, _cancel.Token);
            if (IsDisposed) return;
            _status.Text = UiLanguage.Format("Модель {0} → {1} установлена. Выберите «Argos локально» в блоке перевода.",
                package.Source, package.Target);
            _busy = false;
            await RefreshModels();
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception error) { if (!IsDisposed) _status.Text = error.Message; }
        finally { _busy = false; }
    }

    private async Task RefreshService()
    {
        if (_busy) return;
        if (string.IsNullOrWhiteSpace(_endpoint.Text))
        {
            _status.Text = UiLanguage.T("Адрес LibreTranslate не задан. Локальному Argos сервер не требуется.");
            return;
        }
        _busy = true; _status.Text = UiLanguage.T("Проверка сервиса перевода…");
        try
        {
            var languages = await TranslationTools.GetLanguagesAsync(_endpoint.Text, 10000, _cancel.Token);
            if (IsDisposed) return;
            _service.Items.Clear();
            foreach (var language in languages)
                _service.Items.Add($"{language.Code} → {string.Join(", ", language.Targets)}");
            _status.Text = UiLanguage.Format("Сервис доступен; языков: {0}.", languages.Count);
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (HttpRequestException error) when (!IsDisposed)
        {
            _status.Text = UiLanguage.Format("Нет подключения к LibreTranslate ({0}). Это отдельный необязательный сервер; для «Argos локально» он не нужен.",
                error.HttpRequestError == HttpRequestError.ConnectionError ? _endpoint.Text : error.Message);
        }
        catch (Exception error) { if (!IsDisposed) _status.Text = error.Message; }
        finally { _busy = false; }
    }

    private void Save()
    {
        if (_uiLanguage.SelectedItem is not LanguageChoice choice) return;
        if (_endpoint.Text.Trim().Length > 0 && (!Uri.TryCreate(_endpoint.Text.Trim(), UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length > 0))
        { MessageBox.Show(this, UiLanguage.T("Проверьте адрес HTTP/HTTPS сервиса.")); return; }
        _settings.UiLanguageCode = choice.Code;
        _settings.ArgosPythonPath = string.IsNullOrWhiteSpace(_python.Text) ? "Встроенный" : _python.Text.Trim();
        _settings.TranslationEndpoint = _endpoint.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowInstallHelp()
    {
        MessageBox.Show(this, UiLanguage.Code == "en"
            ? "Choose a language pair and click ‘Download offline translation’. Kliker Blocks installs Python and only the required Argos models after confirmation. Then select ‘Argos local’ in translation blocks. A LibreTranslate service is an alternative; its address and availability are managed separately."
            : "Выберите языковую пару и нажмите «Скачать офлайн-перевод». После подтверждения Kliker Blocks установит Python и только нужные модели Argos. Затем выберите «Argos локально» в блоках перевода. Альтернатива — отдельный сервис LibreTranslate; его адрес и доступность настраиваются отдельно.",
            UiLanguage.T("Установка языковой модели"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private sealed record LanguageChoice(string Code, string Name)
    {
        public override string ToString() => Name;
    }
}
