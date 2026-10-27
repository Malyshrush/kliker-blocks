using System.Drawing;

namespace KlikerBlocks;

internal sealed class SupportForm : Form
{
    internal static readonly (string Label, string File)[] PaymentMethods =
    [
        ("USDT · TON", "usdt-ton.png"),
        ("USDT · TRC20", "usdt-trc20.png"),
        ("USDT · ERC20", "usdt-erc20.png"),
        ("USDT · Solana", "usdt-sol.png"),
        ("Сбербанк", "sberbank.jpg")
    ];

    public SupportForm()
    {
        Text = UiLanguage.T("Поддержать проект Kliker Blocks");
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 770;
        Height = 820;
        MinimumSize = new Size(620, 700);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(247, 250, 252);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 69));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 61, 79), Text = UiLanguage.T("Поддержать Kliker Blocks") +
                Environment.NewLine + UiLanguage.T("Добровольная поддержка разработки"),
        }, 0, 0);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2 };
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(cards, 0, 1);
        for (var i = 0; i < PaymentMethods.Length; i++)
        {
            var (label, file) = PaymentMethods[i];
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2,
                BackColor = Color.White, Margin = new Padding(7), Padding = new Padding(10) };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(new Label { Dock = DockStyle.Fill, Text = UiLanguage.T(label),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            var imagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Support", file);
            if (File.Exists(imagePath))
            {
                var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
                    Image = Image.FromFile(imagePath), AccessibleName = UiLanguage.T(label) + " QR" };
                card.Controls.Add(picture, 0, 1);
                FormClosed += (_, _) => picture.Image?.Dispose();
            }
            else card.Controls.Add(new Label { Dock = DockStyle.Fill,
                Text = UiLanguage.T("QR-код недоступен в этой сборке."),
                TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
            cards.Controls.Add(card, i % 2, i / 2);
            if (i == 4) cards.SetColumnSpan(card, 2);
        }
        root.Controls.Add(new Label { Dock = DockStyle.Fill,
            Text = UiLanguage.T("Для USDT выберите указанную сеть. Перед любым переводом проверьте данные получателя в кошельке или банке. QR-коды предоставлены автором проекта."),
            ForeColor = Color.FromArgb(88, 105, 120), AutoSize = false }, 0, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false };
        var close = new Button { Text = UiLanguage.T("Закрыть"), Width = 105, Height = 34 };
        close.Click += (_, _) => Close();
        var website = new Button { Text = UiLanguage.T("Сайт Kliker Blocks"), Width = 190, Height = 34 };
        website.Click += (_, _) => WebsiteLinks.Open(this);
        footer.Controls.Add(close);
        footer.Controls.Add(website);
        root.Controls.Add(footer, 0, 3);
    }
}
