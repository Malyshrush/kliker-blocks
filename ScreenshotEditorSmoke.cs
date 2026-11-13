using System.Drawing.Imaging;
using System.Reflection;

namespace KlikerBlocks;

internal static class ScreenshotEditorSmoke
{
    internal static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Screenshot editor test failed: " + message);
        }
        var desktop = new Rectangle(-200, -100, 400, 300);
        using var background = new Bitmap(400, 300, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(background)) graphics.Clear(Color.White);
        var selected = new Rectangle(-180, -80, 120, 80);
        using var editor = new ScreenshotEditorForm(background, desktop, selected, "Прямоугольник");
        var tools = editor.Controls.OfType<FlowLayoutPanel>().Single();
        var linesMenu = tools.Controls.OfType<Button>().Single(button => button.AccessibleName == "Линии");
        var arrowsMenu = tools.Controls.OfType<Button>().Single(button => button.AccessibleName == "Стрелки");
        var shapesMenu = tools.Controls.OfType<Button>().Single(button => button.AccessibleName == "Фигуры");
        Check(linesMenu.ContextMenuStrip?.Items.Count == 3 && arrowsMenu.ContextMenuStrip?.Items.Count == 4 &&
              shapesMenu.ContextMenuStrip?.Items.Count == 6, "tools are grouped into compact menus");
        shapesMenu.ContextMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text?.Contains("Звезда") == true).PerformClick();
        Check(shapesMenu.AccessibleName == "Фигуры: Звезда" && shapesMenu.BackColor != Color.WhiteSmoke,
            "chosen shape updates the group button and highlight");
        typeof(ScreenshotEditorForm).GetField("_tool", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(editor, ScreenshotEditorForm.DrawingTool.Selection);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Mouse(string name, int x, int y)
        {
            var method = typeof(ScreenshotEditorForm).GetMethod(name, flags)!;
            method.Invoke(editor, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        }
        Mouse("OnMouseDown", 140, 60);
        Mouse("OnMouseMove", 180, 60);
        Mouse("OnMouseUp", 180, 60);
        Check(editor.SelectedArea == new Rectangle(-180, -80, 160, 80), "resize right edge on negative-coordinate monitor");
        typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, ScreenshotEditorForm.DrawingTool.Line);
        Mouse("OnMouseDown", 30, 40);
        Mouse("OnMouseMove", 100, 40);
        Mouse("OnMouseUp", 100, 40);
        Check(editor.Marks.Count == 1, "line created");
        using (var image = editor.RenderImage())
        {
            Check(image.Size == new Size(160, 80), "render follows adjusted bounds");
            var red = image.GetPixel(40, 20);
            Check(red.R > 180 && red.G < 100 && red.B < 100, "line appears in saved pixels");
            var white = image.GetPixel(5, 5);
            Check(white.R > 240 && white.G > 240 && white.B > 240, "background preserved");
        }
        var key = typeof(ScreenshotEditorForm).GetMethod("OnKeyDown", flags)!;
        key.Invoke(editor, [new KeyEventArgs(Keys.Control | Keys.Z)]);
        Check(editor.Marks.Count == 0, "undo removes the annotation");
        foreach (var tool in new[] { ScreenshotEditorForm.DrawingTool.Pen, ScreenshotEditorForm.DrawingTool.Arrow,
                     ScreenshotEditorForm.DrawingTool.Rectangle, ScreenshotEditorForm.DrawingTool.Ellipse })
        {
            typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, tool);
            Mouse("OnMouseDown", 30, 30);
            Mouse("OnMouseMove", 90, 70);
            Mouse("OnMouseUp", 90, 70);
        }
        Check(editor.Marks.Count == 4, "pen, arrow, rectangle and ellipse are retained");
        using (var marked = editor.RenderImage())
            Check(marked.Width == 160 && marked.Height == 80, "all drawing tools render into the selection");

        Check(ScreenshotEditorForm.ConstrainShape(new Point(100, 100), new Point(180, 130), true) == new Point(130, 130) &&
              ScreenshotEditorForm.ConstrainShape(new Point(100, 100), new Point(20, 70), true) == new Point(70, 70) &&
              ScreenshotEditorForm.ConstrainShape(new Point(100, 100), new Point(180, 130), false) == new Point(180, 130),
              "Shift constrains shapes in both drag directions");
        foreach (var tool in new[] { ScreenshotEditorForm.DrawingTool.Star, ScreenshotEditorForm.DrawingTool.Heart,
                     ScreenshotEditorForm.DrawingTool.Triangle, ScreenshotEditorForm.DrawingTool.Diamond })
        {
            typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, tool);
            Mouse("OnMouseDown", 30, 30);
            Mouse("OnMouseMove", 90, 70);
            Mouse("OnMouseUp", 90, 70);
        }
        Check(editor.Marks.Count == 8, "additional shapes are retained");
        using (var marked = editor.RenderImage())
            Check(marked.Width == 160 && marked.Height == 80, "additional shapes render");

        typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, ScreenshotEditorForm.DrawingTool.Curve);
        Mouse("OnMouseDown", 30, 30);
        Mouse("OnMouseDown", 60, 50);
        Mouse("OnMouseDown", 90, 30);
        key.Invoke(editor, [new KeyEventArgs(Keys.Enter)]);
        Check(editor.Marks.Last().Tool == ScreenshotEditorForm.DrawingTool.Curve && editor.Marks.Last().Points.Length == 3,
            "a curve keeps multiple bend points when completed with Enter");
        typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, ScreenshotEditorForm.DrawingTool.Polyline);
        Mouse("OnMouseDown", 30, 30);
        Mouse("OnMouseDown", 60, 50);
        typeof(ScreenshotEditorForm).GetMethod("OnMouseDown", flags)!.Invoke(editor,
            [new MouseEventArgs(MouseButtons.Left, 2, 90, 30, 0)]);
        Check(editor.Marks.Last().Tool == ScreenshotEditorForm.DrawingTool.Polyline && editor.Marks.Last().Points.Length == 3,
            "a polyline keeps its bend points when completed with a double click");
        foreach (var arrow in new[] { ScreenshotEditorForm.DrawingTool.DoubleArrow, ScreenshotEditorForm.DrawingTool.PolylineArrow,
                     ScreenshotEditorForm.DrawingTool.CurveArrow })
        {
            typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, arrow);
            Mouse("OnMouseDown", 30, 30);
            if (arrow == ScreenshotEditorForm.DrawingTool.DoubleArrow)
            {
                Mouse("OnMouseMove", 90, 70);
                Mouse("OnMouseUp", 90, 70);
            }
            else
            {
                Mouse("OnMouseDown", 60, 50);
                Mouse("OnMouseDown", 90, 30);
                key.Invoke(editor, [new KeyEventArgs(Keys.Enter)]);
            }
            Check(editor.Marks.Last().Tool == arrow, "arrow variant is retained");
        }
        using (var marked = editor.RenderImage())
            Check(marked.Width == 160, "curves and arrow variants render into PNG");

        // Exercise the real mouse handlers, including releasing Shift just before the mouse button.
        bool shift = false;
        editor.ShiftPressed = () => shift;
        foreach (var end in new[] { new Point(100, 60), new Point(30, 60), new Point(100, 25), new Point(30, 25) })
        {
            typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, ScreenshotEditorForm.DrawingTool.Rectangle);
            shift = true;
            Mouse("OnMouseDown", 65, 45);
            Mouse("OnMouseMove", end.X, end.Y);
            shift = false;
            Mouse("OnMouseUp", end.X, end.Y);
            var points = editor.Marks.Last().Points;
            Check(Math.Abs(points[1].X - points[0].X) == Math.Abs(points[1].Y - points[0].Y), "Shift rectangle stays square after mouse release");
        }
        foreach (var path in new[] { ScreenshotEditorForm.DrawingTool.Polyline, ScreenshotEditorForm.DrawingTool.Curve,
                     ScreenshotEditorForm.DrawingTool.PolylineArrow, ScreenshotEditorForm.DrawingTool.CurveArrow })
        {
            typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(editor, path);
            int before = editor.Marks.Count;
            shift = false;
            Mouse("OnMouseDown", 30, 30);
            Mouse("OnMouseDown", 60, 50);
            shift = true;
            Mouse("OnMouseDown", 90, 30);
            shift = false;
            Check(editor.Marks.Count == before + 1 && editor.Marks.Last().Tool == path && editor.Marks.Last().Points.Length == 3,
                "Shift+click finishes each path and arrow variant");
            Mouse("OnMouseDown", 40, 35);
            key.Invoke(editor, [new KeyEventArgs(Keys.Escape)]);
            Check(editor.Marks.Count == before + 1, "a finished path is independent of the next path");
        }
        var hintMenu = linesMenu.ContextMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text?.Contains("Ломаная") == true);
        hintMenu.PerformClick();
        Check(tools.Controls.OfType<Label>().Any(label => label.Text.Contains("Shift+щелчок")), "Russian path completion hint is present");
        using var textEditor = new ScreenshotEditorForm(background, desktop, selected, "Прямоугольник");
        typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(textEditor, ScreenshotEditorForm.DrawingTool.Text);
        var mouseDown = typeof(ScreenshotEditorForm).GetMethod("OnMouseDown", flags)!;
        mouseDown.Invoke(textEditor, [new MouseEventArgs(MouseButtons.Left, 1, 30, 35, 0)]);
        var input = textEditor.Controls.OfType<TextBox>().Single();
        input.Text = "Тест";
        var processKey = typeof(ScreenshotEditorForm).GetMethod("ProcessCmdKey", flags)!;
        object[] enterArgs = [new Message(), Keys.Enter];
        Check((bool)processKey.Invoke(textEditor, enterArgs)!, "Enter is handled while inline text is active");
        Check(!textEditor.Controls.OfType<TextBox>().Any() && textEditor.Marks.Single().Text == "Тест" &&
              textEditor.Marks.Single().Points[0] == new Point(30, 35), "Enter applies inline text at the clicked point");
        mouseDown.Invoke(textEditor, [new MouseEventArgs(MouseButtons.Left, 1, 40, 50, 0)]);
        input = textEditor.Controls.OfType<TextBox>().Single();
        input.Text = "Ещё";
        typeof(Control).GetMethod("OnLostFocus", flags)!.Invoke(input, [EventArgs.Empty]);
        Check(textEditor.Marks.Count == 2 && textEditor.Marks[1].Text == "Ещё", "losing focus applies inline text");
        mouseDown.Invoke(textEditor, [new MouseEventArgs(MouseButtons.Left, 1, 50, 60, 0)]);
        textEditor.Controls.OfType<TextBox>().Single().Text = "отмена";
        key.Invoke(textEditor, [new KeyEventArgs(Keys.Escape)]);
        Check(textEditor.Marks.Count == 2 && textEditor.DialogResult != DialogResult.Cancel,
            "Escape cancels inline input without cancelling the screenshot");
        using (var marked = textEditor.RenderImage())
        {
            bool redText = false;
            for (int y = 15; y < 50 && !redText; y++)
                for (int x = 10; x < 100; x++)
                {
                    var pixel = marked.GetPixel(x, y);
                    if (pixel.R > 150 && pixel.G < 150 && pixel.B < 150) { redText = true; break; }
                }
            Check(redText, "committed inline text is rendered into the saved image");
        }
        UiLanguage.Configure("en");
        try
        {
            using var english = new ScreenshotEditorForm(background, desktop, selected, "Прямоугольник");
            var bar = english.Controls.OfType<FlowLayoutPanel>().Single();
            Check(bar.Controls.OfType<Button>().Any(button => button.AccessibleName == "Text") &&
                  bar.Controls.OfType<Button>().Any(button => button.AccessibleName == "Save screenshot") &&
                  bar.Controls.OfType<Button>().Any(button => button.AccessibleName == "Lines") &&
                  bar.Controls.OfType<Button>().Any(button => button.AccessibleName == "Arrows") &&
                  bar.Controls.OfType<Button>().Any(button => button.AccessibleName == "Shapes"),
                "English screenshot editor tools are localized");
            typeof(ScreenshotEditorForm).GetField("_tool", flags)!.SetValue(english, ScreenshotEditorForm.DrawingTool.Text);
            mouseDown.Invoke(english, [new MouseEventArgs(MouseButtons.Left, 1, 30, 35, 0)]);
            Check(english.Controls.OfType<TextBox>().Single().AccessibleName!.StartsWith("Screenshot text"),
                "English inline input is localized");
        }
        finally { UiLanguage.Configure("ru"); }

        using var circle = new ScreenshotEditorForm(background, desktop, new Rectangle(-180, -80, 80, 80), "Круг");
        using (var image = circle.RenderImage())
            Check(image.GetPixel(0, 0).A == 0 && image.GetPixel(40, 40).A > 0, "circle alpha retained");

        var folder = Path.Combine(Path.GetTempPath(), "kliker-capture-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new AppSettings { ScreenshotFolder = folder, ScreenshotPrefix = "test" };
            using var image = editor.RenderImage();
            var first = CaptureService.SaveEdited(editor.SelectedArea, "Прямоугольник", "example", settings, folder, image);
            var second = CaptureService.SaveEdited(editor.SelectedArea, "Прямоугольник", "example", settings, folder, image);
            Check(first.Path != second.Path && File.Exists(first.Path) && File.Exists(second.Path), "saving avoids overwrites");
            using var opened = new Bitmap(first.Path);
            Check(opened.Size == editor.SelectedArea.Size, "saved PNG dimensions");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("Screenshot editor tests passed.");
    }
}
