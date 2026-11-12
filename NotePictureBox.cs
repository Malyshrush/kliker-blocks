namespace KlikerBlocks;

// Image transforms are display-only; saving a note preserves the original bitmap.
internal sealed class NotePictureBox : PictureBox
{
    private Point? _dragStart;
    private PointF _startOffset;
    internal float ViewZoom { get; private set; } = 1f;
    internal PointF ViewOffset { get; private set; }
    internal event EventHandler? ViewChanged;

    internal NotePictureBox() { DoubleBuffered = true; Cursor = Cursors.SizeAll; }

    internal void SetView(float zoom, float x, float y)
    {
        ViewZoom = float.IsFinite(zoom) ? Math.Clamp(zoom, 0.1f, 10f) : 1f;
        ViewOffset = new PointF(float.IsFinite(x) ? Math.Clamp(x, -10000f, 10000f) : 0f,
            float.IsFinite(y) ? Math.Clamp(y, -10000f, 10000f) : 0f);
        Invalidate();
    }
    internal void ZoomBy(float factor) { SetView(ViewZoom * factor, ViewOffset.X, ViewOffset.Y); ViewChanged?.Invoke(this, EventArgs.Empty); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Image is not null && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            float width = ClientSize.Width, height = ClientSize.Height;
            if (SizeMode == PictureBoxSizeMode.Zoom)
            {
                var fit = Math.Min(width / Image.Width, height / Image.Height);
                width = Image.Width * fit; height = Image.Height * fit;
            }
            width *= ViewZoom; height *= ViewZoom;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(Image, (ClientSize.Width - width) / 2 + ViewOffset.X,
                (ClientSize.Height - height) / 2 + ViewOffset.Y, width, height);
        }
        // Do not call PictureBox.OnPaint: it would paint the untransformed image again.
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Image is not null) { _dragStart = e.Location; _startOffset = ViewOffset; Capture = true; }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragStart is not Point start || (e.Button & MouseButtons.Left) == 0) return;
        SetView(ViewZoom, _startOffset.X + e.X - start.X, _startOffset.Y + e.Y - start.Y);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragStart is null) return;
        _dragStart = null; Capture = false;
        if (ViewOffset != _startOffset) ViewChanged?.Invoke(this, EventArgs.Empty);
    }
}
