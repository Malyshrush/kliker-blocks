namespace KlikerBlocks;

internal sealed class OfflineTranslationSetupForm : Form
{
    private static string L(string ru, string en) => UiLanguage.Code == "en" ? en : ru;
    private readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
    private readonly ComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
    private readonly CheckBox _reverse = new() { AutoSize = true, Checked = true };
    private readonly Button _install = new() { Width = 170, Height = 38 };
    private readonly Label _status = new() { AutoSize = false, Width = 530, Height = 76 };
    private readonly CancellationTokenSource _cancel = new();
    private bool _busy;
    internal string InstalledPythonPath { get; private set; } = "";

    internal OfflineTranslationSetupForm(string source = "ru", string target = "en")
    {
        Text = L("Скачать офлайн-перевод", "Download offline translation");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 590;
        Height = 375;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(17),
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(panel);
        panel.Controls.Add(new Label { Text = L(
            "Выберите языки. Kliker Blocks загрузит только нужные компоненты после вашего подтверждения.",
            "Choose languages. Kliker Blocks downloads only the required components after your confirmation."),
            Width = 535, Height = 42 });
        foreach (var (code, ru, en) in new[]
        {
            ("ru", "Русский", "Russian"), ("en", "Английский", "English"),
            ("fr", "Французский", "French"), ("es", "Испанский", "Spanish"),
            ("zh", "Китайский", "Chinese"), ("de", "Немецкий", "German")
        })
        {
            _source.Items.Add(new Choice(code, L(ru, en)));
            _target.Items.Add(new Choice(code, L(ru, en)));
        }
        _source.SelectedIndex = Math.Max(0, _source.Items.Cast<Choice>().ToList().FindIndex(item => item.Code == source));
        _target.SelectedIndex = Math.Max(0, _target.Items.Cast<Choice>().ToList().FindIndex(item => item.Code == target));
        panel.Controls.Add(new Label { Text = L("Исходный язык", "Source language"), Width = 530, Height = 23 });
        panel.Controls.Add(_source);
        panel.Controls.Add(new Label { Text = L("Язык перевода", "Target language"), Width = 530, Height = 23 });
        panel.Controls.Add(_target);
        _reverse.Text = L("Также установить обратное направление", "Also install the reverse direction");
        panel.Controls.Add(_reverse);
        var buttons = new FlowLayoutPanel { Width = 535, Height = 48 };
        _install.Text = L("Скачать и установить", "Download and install");
        _install.Click += async (_, _) => await InstallAsync();
        buttons.Controls.Add(_install);
        var close = new Button { Text = L("Закрыть", "Close"), Width = 105, Height = 38 };
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close);
        panel.Controls.Add(buttons);
        _status.Text = L("Локальный перевод необязателен. Без него используйте подключённый сервис LibreTranslate.",
            "Offline translation is optional. Without it, use a connected LibreTranslate service.");
        panel.Controls.Add(_status);
        FormClosing += (_, args) =>
        {
            if (!_busy) { _cancel.Cancel(); return; }
            if (MessageBox.Show(this, L("Прервать загрузку и установку?", "Cancel download and installation?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            { args.Cancel = true; return; }
            _cancel.Cancel();
        };
    }

    private async Task InstallAsync()
    {
        if (_busy || _source.SelectedItem is not Choice source || _target.SelectedItem is not Choice target) return;
        if (source.Code == target.Code)
        {
            MessageBox.Show(this, L("Выберите разные языки.", "Choose different languages."));
            return;
        }
        var confirmation = L(
            "Kliker Blocks загрузит Python с python.org, движок Argos и выбранные модели из каталога Argos. " +
            "Это может занять несколько сотен мегабайт и несколько минут. Компоненты будут установлены для вашей учётной записи. Продолжить?",
            "Kliker Blocks will download Python from python.org, the Argos engine, and selected models from the Argos catalogue. " +
            "This may use several hundred megabytes and take several minutes. Components will be installed for your account. Continue?");
        if (MessageBox.Show(this, confirmation, Text, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
        _busy = true;
        _install.Enabled = false;
        var progress = new Progress<string>(message => { if (!IsDisposed) _status.Text = LocalizeProgress(message); });
        try
        {
            InstalledPythonPath = await OfflineTranslationInstaller.InstallAsync(source.Code, target.Code,
                _reverse.Checked, progress, _cancel.Token);
            _status.Text = L("Готово. Выберите «Argos локально» в нужном блоке; перезапуск не требуется.",
                "Ready. Select “Argos local” in the block; no restart is needed.");
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception error)
        {
            _status.Text = L("Установка не завершена: ", "Installation did not finish: ") + error.Message;
            MessageBox.Show(this, _status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _busy = false; _install.Enabled = true; }
    }

    private static string LocalizeProgress(string message)
    {
        if (UiLanguage.Code == "en") return message;
        if (message.StartsWith("Downloading ") && message.Contains('→'))
            return "Загрузка модели " + message[12..];
        if (message.StartsWith("Installing ") && message.Contains('→'))
            return "Установка модели " + message[11..];
        return message switch
        {
            "Reading the Argos package catalogue…" => "Чтение каталога моделей Argos…",
            "The selected language pair is already installed." => "Выбранная языковая пара уже установлена.",
            "Downloading Python from python.org…" => "Загрузка Python с python.org…",
            "Installing Python for this user…" => "Установка Python для текущего пользователя…",
            "Creating a private Python environment for Kliker Blocks…" => "Создание отдельного окружения Python для Kliker Blocks…",
            "Installing the Argos translation engine from PyPI…" => "Установка движка Argos из PyPI…",
            "Offline translation is ready. Select “Argos local” in a translation block." =>
                "Офлайн-перевод готов. Выберите «Argos локально» в блоке перевода.",
            _ => message
        };
    }

    private sealed record Choice(string Code, string Title)
    {
        public override string ToString() => Title;
    }
}
