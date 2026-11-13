using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KlikerBlocks;

internal sealed class ScreenshotEditorForm : Form
{
    internal enum DrawingTool { Selection, Pen, Line, Arrow, DoubleArrow, PolylineArrow, CurveArrow, Rectangle, Ellipse, Text, Star, Heart, Triangle, Diamond, Polyline, Curve }
    internal sealed record Mark(DrawingTool Tool, Color Color, float Width, Point[] Points, string Text = "");
    private enum DragMode { None, Move, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight, Draw }

    private static string L(string ru, string en) => UiLanguage.Code == "ru" ? ru : en;
    private readonly Bitmap _background;
    private readonly Rectangle _desktop;
    private readonly string _shape;
    private readonly List<Mark> _marks = [];
    private readonly List<Point> _drawing = [];
    private readonly List<Point> _pathPoints = [];
    private readonly Dictionary<DrawingTool, Button> _toolButtons = [];
    private readonly List<ContextMenuStrip> _toolMenus = [];
    private readonly FlowLayoutPanel _toolbar;
    private readonly ToolTip _tips = new();
    private readonly Label _pathHint = new() { AutoSize = true, ForeColor = Color.White, Margin = new Padding(4, 5, 4, 3) };
    internal Func<bool> ShiftPressed = () => (ModifierKeys & Keys.Shift) != 0 || NativeMouse.IsKeyDown(0x10);
    private bool _shapeConstrained;
    private readonly NumericUpDown _width = new() { Minimum = 1, Maximum = 30, Value = 3, Width = 48 };
    private DrawingTool _tool;
    private Color _color = Color.Red;
    private TextBox? _inlineText;
    private Point _textOrigin;
    private Color _textColor;
    private float _textWidth;
    private Point _pathHover;
    private Color _pathColor;
    private float _pathWidth;
    private DrawingTool _pathTool;
    private Rectangle _selection;
    private Rectangle _dragBounds;
    private Point _dragStart;
    private DragMode _drag;

    internal Rectangle SelectedArea => new(_selection.X + _desktop.X, _selection.Y + _desktop.Y, _selection.Width, _selection.Height);
    internal IReadOnlyList<Mark> Marks => _marks;

