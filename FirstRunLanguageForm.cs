namespace KlikerBlocks;

internal sealed class FirstRunLanguageForm : Form
{
    public string? SelectedLanguageCode { get; private set; }

    public FirstRunLanguageForm()
    {
        Text = "Kliker Blocks — Choose language / Выберите язык";
        ClientSize = new Size(700, 360);
        MinimumSize = new Size(680, 350);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(246, 249, 253);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 18),
            RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill,
            Text = "Choose the interface language\nВыберите язык интерфейса",
            ForeColor = Color.FromArgb(28, 48, 78),
            Font = new Font("Segoe UI", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill,
            Text = "The demo will use the same language.\nДемонстрация будет на выбранном языке.",
            ForeColor = Color.FromArgb(68, 82, 101), TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        var choices = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(4, 13, 4, 13) };
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var english = new Button { Text = "English", Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 13, FontStyle.Bold), Margin = new Padding(6),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(225, 237, 254),
            ForeColor = Color.FromArgb(21, 65, 123) };
        var russian = new Button { Text = "Русский", Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 13, FontStyle.Bold), Margin = new Padding(6),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White,
            ForeColor = Color.FromArgb(21, 65, 123) };
        english.FlatAppearance.BorderColor = Color.FromArgb(117, 159, 215);
        russian.FlatAppearance.BorderColor = Color.FromArgb(170, 189, 214);
        english.Click += (_, _) => Choose("en");
        russian.Click += (_, _) => Choose("ru");
        choices.Controls.Add(english, 0, 0);
        choices.Controls.Add(russian, 1, 0);
        layout.Controls.Add(choices, 0, 2);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill,
            Text = "You can change this later in Languages.\nПозже язык можно сменить в меню «Языки».",
            ForeColor = Color.FromArgb(86, 101, 120),
            Font = new Font("Segoe UI", 9.5f), TextAlign = ContentAlignment.MiddleCenter }, 0, 3);
    }

    internal void Choose(string code)
    {
        if (code is not ("en" or "ru")) throw new ArgumentOutOfRangeException(nameof(code));
        SelectedLanguageCode = code;
        DialogResult = DialogResult.OK;
        Close();
    }
}
