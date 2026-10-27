using System.Drawing;

namespace KlikerBlocks;

internal static class DialogTools
{
    public static string Input(IWin32Window owner, string message, string defaultValue, int delaySeconds,
        CancellationToken cancel, Point? screenLocation = null, bool onlyOk = false,
        DialogWindowOptions? options = null)
    {
        if (delaySeconds is < 0 or > 3600)
            throw new InvalidOperationException("INPUTBOX: таймаут должен быть от 0 до 3600 секунд (0 — без таймаута).");
        cancel.ThrowIfCancellationRequested();
        var window = options ?? default;
        var inputWidth = AutoWidth(message, 435, 32);
        var inputHeight = AutoHeight(message, inputWidth - 32, 205, 147);
        var size = window.ClientSize(inputWidth, inputHeight, 360, 185);
        using var form = MakeForm("Kliker Blocks — ввод", size.Width, size.Height);
        if (!string.IsNullOrWhiteSpace(window.Title)) form.Text = window.Title;
        if ((window.Location ?? screenLocation) is { } inputLocation)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = inputLocation;
            form.TopMost = true;
        }
        var label = new Label { Text = message, Left = 16, Top = 14, Width = size.Width - 32,
            Height = size.Height - 147,
            AutoEllipsis = true };
        var input = new TextBox { Text = defaultValue, Left = 16, Top = size.Height - 125, Width = size.Width - 32 };
        var ok = MakeButton("ОК", DialogResult.OK, size.Width - 197, size.Height - 79);
        var dismiss = MakeButton("Отмена", DialogResult.Cancel, size.Width - 106, size.Height - 79);
        form.Controls.AddRange(onlyOk ? [label, input, ok] : [label, input, ok, dismiss]);
        form.AcceptButton = ok;
        if (!onlyOk) form.CancelButton = dismiss;
        FocusWhenShown(form, input);
        var timedOut = false;
        using var timer = new System.Windows.Forms.Timer();
        if (delaySeconds > 0)
        {
            timer.Interval = checked(delaySeconds * 1000);
            timer.Tick += (_, _) => { timedOut = true; form.DialogResult = DialogResult.OK; form.Close(); };
            input.TextChanged += (_, _) => timer.Stop();
            form.Shown += (_, _) => timer.Start();
        }
        _ = form.Handle; // Закрытие по отмене должно работать и при гонке перед ShowDialog.
        using var registration = RegisterCancellation(form, cancel);
        cancel.ThrowIfCancellationRequested();
        var result = form.ShowDialog(owner);
        cancel.ThrowIfCancellationRequested();
        return result == DialogResult.OK ? timedOut ? defaultValue : input.Text : "";
    }

    public static int Choice(IWin32Window owner, string message, int buttonCode, int iconCode,
        CancellationToken cancel, Point? screenLocation = null, DialogWindowOptions? options = null)
    {
        if (buttonCode is < 0 or > 5 || iconCode is < 0 or > 4)
            throw new InvalidOperationException("DIALOGBOX: код кнопок 0–5, код иконки 0–4.");
        cancel.ThrowIfCancellationRequested();
        var window = options ?? default;
        var choiceWidth = AutoWidth(message, 470, iconCode == 0 ? 32 : 78);
        var choiceHeight = AutoHeight(message, choiceWidth - (iconCode == 0 ? 32 : 78), 190, 110);
        var size = window.ClientSize(choiceWidth, choiceHeight, 360, 180);
        using var form = MakeForm("Kliker Blocks — вопрос", size.Width, size.Height);
        if (!string.IsNullOrWhiteSpace(window.Title)) form.Text = window.Title;
        if ((window.Location ?? screenLocation) is { } choiceLocation)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = choiceLocation;
            form.TopMost = true;
        }
        var buttons = ChoiceDialogCatalog.ButtonsFor(buttonCode)
            .Select(text => (text, code: ChoiceDialogCatalog.ResultCode(text))).ToArray();
        var label = new Label { Text = message, Left = iconCode == 0 ? 16 : 62, Top = 17,
            Width = size.Width - (iconCode == 0 ? 32 : 78), Height = size.Height - 110, AutoEllipsis = true };
        form.Controls.Add(label);
        if (iconCode != 0)
        {
            var icon = iconCode switch
            {
                1 => SystemIcons.Error, 2 => SystemIcons.Question, 3 => SystemIcons.Warning,
                _ => SystemIcons.Information
            };
            var panel = new Panel { Left = 16, Top = 19, Width = 36, Height = 36 };
            panel.Paint += (_, e) => e.Graphics.DrawIcon(icon, 0, 0);
            form.Controls.Add(panel);
        }
        var left = size.Width - 16 - buttons.Length * 104;
        foreach (var (text, code) in buttons)
        {
            var button = MakeButton(text, (DialogResult)code, left, size.Height - 80);
            form.Controls.Add(button);
            if (form.AcceptButton is null) form.AcceptButton = button;
            if (code == 2) form.CancelButton = button;
            left += 104;
        }
        FocusWhenShown(form, form.AcceptButton as Control ?? label);
        _ = form.Handle;
        using var registration = RegisterCancellation(form, cancel);
        cancel.ThrowIfCancellationRequested();
        var result = form.ShowDialog(owner);
        cancel.ThrowIfCancellationRequested();
        return result == DialogResult.None ? 2 : (int)result;
    }

    public static int Radio(IWin32Window owner, string message, IReadOnlyList<string> options,
        CancellationToken cancel, Point? screenLocation = null, bool onlyOk = false,
        DialogWindowOptions? window = null)
    {
        if (options.Count is < 1 or > 100 || options.Any(option => string.IsNullOrWhiteSpace(option) || option.Length > 250))
            throw new InvalidOperationException("RADIOBOX: требуется от 1 до 100 непустых вариантов (до 250 символов каждый).");
        cancel.ThrowIfCancellationRequested();
        var settings = window ?? default;
        var radioWidth = Math.Max(AutoWidth(message, 465, 35),
            Math.Min(900, options.Max(option => MeasureSingleLine(option)) + 72));
        var questionHeight = Math.Max(58, TextRenderer.MeasureText(message, SystemFonts.MessageBoxFont,
            new Size(radioWidth - 35, 3000), TextFormatFlags.WordBreak).Height + 8);
        var extraHeader = questionHeight - 58;
        var radioHeight = Math.Max(410 + extraHeader,
            Math.Min(760, 169 + options.Count * 30 + extraHeader));
        var size = settings.ClientSize(radioWidth, radioHeight, 360, 260);
        using var form = MakeForm("Kliker Blocks — выбор", size.Width, size.Height);
        if (!string.IsNullOrWhiteSpace(settings.Title)) form.Text = settings.Title;
        if ((settings.Location ?? screenLocation) is { } radioLocation)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = radioLocation;
            form.TopMost = true;
        }
        var label = new Label { Text = message, Left = 16, Top = 14, Width = size.Width - 35,
            Height = Math.Min(questionHeight, size.Height - 180), AutoEllipsis = true };
        var listTop = label.Bottom + 6;
        var list = new Panel { Left = 16, Top = listTop, Width = size.Width - 35,
            Height = size.Height - listTop - 74, AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle };
        var radios = new List<RadioButton>();
        for (var i = 0; i < options.Count; i++)
        {
            var radio = new RadioButton { Text = options[i], Left = 8, Top = i * 30 + 5,
                Width = size.Width - 70, Height = 28, AutoEllipsis = true };
            list.Controls.Add(radio);
            radios.Add(radio);
        }
        var ok = MakeButton("ОК", DialogResult.OK, size.Width - 199, size.Height - 59);
        var dismiss = MakeButton("Отмена", DialogResult.Cancel, size.Width - 103, size.Height - 59);
        form.Controls.AddRange(onlyOk ? [label, list, ok] : [label, list, ok, dismiss]);
        form.AcceptButton = ok;
        if (!onlyOk) form.CancelButton = dismiss;
        FocusWhenShown(form, radios[0]);
        _ = form.Handle;
        using var registration = RegisterCancellation(form, cancel);
        cancel.ThrowIfCancellationRequested();
        var result = form.ShowDialog(owner);
        cancel.ThrowIfCancellationRequested();
        if (result != DialogResult.OK) return 0;
        var index = radios.FindIndex(radio => radio.Checked);
        return index < 0 ? 0 : index + 1;
    }

    public static void Acknowledge(IWin32Window owner, string message, CancellationToken cancel,
        Point? screenLocation = null)
    {
        cancel.ThrowIfCancellationRequested();
        using var form = MakeForm("Kliker Blocks — вопрос", 470, 190);
        if (screenLocation is { } location)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = location;
            form.TopMost = true;
        }
        var label = new Label { Text = message, Left = 16, Top = 18, Width = 438,
            Height = 105, AutoEllipsis = true };
        var ok = MakeButton("ОК", DialogResult.OK, 363, 137);
        form.Controls.AddRange([label, ok]);
        form.AcceptButton = ok;
        FocusWhenShown(form, ok);
        _ = form.Handle;
        using var registration = RegisterCancellation(form, cancel);
        cancel.ThrowIfCancellationRequested();
        if (form.ShowDialog(owner) != DialogResult.OK && !cancel.IsCancellationRequested)
            throw new OperationCanceledException(cancel);
        cancel.ThrowIfCancellationRequested();
    }

    private static Form MakeForm(string title, int width, int height) => new()
    {
        Text = UiLanguage.T(title), ClientSize = new Size(width, height), FormBorderStyle = FormBorderStyle.FixedDialog,
        StartPosition = FormStartPosition.CenterScreen, MinimizeBox = false, MaximizeBox = false,
        ShowInTaskbar = false, Font = new Font("Segoe UI", 9.5f)
    };

    private static int AutoWidth(string text, int baseline, int padding)
    {
        var longest = text.Replace("\r", "").Split('\n').Max(line =>
            Math.Min(100, line.Length));
        return Math.Clamp(Math.Max(baseline, longest * 7 + padding), baseline, 900);
    }

    private static int AutoHeight(string text, int textWidth, int baseline, int reserved)
    {
        var measure = TextRenderer.MeasureText(text, SystemFonts.MessageBoxFont,
            new Size(Math.Max(100, textWidth), 3000), TextFormatFlags.WordBreak);
        return Math.Clamp(Math.Max(baseline, measure.Height + reserved + 12), baseline, 900);
    }

    private static int MeasureSingleLine(string text) => TextRenderer.MeasureText(
        text, SystemFonts.MessageBoxFont, new Size(3000, 40), TextFormatFlags.SingleLine).Width;

    private static Button MakeButton(string text, DialogResult result, int left, int top) => new()
    {
        Text = UiLanguage.T(text), DialogResult = result, Left = left, Top = top, Width = 91, Height = 32
    };

    private static void FocusWhenShown(Form form, Control target)
    {
        form.Shown += (_, _) =>
        {
            void ActivatePrompt()
            {
                if (form.IsDisposed || !form.Visible) return;
                form.BringToFront();
                form.Activate();
                try { WindowTools.Activate(form.Handle); }
                catch (InvalidOperationException) { /* Windows may defer activation until the modal loop pumps. */ }
                target.Focus();
            }
            ActivatePrompt();
            form.BeginInvoke((Action)(() =>
            {
                if (!WindowTools.IsForegroundHandle(form.Handle)) ActivatePrompt();
            }));
        };
    }

    private static CancellationTokenRegistration RegisterCancellation(Form form, CancellationToken cancel) =>
        cancel.Register(() =>
        {
            try
            {
                if (form.IsHandleCreated && !form.IsDisposed)
                    form.BeginInvoke(() => form.Close());
            }
            catch (InvalidOperationException) { }
        });
}
