using System.Drawing;

namespace KlikerBlocks;

internal sealed class CodeImportForm : Form
{
    private readonly RichTextBox _code = new()
    {
        Dock = DockStyle.Fill, Font = new Font("Consolas", 11f), AcceptsTab = true,
        WordWrap = false, DetectUrls = false
    };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Point _insertionPoint;
    private readonly string _includeDirectory;

    public CodeImportResult? Result { get; private set; }

    public CodeImportForm(Point insertionPoint, string includeDirectory, string? initialCode = null)
    {
        _insertionPoint = insertionPoint;
        _includeDirectory = includeDirectory;
        Text = "KLIKER — импорт кода как блоков";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 950;
        Height = 720;
        MinimumSize = new Size(720, 500);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Этот исторический преобразователь команд больше не доступен в интерфейсе Kliker Blocks.",
            ForeColor = Color.FromArgb(55, 67, 84)
        }, 0, 0);
        root.Controls.Add(_code, 0, 1);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        root.Controls.Add(footer, 0, 2);
        footer.Controls.Add(_status, 0, 0);
        var check = new Button { Text = "Проверить", Dock = DockStyle.Fill };
        check.Click += (_, _) => ValidateCode(false);
        footer.Controls.Add(check, 1, 0);
        var insert = new Button { Text = "Вставить блоки", Dock = DockStyle.Fill };
        insert.Click += (_, _) => ValidateCode(true);
        footer.Controls.Add(insert, 2, 0);
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };
        footer.Controls.Add(cancel, 3, 0);
        AcceptButton = insert;
        CancelButton = cancel;
        _code.Text = initialCode ?? "";
        Shown += (_, _) => _code.Focus();
    }

    private void ValidateCode(bool insert)
    {
        try
        {
            var result = CodeImporter.Convert(_code.Text, _insertionPoint, _includeDirectory);
            _status.ForeColor = Color.FromArgb(34, 116, 70);
            _status.Text = $"Код проверен: {result.Nodes.Count} блоков, {result.Commands} строк.";
            if (!insert) return;
            Result = result;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (CodeImportException error)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = error.Message;
            var lines = _code.Lines;
            if (error.LineNumber >= 1 && error.LineNumber <= lines.Length)
            {
                var offset = _code.GetFirstCharIndexFromLine(error.LineNumber - 1);
                if (offset >= 0) _code.Select(offset, lines[error.LineNumber - 1].Length);
            }
            _code.Focus();
        }
        catch (Exception error)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Не удалось преобразовать код: " + error.Message;
        }
    }
}
