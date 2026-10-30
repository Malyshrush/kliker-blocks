namespace KlikerBlocks;

internal sealed class OcrSetupForm : Form
{
    private readonly string _languages;
    private readonly Button _install = new() { Width = 190, Height = 38 };
    private readonly Label _status = new() { AutoSize = false, Width = 525, Height = 80 };
    private readonly CancellationTokenSource _cancel = new();
    private bool _busy;

    internal string EnginePath { get; private set; } = "";
    private static string L(string ru, string en) => UiLanguage.Code == "en" ? en : ru;

    internal OcrSetupForm(string languages)
    {
        _languages = languages;
        Text = L("Установить Tesseract OCR", "Install Tesseract OCR");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 580;
        Height = 330;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(17),
            FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Controls.Add(panel);
        panel.Controls.Add(new Label { Width = 525, Height = 86, Text = L(
            "Блоку «Распознать текст» нужен локальный Tesseract OCR. По вашему согласию Kliker Blocks скачает Windows-сборку Tesseract 5.5.0 из релиза Tesseract на GitHub (20,4 МиБ), установит её только для текущего пользователя и добавит модели: " + languages + ".\n\nМодели обычно добавляют несколько МиБ каждая. Изображения остаются на компьютере.",
            "The “Recognize text” block needs local Tesseract OCR. With your consent, Kliker Blocks downloads the Tesseract 5.5.0 Windows build from the Tesseract GitHub release (20.4 MiB), installs it for the current user only, and adds these models: " + languages + ".\n\nModels usually add a few MiB each. Images stay on this computer."),
            MaximumSize = new Size(525, 0) });
        _install.Text = L("Скачать и установить", "Download and install");
        _install.Click += async (_, _) => await InstallAsync();
        panel.Controls.Add(_install);
        var cancel = new Button { Text = L("Нет", "No"), Width = 105, Height = 38 };
        cancel.Click += (_, _) => Close();
        panel.Controls.Add(cancel);
        _status.Text = L("Без Tesseract блок не запускается. Можно закрыть это окно и указать путь к уже установленному tesseract.exe в свойствах блока.",
            "Without Tesseract the block cannot run. You can close this window and enter the path to an existing tesseract.exe in the block properties.");
        panel.Controls.Add(_status);
        FormClosing += (_, args) =>
        {
            if (!_busy) { _cancel.Cancel(); return; }
            if (MessageBox.Show(this, L("Прервать загрузку и установку Tesseract?", "Cancel Tesseract download and setup?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) args.Cancel = true;
            else _cancel.Cancel();
        };
    }

    private async Task InstallAsync()
    {
        if (_busy) return;
        try { _ = TesseractInstaller.ParseLanguages(_languages); }
        catch (Exception error) { _status.Text = error.Message; return; }
        var confirmation = L(
            "Продолжить? Будут загружены Tesseract и только модели из поля «Код языка OCR». Компоненты будут установлены в:\n",
            "Continue? Tesseract and only the models from the “OCR language code” field will be downloaded. Components will be installed in:\n") +
            TesseractInstaller.Root;
        if (MessageBox.Show(this, confirmation, Text, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
        _busy = true;
        _install.Enabled = false;
        try
        {
            var progress = new Progress<string>(message => { if (!IsDisposed) _status.Text = message; });
            EnginePath = await TesseractInstaller.InstallAsync(_languages, progress, _cancel.Token);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception error)
        {
            _status.Text = L("Установка не завершена: ", "Installation did not finish: ") + error.Message;
            MessageBox.Show(this, _status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _busy = false; _install.Enabled = true; }
    }
}
