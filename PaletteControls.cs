using System.Drawing.Drawing2D;

namespace KlikerBlocks;

internal sealed class PaletteSectionHeader : Panel
{
    private readonly Label _arrow = new();
    private readonly Label _title = new();
    private bool _expanded;
    private bool _hovered;

    internal event EventHandler? Clicked;

    internal PaletteSectionHeader(string title)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        _arrow.SetBounds(2, 0, 22, 31);
        _arrow.Font = new Font("Segoe UI Symbol", 9.5f, FontStyle.Bold);
        _arrow.TextAlign = ContentAlignment.MiddleCenter;
        _title.SetBounds(24, 0, 220, 31);
        _title.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.AutoEllipsis = true;
        Controls.Add(_arrow);
        Controls.Add(_title);
        SetTitle(title);
        UpdateArrow();
        Bind(_arrow);
        Bind(_title);
        Bind(this);
        Resize += (_, _) => _title.Width = Math.Max(40, ClientSize.Width - 29);
    }

    internal void SetTitle(string title) => _title.Text = title;

    internal void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        UpdateArrow();
    }

    private void UpdateArrow()
    {
        _arrow.Text = _expanded ? "▾" : "▸";
        _arrow.ForeColor = _hovered ? Color.FromArgb(36, 109, 191) : Color.FromArgb(88, 106, 128);
        _title.ForeColor = _hovered ? Color.FromArgb(34, 92, 157) : Color.FromArgb(42, 58, 77);
        Invalidate();
    }

    private void Bind(Control control)
    {
        control.Click += (_, args) => Clicked?.Invoke(this, args);
        control.MouseEnter += (_, _) => { _hovered = true; UpdateArrow(); };
        control.MouseLeave += (_, _) => { _hovered = false; UpdateArrow(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(_hovered ? Color.FromArgb(100, 152, 210) : Color.FromArgb(212, 222, 234));
        e.Graphics.DrawLine(pen, 3, Height - 1, Width - 4, Height - 1);
    }
}

internal sealed class PaletteBlockButton : Button
{
    private bool _hovered;
    private bool _pressed;

    internal PaletteBlockButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(40, 55, 73);
        Font = new Font("Segoe UI", 9.2f);
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

    protected override void OnPaintBackground(PaintEventArgs pevent) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        var card = new Rectangle(2, 1, Math.Max(1, Width - 6), Math.Max(1, Height - 7));
        var shadow = card;
        shadow.Offset(0, 3);
        using (var shadowPath = Rounded(shadow, 8))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(45, 37, 61, 92)))
            graphics.FillPath(shadowBrush, shadowPath);

        using var path = Rounded(card, 8);
        var top = !Enabled ? Color.FromArgb(242, 244, 247) : _pressed
            ? Color.FromArgb(198, 220, 246) : _hovered ? Color.White : Color.FromArgb(255, 255, 255);
        var bottom = !Enabled ? Color.FromArgb(230, 234, 239) : _pressed
            ? Color.FromArgb(219, 235, 253) : _hovered ? Color.FromArgb(224, 239, 255) : Color.FromArgb(235, 243, 252);
        using (var fill = new LinearGradientBrush(card, top, bottom, LinearGradientMode.Vertical))
            graphics.FillPath(fill, path);
        using (var outline = new Pen(_hovered || _pressed ? Color.FromArgb(107, 158, 218) : Color.FromArgb(196, 211, 230)))
            graphics.DrawPath(outline, path);
        using (var highlight = new Pen(Color.FromArgb(205, 255, 255, 255)))
            graphics.DrawLine(highlight, card.Left + 9, card.Top + 2, card.Right - 9, card.Top + 2);

        var badge = new Rectangle(card.Left + 8, card.Top + (card.Height - 18) / 2, 18, 18);
        using (var badgePath = Rounded(badge, 6))
        using (var badgeFill = new LinearGradientBrush(badge, Color.FromArgb(99, 163, 230), Color.FromArgb(52, 117, 192), LinearGradientMode.Vertical))
            graphics.FillPath(badgeFill, badgePath);
        using (var plus = new Pen(Color.White, 1.7f))
        {
            plus.StartCap = LineCap.Round;
            plus.EndCap = LineCap.Round;
            graphics.DrawLine(plus, badge.Left + 5, badge.Top + 9, badge.Right - 5, badge.Top + 9);
            graphics.DrawLine(plus, badge.Left + 9, badge.Top + 5, badge.Left + 9, badge.Bottom - 5);
        }

        var textBounds = new Rectangle(card.Left + 34, card.Top + 1,
            Math.Max(0, card.Width - 42), card.Height - 1);
        TextRenderer.DrawText(graphics, Text, Font, textBounds,
            Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(card, -4, -4);
            ControlPaint.DrawFocusRectangle(graphics, focus);
        }
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