    internal ScreenshotEditorForm(Bitmap background, Rectangle desktop, Rectangle selection, string shape)
    {
        _background = background;
        _desktop = desktop;
        _shape = shape;
        if (background.Size != desktop.Size || !desktop.Contains(selection) || selection.Width < 5 || selection.Height < 5)
            throw new ArgumentException(L("Недопустимая область снимка.", "Invalid screenshot area."));
        _selection = new Rectangle(selection.X - desktop.X, selection.Y - desktop.Y, selection.Width, selection.Height);
        Text = L("Редактор снимка — Kliker Blocks", "Screenshot editor — Kliker Blocks");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = desktop;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        _toolbar = new FlowLayoutPanel
        {
            AutoSize = false, Height = desktop.Width >= 700 ? 44 : 80, Width = Math.Min(700, desktop.Width),
            WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(35, 42, 55),
            Padding = new Padding(3), Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(_toolbar);
        AddTool("↖", L("Переместить область и изменить границы", "Move selection and resize its edges"), DrawingTool.Selection);
        AddTool("✎", L("Карандаш", "Pen"), DrawingTool.Pen);
        AddToolMenu("╱", L("Линии", "Lines"),
            (DrawingTool.Line, "╱", L("Прямая линия", "Straight line")),
            (DrawingTool.Polyline, "⌁", L("Ломаная линия", "Polyline")),
            (DrawingTool.Curve, "∿", L("Плавная линия", "Smooth curve")));
        AddToolMenu("➜", L("Стрелки", "Arrows"),
            (DrawingTool.Arrow, "➜", L("Прямая стрелка", "Straight arrow")),
            (DrawingTool.DoubleArrow, "↔", L("Двусторонняя стрелка", "Double-headed arrow")),
            (DrawingTool.PolylineArrow, "↗", L("Ломаная стрелка", "Polyline arrow")),
            (DrawingTool.CurveArrow, "↝", L("Плавная стрелка", "Curved arrow")));
        AddToolMenu("▭", L("Фигуры", "Shapes"),
            (DrawingTool.Rectangle, "▭", L("Прямоугольник", "Rectangle")),
            (DrawingTool.Ellipse, "◯", L("Эллипс", "Ellipse")),
            (DrawingTool.Star, "☆", L("Звезда", "Star")),
            (DrawingTool.Heart, "♥", L("Сердце", "Heart")),
            (DrawingTool.Triangle, "△", L("Треугольник", "Triangle")),
            (DrawingTool.Diamond, "◇", L("Ромб", "Diamond")));
        AddTool("T", L("Текст", "Text"), DrawingTool.Text);
        var color = Button("●", L("Цвет разметки", "Annotation color"), (sender, _) =>
        {
            CommitInlineText();
            FinishPath();
            using var picker = new ColorDialog { Color = _color, FullOpen = true };
            if (picker.ShowDialog(this) == DialogResult.OK) _color = picker.Color;
            if (sender is Button button)
            {
                button.ForeColor = _color;
                button.BackColor = _color.GetBrightness() > 0.8f ? Color.FromArgb(45, 50, 60) : Color.WhiteSmoke;
            }
        });
        color.ForeColor = _color;
        _toolbar.Controls.Add(color);
        _width.Margin = new Padding(3, 7, 5, 3);
        _tips.SetToolTip(_width, L("Толщина линии и размер текста", "Line width and text size"));
        _toolbar.Controls.Add(_width);
        _toolbar.Controls.Add(Button("↶", L("Отменить последнюю отметку", "Undo last annotation"), (_, _) =>
        {
            CommitInlineText();
            FinishPath();
            if (_marks.Count > 0) { _marks.RemoveAt(_marks.Count - 1); Invalidate(); }
        }));
        _toolbar.Controls.Add(Button("⇅", L("Переместить панель инструментов", "Move the toolbar"), (_, _) =>
        {
            _toolbar.Top = _toolbar.Top < ClientSize.Height / 2 ? Math.Max(0, ClientSize.Height - _toolbar.Height) : 0;
        }));
        _toolbar.Controls.Add(Button(L("Сохранить", "Save"), L("Сохранить снимок", "Save screenshot"), (_, _) => { CommitInlineText(); FinishPath(); DialogResult = DialogResult.OK; Close(); }, 92));
        var cancel = Button(L("Отмена", "Cancel"), L("Отменить снимок (Esc)", "Cancel screenshot (Esc)"), (_, _) => { DialogResult = DialogResult.Cancel; Close(); }, 80);
        _toolbar.Controls.Add(cancel);
        _toolbar.SetFlowBreak(cancel, true);
        _pathHint.Text = L("Точки: щелчок. Завершить: Shift+щелчок или Enter. Отмена: Esc.", "Points: click. Finish: Shift+click or Enter. Cancel: Esc.");
        _toolbar.Controls.Add(_pathHint);
        UpdateToolButtons();
        PositionToolbar();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { CommitInlineText(false); foreach (var menu in _toolMenus) menu.Dispose(); _tips.Dispose(); }
        base.Dispose(disposing);
    }

    private Button Button(string text, string tip, EventHandler click, int width = 38)
    {
        var result = new Button
        {
            Text = text, Width = width, Height = 32, Margin = new Padding(2), AccessibleName = tip,
            BackColor = Color.WhiteSmoke, ForeColor = Color.FromArgb(25, 35, 48),
            UseVisualStyleBackColor = false, FlatStyle = FlatStyle.Flat
        };
        _tips.SetToolTip(result, tip);
        result.Click += click;
        return result;
    }

    private void AddTool(string text, string tip, DrawingTool tool)
    {
        var button = Button(text, tip, (_, _) => { CommitInlineText(); FinishPath(); _tool = tool; Cursor = tool == DrawingTool.Selection ? Cursors.SizeAll : Cursors.Cross; UpdateToolButtons(); });
        _toolButtons.Add(tool, button);
        _toolbar.Controls.Add(button);
    }

    private void AddToolMenu(string icon, string title, params (DrawingTool Tool, string Icon, string Label)[] choices)
    {
        var menu = new ContextMenuStrip();
        _toolMenus.Add(menu);
        Button? button = null;
        button = Button(icon + " ▾", title, (_, _) =>
        {
            CommitInlineText();
            FinishPath();
            menu.Show(button!, new Point(0, button!.Height));
        }, 48);
        button.ContextMenuStrip = menu;
        foreach (var choice in choices)
        {
            _toolButtons.Add(choice.Tool, button);
            menu.Items.Add(choice.Icon + "  " + choice.Label, null, (_, _) =>
            {
                _tool = choice.Tool;
                button.Text = choice.Icon + " ▾";
                button.AccessibleName = title + ": " + choice.Label;
                _tips.SetToolTip(button, title + ": " + choice.Label);
                Cursor = Cursors.Cross;
                UpdateToolButtons();
            });
        }
        _toolbar.Controls.Add(button);
    }

    private void UpdateToolButtons()
    {
        bool path = _tool is DrawingTool.Polyline or DrawingTool.Curve or DrawingTool.PolylineArrow or DrawingTool.CurveArrow;
        _pathHint.Visible = path;
        _toolbar.Height = (ClientSize.Width >= 700 ? 44 : 80) + (path ? 30 : 0);
        PositionToolbar();
        _toolButtons.TryGetValue(_tool, out var active);
        foreach (var button in _toolButtons.Values.Distinct())
        {
            button.BackColor = button == active ? Color.FromArgb(160, 210, 255) : Color.WhiteSmoke;
        }
    }

    private void PositionToolbar()
    {
        int x = Math.Clamp(_selection.Left, 0, Math.Max(0, ClientSize.Width - _toolbar.Width));
        int y = _selection.Bottom + _toolbar.Height + 8 <= ClientSize.Height
            ? _selection.Bottom + 8 : Math.Max(0, _selection.Top - _toolbar.Height - 8);
        _toolbar.Location = new Point(x, y);
        _toolbar.BringToFront();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_inlineText is not null)
        {
            if (e.KeyCode == Keys.Enter) { CommitInlineText(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { CommitInlineText(false); e.SuppressKeyPress = true; }
            return;
        }
        if (_pathPoints.Count > 0)
        {
            if (e.KeyCode == Keys.Enter) { FinishPath(); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Escape) { _pathPoints.Clear(); Invalidate(); e.SuppressKeyPress = true; return; }
        }
        if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; }
        if (e.Control && e.KeyCode == Keys.Z && _marks.Count > 0) { _marks.RemoveAt(_marks.Count - 1); Invalidate(); e.Handled = true; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_inlineText is not null && keyData == Keys.Enter) { CommitInlineText(); return true; }
        if (_inlineText is not null && keyData == Keys.Escape) { CommitInlineText(false); return true; }
        if (_pathPoints.Count > 0 && keyData == Keys.Enter) { FinishPath(); return true; }
        if (_pathPoints.Count > 0 && keyData == Keys.Escape) { _pathPoints.Clear(); Invalidate(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private DragMode Hit(Point p)
    {
        const int tolerance = 8;
        bool left = Math.Abs(p.X - _selection.Left) <= tolerance;
        bool right = Math.Abs(p.X - _selection.Right) <= tolerance;
        bool top = Math.Abs(p.Y - _selection.Top) <= tolerance;
        bool bottom = Math.Abs(p.Y - _selection.Bottom) <= tolerance;
        bool horizontal = p.X >= _selection.Left - tolerance && p.X <= _selection.Right + tolerance;
        bool vertical = p.Y >= _selection.Top - tolerance && p.Y <= _selection.Bottom + tolerance;
        if (left && top) return DragMode.TopLeft;
        if (right && top) return DragMode.TopRight;
        if (left && bottom) return DragMode.BottomLeft;
        if (right && bottom) return DragMode.BottomRight;
        if (left && vertical) return DragMode.Left;
        if (right && vertical) return DragMode.Right;
        if (top && horizontal) return DragMode.Top;
        if (bottom && horizontal) return DragMode.Bottom;
        return _selection.Contains(p) ? DragMode.Move : DragMode.None;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        CommitInlineText();
        if (_tool is DrawingTool.Polyline or DrawingTool.Curve or DrawingTool.PolylineArrow or DrawingTool.CurveArrow)
        {
            if (!_selection.Contains(e.Location)) return;
            if (_pathPoints.Count == 0) { _pathColor = _color; _pathWidth = (float)_width.Value; _pathTool = _tool; }
            if (_pathPoints.Count == 0 || _pathPoints[^1] != e.Location) _pathPoints.Add(e.Location);
            _pathHover = e.Location;
            if (e.Clicks >= 2 || (ShiftPressed() && _pathPoints.Count >= 2)) FinishPath(); else Invalidate();
            return;
        }
        if (_tool == DrawingTool.Text)
        {
            if (!_selection.Contains(e.Location)) return;
            BeginInlineText(e.Location);
            return;
        }
        _drag = _tool == DrawingTool.Selection ? Hit(e.Location) : _selection.Contains(e.Location) ? DragMode.Draw : DragMode.None;
        if (_drag == DragMode.None) return;
        _dragStart = e.Location;
        _dragBounds = _selection;
        _drawing.Clear();
        _shapeConstrained = ShiftPressed();
        if (_drag == DragMode.Draw) _drawing.Add(e.Location);
        Capture = true;
    }

    private void FinishPath()
    {
        if (_pathPoints.Count >= 2)
            _marks.Add(new Mark(_pathTool, _pathColor, _pathWidth, _pathPoints.ToArray()));
        _pathPoints.Clear();
        Invalidate();
    }

    private void BeginInlineText(Point point)
    {
        _textOrigin = point;
        _textColor = _color;
        _textWidth = (float)_width.Value;
        var font = new Font(SystemFonts.MessageBoxFont!.FontFamily, Math.Max(10, _textWidth * 4), FontStyle.Bold, GraphicsUnit.Pixel);
        var input = new TextBox
        {
            Location = point, Width = Math.Max(12, Math.Min(200, _selection.Right - point.X)),
            Font = font, ForeColor = _textColor,
            BackColor = _textColor.GetBrightness() > 0.8f ? Color.FromArgb(45, 50, 60) : Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = L("Текст на снимке. Enter — применить, Esc — отменить ввод", "Screenshot text. Enter applies, Esc cancels input")
        };
        _inlineText = input;
        input.TextChanged += (_, _) =>
        {
            var desired = TextRenderer.MeasureText(input.Text + "  ", input.Font).Width + 8;
            input.Width = Math.Max(12, Math.Min(Math.Max(80, desired), _selection.Right - point.X));
        };
        input.LostFocus += (_, _) => CommitInlineText();
        Controls.Add(input);
        input.BringToFront();
        input.Focus();
    }

    private void CommitInlineText(bool apply = true)
    {
        if (_inlineText is not { } input) return;
        _inlineText = null;
        var text = input.Text;
        var font = input.Font;
        Controls.Remove(input);
        input.Dispose();
        font.Dispose();
        if (apply && text.Length > 0)
            _marks.Add(new Mark(DrawingTool.Text, _textColor, _textWidth, [_textOrigin], text));
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pathPoints.Count > 0 && _tool is (DrawingTool.Polyline or DrawingTool.Curve or DrawingTool.PolylineArrow or DrawingTool.CurveArrow))
        {
            _pathHover = new Point(Math.Clamp(e.X, _selection.Left, _selection.Right), Math.Clamp(e.Y, _selection.Top, _selection.Bottom));
            Invalidate();
            return;
        }
        if (_drag == DragMode.None)
        {
            if (_tool == DrawingTool.Selection)
                Cursor = Hit(e.Location) switch
                {
                    DragMode.Left or DragMode.Right => Cursors.SizeWE,
                    DragMode.Top or DragMode.Bottom => Cursors.SizeNS,
                    DragMode.TopLeft or DragMode.BottomRight => Cursors.SizeNWSE,
                    DragMode.TopRight or DragMode.BottomLeft => Cursors.SizeNESW,
                    DragMode.Move => Cursors.SizeAll,
                    _ => Cursors.Default
                };
            return;
        }
        var point = new Point(Math.Clamp(e.X, 0, ClientSize.Width), Math.Clamp(e.Y, 0, ClientSize.Height));
        if (_drag == DragMode.Draw)
        {
            if (_tool is DrawingTool.Rectangle or DrawingTool.Ellipse or DrawingTool.Star or DrawingTool.Heart or DrawingTool.Triangle or DrawingTool.Diamond)
            {
                _shapeConstrained = ShiftPressed();
                point = ConstrainShape(_dragStart, point, _shapeConstrained);
            }
            if (_tool == DrawingTool.Pen) _drawing.Add(point);
            else if (_drawing.Count == 1) _drawing.Add(point);
            else _drawing[1] = point;
        }
        else _selection = ResizeSelection(_dragBounds, _dragStart, point, _drag, ClientSize, _shape != "Прямоугольник");
        Invalidate();
    }

    internal static Point ConstrainShape(Point start, Point end, bool shift)
    {
        if (!shift) return end;
        int dx = end.X - start.X, dy = end.Y - start.Y;
        int side = Math.Min(Math.Abs(dx), Math.Abs(dy));
        return new Point(start.X + Math.Sign(dx) * side, start.Y + Math.Sign(dy) * side);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_drag == DragMode.None) return;
        bool preserveConstraint = _shapeConstrained;
        OnMouseMove(e);
        if (_drag == DragMode.Draw && preserveConstraint && _drawing.Count >= 2 &&
            _tool is DrawingTool.Rectangle or DrawingTool.Ellipse or DrawingTool.Star or DrawingTool.Heart or DrawingTool.Triangle or DrawingTool.Diamond)
            _drawing[1] = ConstrainShape(_dragStart, _drawing[1], true);
        if (_drag == DragMode.Draw && _drawing.Count >= 2 && _drawing.Any(point => point != _drawing[0]))
            _marks.Add(new Mark(_tool, _color, (float)_width.Value, _drawing.ToArray()));
        _drawing.Clear(); _drag = DragMode.None; Capture = false;
        PositionToolbar(); Invalidate();
    }

    private static Rectangle ResizeSelection(Rectangle old, Point start, Point end, DragMode mode, Size desktop, bool square)
    {
        int dx = end.X - start.X, dy = end.Y - start.Y;
        if (mode == DragMode.Move)
        {
            int x = Math.Clamp(old.X + dx, 0, desktop.Width - old.Width);
            int y = Math.Clamp(old.Y + dy, 0, desktop.Height - old.Height);
            return new Rectangle(x, y, old.Width, old.Height);
        }
        int left = old.Left, right = old.Right, top = old.Top, bottom = old.Bottom;
        if (mode is DragMode.Left or DragMode.TopLeft or DragMode.BottomLeft) left = Math.Clamp(left + dx, 0, right - 5);
        if (mode is DragMode.Right or DragMode.TopRight or DragMode.BottomRight) right = Math.Clamp(right + dx, left + 5, desktop.Width);
        if (mode is DragMode.Top or DragMode.TopLeft or DragMode.TopRight) top = Math.Clamp(top + dy, 0, bottom - 5);
        if (mode is DragMode.Bottom or DragMode.BottomLeft or DragMode.BottomRight) bottom = Math.Clamp(bottom + dy, top + 5, desktop.Height);
        if (square)
        {
            bool horizontalOnly = mode is DragMode.Left or DragMode.Right;
            int side = horizontalOnly ? right - left : bottom - top;
            side = Math.Min(side, Math.Min(desktop.Width, desktop.Height));
            if (mode is DragMode.Left or DragMode.TopLeft or DragMode.BottomLeft) left = Math.Max(0, right - side);
            else right = Math.Min(desktop.Width, left + side);
            side = right - left;
            if (mode is DragMode.Top or DragMode.TopLeft or DragMode.TopRight) top = Math.Max(0, bottom - side);
            else bottom = Math.Min(desktop.Height, top + side);
            side = Math.Min(right - left, bottom - top);
            if (mode is DragMode.Left or DragMode.TopLeft or DragMode.BottomLeft) left = right - side; else right = left + side;
            if (mode is DragMode.Top or DragMode.TopLeft or DragMode.TopRight) top = bottom - side; else bottom = top + side;
        }
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.DrawImageUnscaled(_background, 0, 0);
        using var circle = new GraphicsPath();
        if (_shape == "Круг") circle.AddEllipse(_selection);
        using (var shade = new SolidBrush(Color.FromArgb(145, Color.Black)))
        {
            var oldClip = g.Save();
            if (_shape == "Круг")
            {
                using var region = new Region(circle);
                g.ExcludeClip(region);
            }
            else g.ExcludeClip(_selection);
            g.FillRectangle(shade, ClientRectangle);
            g.Restore(oldClip);
        }
        var state = g.Save();
        if (_shape == "Круг") g.SetClip(circle); else g.SetClip(_selection);
        foreach (var mark in _marks) DrawMark(g, mark);
        if (_pathPoints.Count > 0)
        {
            var preview = _pathPoints.Append(_pathHover).ToArray();
            if (preview.Length >= 2) DrawMark(g, new Mark(_pathTool, _pathColor, _pathWidth, preview));
        }
        if (_drag == DragMode.Draw && _drawing.Count >= 2) DrawMark(g, new Mark(_tool, _color, (float)_width.Value, _drawing.ToArray()));
        g.Restore(state);
        using var border = new Pen(Color.DeepSkyBlue, 2);
        if (_shape == "Круг") g.DrawEllipse(border, _selection); else g.DrawRectangle(border, _selection);
        foreach (var point in Handles(_selection))
            g.FillRectangle(Brushes.DeepSkyBlue, point.X - 4, point.Y - 4, 8, 8);
    }

    private static IEnumerable<Point> Handles(Rectangle r) =>
        [new(r.Left, r.Top), new(r.Right, r.Top), new(r.Left, r.Bottom), new(r.Right, r.Bottom),
         new(r.Left + r.Width / 2, r.Top), new(r.Left + r.Width / 2, r.Bottom),
         new(r.Left, r.Top + r.Height / 2), new(r.Right, r.Top + r.Height / 2)];

    private static void DrawMark(Graphics g, Mark mark)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(mark.Color, mark.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var first = mark.Points[0];
        if (mark.Tool == DrawingTool.Text)
        {
            using var font = new Font(SystemFonts.MessageBoxFont!.FontFamily, Math.Max(10, mark.Width * 4), FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(mark.Color);
            g.DrawString(mark.Text, font, brush, first);
            return;
        }
        if (mark.Points.Length < 2) return;
        var last = mark.Points[^1];
        var bounds = Rectangle.FromLTRB(Math.Min(first.X, last.X), Math.Min(first.Y, last.Y), Math.Max(first.X, last.X), Math.Max(first.Y, last.Y));
        switch (mark.Tool)
        {
            case DrawingTool.Pen: g.DrawLines(pen, mark.Points); break;
            case DrawingTool.Line: g.DrawLine(pen, first, last); break;
            case DrawingTool.Arrow:
                using (var cap = new AdjustableArrowCap(4, 5, true)) { pen.CustomEndCap = cap; g.DrawLine(pen, first, last); }
                break;
            case DrawingTool.DoubleArrow:
                using (var cap = new AdjustableArrowCap(4, 5, true)) { pen.CustomStartCap = cap; pen.CustomEndCap = cap; g.DrawLine(pen, first, last); }
                break;
            case DrawingTool.Rectangle: if (bounds.Width > 0 && bounds.Height > 0) g.DrawRectangle(pen, bounds); break;
            case DrawingTool.Ellipse: if (bounds.Width > 0 && bounds.Height > 0) g.DrawEllipse(pen, bounds); break;
            case DrawingTool.Polyline: g.DrawLines(pen, mark.Points); break;
            case DrawingTool.Curve:
                if (mark.Points.Length >= 3) g.DrawCurve(pen, mark.Points);
                else g.DrawLines(pen, mark.Points);
                break;
            case DrawingTool.PolylineArrow:
            case DrawingTool.CurveArrow:
                using (var cap = new AdjustableArrowCap(4, 5, true))
                {
                    pen.CustomEndCap = cap;
                    if (mark.Tool == DrawingTool.CurveArrow && mark.Points.Length >= 3) g.DrawCurve(pen, mark.Points);
                    else g.DrawLines(pen, mark.Points);
                }
                break;
            case DrawingTool.Star:
            case DrawingTool.Heart:
            case DrawingTool.Triangle:
            case DrawingTool.Diamond:
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    using var path = ShapePath(mark.Tool, bounds);
                    g.DrawPath(pen, path);
                }
                break;
        }
    }

    private static GraphicsPath ShapePath(DrawingTool tool, Rectangle r)
    {
        var path = new GraphicsPath();
        float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
        if (tool == DrawingTool.Star)
        {
            var points = new PointF[10];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float radius = i % 2 == 0 ? 1f : 0.4f;
                points[i] = new PointF(cx + (float)Math.Cos(angle) * r.Width / 2 * radius,
                    cy + (float)Math.Sin(angle) * r.Height / 2 * radius);
            }
            path.AddPolygon(points);
        }
        else if (tool == DrawingTool.Heart)
        {
            path.StartFigure();
            path.AddBezier(cx, r.Top + r.Height * .25f, r.Left + r.Width * .05f, r.Top - r.Height * .15f,
                r.Left - r.Width * .15f, r.Top + r.Height * .5f, cx, r.Bottom);
            path.AddBezier(cx, r.Bottom, r.Right + r.Width * .15f, r.Top + r.Height * .5f,
                r.Right - r.Width * .05f, r.Top - r.Height * .15f, cx, r.Top + r.Height * .25f);
            path.CloseFigure();
        }
        else if (tool == DrawingTool.Triangle)
            path.AddPolygon([new PointF(cx, r.Top), new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom)]);
        else
            path.AddPolygon([new PointF(cx, r.Top), new PointF(r.Right, cy), new PointF(cx, r.Bottom), new PointF(r.Left, cy)]);
        return path;
    }

    internal Bitmap RenderImage()
    {
        if ((long)_selection.Width * _selection.Height > 30_000_000)
            throw new InvalidOperationException(L("Область снимка слишком велика (максимум 30 млн пикселей).", "The screenshot area is too large (30 million pixels maximum)."));
        var result = new Bitmap(_selection.Width, _selection.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.Clear(Color.Transparent);
            if (_shape == "Круг")
            {
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, result.Width - 1, result.Height - 1);
                graphics.SetClip(path);
            }
            graphics.DrawImage(_background, new Rectangle(0, 0, result.Width, result.Height), _selection, GraphicsUnit.Pixel);
            graphics.TranslateTransform(-_selection.X, -_selection.Y);
            foreach (var mark in _marks) DrawMark(graphics, mark);
            return result;
        }
        catch { result.Dispose(); throw; }
    }
}

