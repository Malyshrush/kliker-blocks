using System.Reflection;

namespace KlikerBlocks;

internal static class MainWindowLayoutSmoke
{
    internal static void Run()
    {
        static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control control in root.Controls)
            {
                yield return control;
                foreach (var child in Descendants(control)) yield return child;
            }
        }
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("Main window layout: " + message);
        }
        static bool clientContains(Form form, Control control) =>
            form.RectangleToScreen(form.ClientRectangle).Contains(control.RectangleToScreen(control.ClientRectangle));
        foreach (string language in new[] { "ru", "en" })
        {
            UiLanguage.Configure(language);
            using var form = new MainForm(uiPreview: true) { StartPosition = FormStartPosition.Manual,
                Location = new Point(-10000, -10000), ShowInTaskbar = false };
            form.Show();
            Application.DoEvents();
            var children = Descendants(form).ToArray();
            var propertyTitle = (Control)typeof(MainForm).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var logButton = children.OfType<Button>().Single(b => b.AccessibleName == (language == "ru"
                ? "Открыть журнал в отдельном окне" : "Open journal in a separate window"));
            var graph = (GraphCanvas)typeof(MainForm).GetField("_graph", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var fields = (FlowLayoutPanel)typeof(MainForm).GetField("_fields", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            graph.Select(graph.Project.Nodes.Last());
            Application.DoEvents();
            Check(fields.Controls.Count > 0, "selected block properties are populated");
            var menu = form.MainMenuStrip!;
            Check(menu.Items.Count == 8 && menu.CanOverflow, "compact menu with overflow " + language);
            var tools = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == (language == "ru" ? "Инструменты" : "Tools"));
            var settings = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == (language == "ru" ? "Настройки" : "Settings"));
            Check(tools.DropDownItems.Count >= 2 && settings.DropDownItems.Count >= 2, "grouped commands available");
            foreach (int width in new[] { 1500, 1100, 1000, 1300 })
            {
                form.Width = width;
                Application.DoEvents();
                var client = form.RectangleToScreen(form.ClientRectangle);
                var properties = propertyTitle.RectangleToScreen(propertyTitle.ClientRectangle);
                var log = logButton.RectangleToScreen(logButton.ClientRectangle);
                Check(properties.Width >= 200 && client.Contains(properties), $"properties visible at {width}: {properties.Width}");
                Check(log.Width >= 20 && client.Contains(log), $"journal button visible at {width}");
                Check(form.ClientSize.Width == form.Controls.OfType<TableLayoutPanel>().First().Width,
                    $"root fits the window at {width}");
            }
            var paletteToggle = children.OfType<ToolStrip>().SelectMany(t => t.Items.OfType<ToolStripButton>())
                .Single(b => b.Text?.Contains(language == "ru" ? "Скрыть блоки" : "Hide blocks") == true);
            paletteToggle.PerformClick();
            Application.DoEvents();
            Check(clientContains(form, propertyTitle), "properties visible with palette collapsed");
            paletteToggle.PerformClick();
            Application.DoEvents();
            Check(clientContains(form, propertyTitle), "properties visible after palette reopens");
            form.Close();
        }
        UiLanguage.Configure("ru");
        Console.WriteLine("Main window layout tests passed (RU/EN).");
    }
}
