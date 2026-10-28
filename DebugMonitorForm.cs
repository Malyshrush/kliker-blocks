namespace KlikerBlocks;

internal sealed class DebugMonitorForm : Form
{
    private readonly Label _current = new() { Dock = DockStyle.Top, Height = 36 };
    private readonly ListView _variables = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        GridLines = true, HideSelection = false
    };

    internal DebugMonitorForm(Action step, Action resume)
    {
        Text = UiLanguage.T("Отладка сценария");
        Width = 520;
        Height = 430;
        MinimumSize = new Size(370, 280);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent;
        _variables.Columns.Add(UiLanguage.T("Переменная"), 170);
        _variables.Columns.Add(UiLanguage.T("Значение"), 290);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54,
            FlowDirection = FlowDirection.RightToLeft };
        var resumeButton = new Button { Text = UiLanguage.T("Продолжить"), Width = 130, Height = 36 };
        var stepButton = new Button { Text = UiLanguage.T("Следующий блок"), Width = 150, Height = 36 };
        resumeButton.Click += (_, _) => resume();
        stepButton.Click += (_, _) => step();
        footer.Controls.Add(resumeButton);
        footer.Controls.Add(stepButton);
        Controls.Add(_variables);
        Controls.Add(_current);
        Controls.Add(footer);
    }

    internal void UpdateState(string title, IReadOnlyDictionary<string, string> variables)
    {
        _current.Text = "  " + title;
        _variables.BeginUpdate();
        try
        {
            _variables.Items.Clear();
            foreach (var (name, value) in variables.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                var item = new ListViewItem(name);
                item.SubItems.Add(value.Length > 500 ? value[..500] + "…" : value);
                _variables.Items.Add(item);
            }
        }
        finally { _variables.EndUpdate(); }
    }
}
