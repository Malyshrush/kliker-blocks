using System.Drawing;

namespace KlikerBlocks;

internal sealed class ScriptNameForm : Form
{
    private readonly TextBox _name;

    public string ScriptName => _name.Text.Trim();

    public ScriptNameForm(string currentName)
    {
        Text = "Имя сценария";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 490;
        Height = 175;
        MinimumSize = new Size(400, 175);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        var label = new Label { Text = "Имя в заголовке окна (пусто — без имени):", Left = 16, Top = 16, Width = 440 };
        _name = new TextBox { Text = currentName, Left = 16, Top = 44, Width = 440, MaxLength = 100 };
        var save = new Button { Text = "Применить", Left = 342, Top = 86, Width = 114 };
        var cancel = new Button { Text = "Отмена", Left = 230, Top = 86, Width = 104, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            if (ScriptName.Length > 0 && !ScriptNameTools.IsValid(ScriptName))
            {
                MessageBox.Show(this, "Имя должно содержать от 1 до 100 видимых символов.", "Имя сценария");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.AddRange([label, _name, cancel, save]);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
