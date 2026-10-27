namespace KlikerBlocks;

internal sealed class ShowcaseQuestionForm : Form
{
    private readonly TextBox _answer = new() { Dock = DockStyle.Fill };
    private readonly TaskCompletionSource<string> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<string> Answer => _result.Task;
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    public ShowcaseQuestionForm()
    {
        Text = UiLanguage.T("Kliker Blocks — вопрос к вам");
        Width = 430; Height = 260; MinimumSize = new Size(390, 250);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);
        root.Controls.Add(new Label { Text = UiLanguage.T("Что нарисовать в Paint? Напишите «звезда», «сердце» или «дом»."),
            Dock = DockStyle.Fill, AutoEllipsis = true }, 0, 0);
        _answer.TextChanged += (_, _) => LastActivityUtc = DateTime.UtcNow;
        root.Controls.Add(_answer, 0, 1);
        var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        foreach (var (caption, value) in new[]
            { ("Звезда", "star"), ("Сердце", "heart"), ("Дом", "house") })
        {
            var button = new Button { Text = UiLanguage.T(caption), Width = 103, Height = 32 };
            button.Click += (_, _) => Complete(value);
            choices.Controls.Add(button);
        }
        root.Controls.Add(choices, 0, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var submit = new Button { Text = UiLanguage.T("Ответить"), Width = 105, Height = 32 };
        submit.Click += (_, _) => Complete(ParseChoice(_answer.Text));
        var skip = new Button { Text = UiLanguage.T("Пропустить"), Width = 105, Height = 32 };
        skip.Click += (_, _) => Complete("");
        footer.Controls.Add(submit); footer.Controls.Add(skip);
        root.Controls.Add(footer, 0, 3);
        AcceptButton = submit;
        CancelButton = skip;
        Shown += (_, _) => _answer.Focus();
        FormClosed += (_, _) => _result.TrySetResult("");
    }

    public static string ParseChoice(string value)
    {
        var answer = value.Trim().ToLowerInvariant();
        if (answer.Contains("heart") || answer.Contains("серд") || answer.Contains("coeur") ||
            answer.Contains("cœur") || answer.Contains("coraz") || answer.Contains("心")) return "heart";
        if (answer.Contains("house") || answer.Contains("дом") || answer.Contains("maison") ||
            answer.Contains("casa") || answer.Contains("房")) return "house";
        return answer.Length == 0 ? "" : "star";
    }

    private void Complete(string value)
    {
        LastActivityUtc = DateTime.UtcNow;
        _result.TrySetResult(value);
        Close();
    }
}
