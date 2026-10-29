using System.Diagnostics;
using System.Text;

namespace KlikerBlocks;

internal static class GraphShareDraft
{
    internal const string NewDiscussion = "https://github.com/Malyshrush/kliker-blocks-site/discussions/new";
    internal const int MaximumPrefilledUrlLength = 7500;

    internal static string Markdown(string description, string code)
    {
        var longest = System.Text.RegularExpressions.Regex.Matches(code, "`+")
            .Select(match => match.Length).DefaultIfEmpty(0).Max();
        var fence = new string('`', Math.Max(3, longest + 1));
        return description.Trim() + "\n\n### Kliker Blocks graph\n\n" + fence +
            "kliker\n" + code.TrimEnd() + "\n" + fence + "\n";
    }

    internal static (string Url, bool Complete) Build(string title, string description, string code)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Graph title is required.");
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Graph code is empty.");
        var body = Markdown(description, code);
        var prefix = NewDiscussion + "?category=" + Uri.EscapeDataString("Графы пользователей") +
            "&title=" + Uri.EscapeDataString(title.Trim());
        var complete = prefix + "&body=" + Uri.EscapeDataString(body);
        if (complete.Length <= MaximumPrefilledUrlLength) return (complete, true);
        return (prefix, false);
    }
}

internal sealed class ShareGraphForm : Form
{
    private readonly TextBox _title = new() { Dock = DockStyle.Fill, MaxLength = 120 };
    private readonly TextBox _description = new() { Dock = DockStyle.Fill, Multiline = true,
        ScrollBars = ScrollBars.Vertical, Height = 68 };
    private readonly TextBox _code = new() { Dock = DockStyle.Fill, Multiline = true,
        ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
        Font = new Font("Consolas", 9f) };
    private readonly string _graphCode;

    internal ShareGraphForm(string projectName, string graphCode)
    {
        _graphCode = graphCode;
        Text = UiLanguage.T("Поделиться графом Kliker Blocks");
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(780, 640);
        MinimumSize = new Size(580, 450);
        StartPosition = FormStartPosition.CenterParent;
        _title.Text = string.IsNullOrWhiteSpace(projectName) ? UiLanguage.T("Мой граф") : projectName;
        _code.Text = graphCode;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18),
            ColumnCount = 1, RowCount = 7 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(root);
        root.Controls.Add(new Label { Dock = DockStyle.Fill,
            Text = UiLanguage.T("Проверьте код перед публикацией: он может содержать ваши пути, тексты и другие личные данные. Публикацию подтвердите в браузере GitHub."),
            ForeColor = Color.FromArgb(88, 62, 35), AutoEllipsis = true }, 0, 0);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = UiLanguage.T("Название графа") }, 0, 1);
        root.Controls.Add(_title, 0, 2);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = UiLanguage.T("Описание для сообщества") }, 0, 3);
        root.Controls.Add(_description, 0, 4);
        root.Controls.Add(_code, 0, 5);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = UiLanguage.T("Отмена"), Width = 100, Height = 34 };
        cancel.Click += (_, _) => Close();
        var open = new Button { Text = UiLanguage.T("Открыть публикацию в GitHub"), Width = 225, Height = 34 };
        open.Click += (_, _) => OpenDraft();
        var copy = new Button { Text = UiLanguage.T("Копировать код графа"), Width = 165, Height = 34 };
        copy.Click += (_, _) => CopyCode();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(open);
        buttons.Controls.Add(copy);
        root.Controls.Add(buttons, 0, 6);
        AcceptButton = open;
        CancelButton = cancel;
    }

    private void CopyCode()
    {
        try { Clipboard.SetText(_graphCode); }
        catch (Exception error) { MessageBox.Show(this, error.Message, UiLanguage.T("Буфер обмена")); }
    }

    private void OpenDraft()
    {
        try
        {
            var draft = GraphShareDraft.Build(_title.Text, _description.Text, _graphCode);
            if (!draft.Complete)
            {
                Clipboard.SetText(GraphShareDraft.Markdown(_description.Text, _graphCode));
                MessageBox.Show(this, UiLanguage.T("Граф слишком велик для ссылки. Описание и код скопированы в буфер: вставьте их в текст обсуждения. Выберите категорию «Графы пользователей» перед публикацией."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Process.Start(new ProcessStartInfo(draft.Url) { UseShellExecute = true });
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
