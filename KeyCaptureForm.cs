namespace KlikerBlocks;

internal sealed class KeyCaptureForm : Form
{
    private readonly bool _allowMouse;
    public int? CapturedCode { get; private set; }

    public KeyCaptureForm(bool allowMouse)
    {
        _allowMouse = allowMouse;
        Text = "Назначить клавишу";
        Width = 420;
        Height = 205;
        MinimumSize = new Size(360, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        var prompt = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = allowMouse
                ? "Нажмите клавишу или кнопку мыши\nв этом окне (ЛКМ, ПКМ, СКМ)."
                : "Нажмите нужную клавишу на клавиатуре.\nЕё код будет подставлен в поле блока."
        };
        var cancel = new Button { Text = "Отмена", Dock = DockStyle.Bottom, Height = 38, TabStop = false,
            DialogResult = DialogResult.Cancel };
        Controls.Add(prompt);
        Controls.Add(cancel);
        if (allowMouse)
        {
            prompt.MouseDown += CaptureMouse;
            MouseDown += CaptureMouse;
        }
        KeyDown += (_, e) =>
        {
            CaptureKey((int)e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        };
        Shown += (_, _) => Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (CaptureKey((int)(keyData & Keys.KeyCode))) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (CaptureKey((int)(keyData & Keys.KeyCode))) return true;
        return base.ProcessDialogKey(keyData);
    }

    private bool CaptureKey(int code)
    {
        if (code is < 1 or > 255) return false;
        CapturedCode = code;
        DialogResult = DialogResult.OK;
        Close();
        return true;
    }

    private void CaptureMouse(object? sender, MouseEventArgs e)
    {
        var code = e.Button switch
        {
            MouseButtons.Left => 1, MouseButtons.Right => 2, MouseButtons.Middle => 4,
            MouseButtons.XButton1 => 5, MouseButtons.XButton2 => 6, _ => 0
        };
        CaptureKey(code);
    }
}
