using System.Drawing.Drawing2D;

namespace KlikerBlocks;

internal sealed class KlikerMenuRenderer : ToolStripProfessionalRenderer
{
    public KlikerMenuRenderer() : base(new Palette()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.IsOnDropDown) { base.OnRenderMenuItemBackground(e); return; }
        var bounds = new Rectangle(2, 3, e.Item.Width - 4, e.Item.Height - 6);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var featured = Equals(e.Item.Tag, "featured");
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(bounds, 7);
        using var fill = new SolidBrush(e.Item.Pressed
            ? Color.FromArgb(195, 218, 246)
            : e.Item.Selected ? Color.FromArgb(213, 232, 253)
            : featured ? Color.FromArgb(218, 233, 250)
            : Color.FromArgb(237, 243, 250));
        using var border = new Pen(e.Item.Selected || e.Item.Pressed || featured
            ? Color.FromArgb(157, 187, 223) : Color.FromArgb(220, 230, 242));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class Palette : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Color.FromArgb(247, 249, 252);
        public override Color MenuStripGradientEnd => Color.FromArgb(247, 249, 252);
        public override Color MenuItemSelected => Color.FromArgb(225, 236, 250);
        public override Color MenuItemBorder => Color.FromArgb(175, 200, 231);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(225, 236, 250);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(225, 236, 250);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(210, 225, 247);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(210, 225, 247);
        public override Color ToolStripDropDownBackground => Color.FromArgb(252, 253, 255);
        public override Color ImageMarginGradientBegin => Color.FromArgb(242, 246, 252);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(242, 246, 252);
        public override Color ImageMarginGradientEnd => Color.FromArgb(242, 246, 252);
        public override Color SeparatorDark => Color.FromArgb(217, 225, 236);
        public override Color SeparatorLight => Color.White;
    }
}
