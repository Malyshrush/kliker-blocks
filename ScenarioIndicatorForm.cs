using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KlikerBlocks;

internal sealed record CursorWatchState(bool Armed, bool Active, string ActivationMode,
    string ActivationKey, string RequiredHold, Point Anchor, string RequiredKey = "SHIFT")
{
    public string Detail => ActivationMode switch
    {
        "Переключатель" => $"{ActivationKey}: {(Armed ? "включено" : "выключено")}",
        "Пока удерживается" => $"{ActivationKey}: {(Armed ? "удерживается" : "отпущена")}",
        _ => Armed ? "Слежение включено" : "Слежение выключено"
    };
}

internal sealed class ScenarioIndicatorForm : Form
{
    public static readonly string[] WindowShapes =
    ["Прямоугольник", "Квадрат", "Круг", "Овал", "Скруглённый прямоугольник", "Скруглённый квадрат", "Капсула", "Ромб", "Звезда", "Шестиугольник"];

    private readonly Font _titleFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font _detailFont = new("Segoe UI", 8.5f);
    private IndicatorAppearance _appearance = new();
    private Color _light = Color.FromArgb(75, 157, 232);
    private string _title = "СЦЕНАРИЙ ВЫПОЛНЯЕТСЯ";
    private string _detail = "";
    private Rectangle _automaticArea;
    private bool _fixedPosition;
    private Point? _dragOffset;
    public event Action<Point>? PositionChangedByUser;
    private int _state; // 0: scenario running, 1: activation armed, 2: activation off
    private Bitmap? _backgroundImage;
    private Bitmap? _runningImage;
    private Bitmap? _activeImage;
    private Bitmap? _inactiveImage;
    private Bitmap? _legacyImage;

    public ScenarioIndicatorForm()
    {
        Size = new Size(228, 62);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(30, 36, 45);
        DoubleBuffered = true;
        _automaticArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        PositionAt(_automaticArea);
        UpdateWindowRegion();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000;
            return parameters;
        }
    }

    public void SetAppearance(IndicatorAppearance? appearance, string? projectDirectory = null)
    {
        _appearance = (appearance ?? new IndicatorAppearance()).Copy();
        BackColor = IndicatorAppearance.ParseColor(_appearance.BackgroundColor, Color.FromArgb(30, 36, 45));
        ReplaceImage(ref _legacyImage,
            _appearance.Symbol == "Изображение" ? _appearance.ImagePath : "", projectDirectory);
        ReplaceImage(ref _backgroundImage, _appearance.BackgroundMode == "Картинка" ? _appearance.BackgroundImagePath : "", projectDirectory);
        ReplaceImage(ref _runningImage, _appearance.RunningMode == "Картинка" ? _appearance.RunningImagePath : "", projectDirectory);
        ReplaceImage(ref _activeImage, _appearance.ActiveMode == "Картинка" ? _appearance.ActiveImagePath : "", projectDirectory);
        ReplaceImage(ref _inactiveImage, _appearance.InactiveMode == "Картинка" ? _appearance.InactiveImagePath : "", projectDirectory);
        Size = _appearance.EffectiveSize();
        UpdateWindowRegion();
        UpdateLight();
        if (!_fixedPosition) PositionAt(_automaticArea);
        Redraw();
    }

    private static Bitmap? LoadImage(string path, string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(projectDirectory ?? AppContext.BaseDirectory, path);
            using var original = Image.FromFile(fullPath);
            return new Bitmap(original);
        }
        catch { return null; }
    }

    private static void ReplaceImage(ref Bitmap? destination, string path, string? projectDirectory)
    {
        destination?.Dispose();
        destination = LoadImage(path, projectDirectory);
    }

    private void UpdateLight()
    {
        var color = _state switch
        {
            1 => _appearance.ActiveColor,
            2 => _appearance.InactiveColor,
            _ => _appearance.RunningColor
        };
        _light = IndicatorAppearance.ParseColor(color, Color.FromArgb(75, 157, 232));
    }

    public void SetRunning()
    {
        _state = 0;
        _title = _appearance.RunningTitle;
        _detail = "";
        UpdateLight();
        Redraw();
    }

    public void SetWatch(CursorWatchState state)
    {
        _state = state.Armed ? 1 : 2;
        _title = state.Armed ? _appearance.ActiveTitle : _appearance.InactiveTitle;
        _detail = state.Detail;
        _automaticArea = Screen.FromPoint(state.Anchor).WorkingArea;
        UpdateLight();
        if (!_fixedPosition) PositionAt(_automaticArea);
        Redraw();
    }

    public void SetConditions(bool active, string detail)
    {
        _state = active ? 1 : 2;
        _title = active
            ? _appearance.ActiveTitle == "СЛЕЖЕНИЕ АКТИВНО" ? "УСЛОВИЯ ВЫПОЛНЕНЫ" : _appearance.ActiveTitle
            : _appearance.InactiveTitle == "СЛЕЖЕНИЕ НЕАКТИВНО" ? "ОЖИДАНИЕ УСЛОВИЙ" : _appearance.InactiveTitle;
        _detail = detail;
        UpdateLight();
        Redraw();
    }

    public void SetPlacement(ProjectModel settings)
    {
        _fixedPosition = settings.UseCustomIndicatorPosition;
        if (_fixedPosition) Location = new Point(settings.IndicatorX, settings.IndicatorY);
        else PositionAt(_automaticArea);
    }

    private void PositionAt(Rectangle area) => Location = new Point(area.Right - Width - 18, area.Top + 18);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragOffset is not Point offset || !Capture) return;
        _fixedPosition = true;
        Location = new Point(Cursor.Position.X - offset.X, Cursor.Position.Y - offset.Y);
        Redraw();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _dragOffset is null) return;
        _dragOffset = null;
        Capture = false;
        if (_fixedPosition) PositionChangedByUser?.Invoke(Location);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateWindowRegion();
        Redraw();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Redraw();
    }

    private void Redraw()
    {
        if (!IsHandleCreated || IsDisposed || Width < 2 || Height < 2) return;
        using var bitmap = RenderBitmap();
        LayeredWindow.Update(Handle, Location, bitmap);
    }

    private void UpdateWindowRegion()
    {
        if (Width < 2 || Height < 2) return;
        using var path = BuildWindowPath();
        var former = Region;
        Region = new Region(path);
        former?.Dispose();
    }

    internal int DisplayState => _state;

    private GraphicsPath BuildWindowPath()
    {
        var path = new GraphicsPath();
        var rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
        var middleX = rectangle.Left + rectangle.Width / 2f;
        var middleY = rectangle.Top + rectangle.Height / 2f;
        switch (_appearance.EffectiveWindowShape())
        {
            case "Прямоугольник":
            case "Квадрат": path.AddRectangle(rectangle); break;
            case "Круг":
            case "Овал": path.AddEllipse(rectangle); break;
            case "Капсула": path.AddRoundedRectangle(rectangle, new Size(Math.Min(rectangle.Width, rectangle.Height), Math.Min(rectangle.Width, rectangle.Height))); break;
            case "Ромб": path.AddPolygon([new PointF(middleX, 0), new PointF(Width - 1, middleY),
                new PointF(middleX, Height - 1), new PointF(0, middleY)]); break;
            case "Звезда":
            {
                var points = new PointF[10];
                for (var i = 0; i < points.Length; i++)
                {
                    var angle = -Math.PI / 2 + i * Math.PI / 5;
                    var factor = i % 2 == 0 ? 1.0 : 0.48;
                    points[i] = new PointF(middleX + (float)(factor * (rectangle.Width / 2f) * Math.Cos(angle)),
                        middleY + (float)(factor * (rectangle.Height / 2f) * Math.Sin(angle)));
                }
                path.AddPolygon(points);
                break;
            }
            case "Шестиугольник": path.AddPolygon([new PointF(rectangle.Width * .25f, 0), new PointF(rectangle.Width * .75f, 0),
                new PointF(Width - 1, middleY), new PointF(rectangle.Width * .75f, Height - 1),
                new PointF(rectangle.Width * .25f, Height - 1), new PointF(0, middleY)]); break;
            default:
            {
                var diameter = Math.Min(18, Math.Min(Width, Height) / 2);
                path.AddRoundedRectangle(rectangle, new Size(diameter, diameter));
                break;
            }
        }
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var bitmap = RenderBitmap();
        e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
    }

    internal Bitmap RenderBitmap()
    {
        var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        DrawContent(graphics);
        return bitmap;
    }

    private void DrawContent(Graphics graphics)
    {
        using var path = BuildWindowPath();
        graphics.SetClip(path);
        var dominant = _appearance.SymbolDominates();
        if (!dominant)
        {
            var backgroundColor = IndicatorAppearance.ParseColor(_appearance.BackgroundColor, Color.FromArgb(30, 36, 45));
            var backgroundAlpha = OpacityByte(_appearance.BackgroundOpacityPercent);
            if (backgroundAlpha > 0)
            {
                if (_backgroundImage != null)
                    DrawImage(graphics, _backgroundImage, new Rectangle(0, 0, Width, Height),
                        _appearance.BackgroundOpacityPercent);
                else
                {
                    using var background = new SolidBrush(Color.FromArgb(backgroundAlpha, backgroundColor));
                    graphics.FillPath(background, path);
                }
                using var outline = new Pen(Color.FromArgb(backgroundAlpha * 68 / 255, 68, 77, 91));
                graphics.DrawPath(outline, path);
            }
        }

        var hasTitle = _appearance.ShowTitle && (_state switch
        {
            0 => _appearance.ShowRunningTitle,
            1 => _appearance.ShowActiveTitle,
            _ => _appearance.ShowInactiveTitle
        }) && !string.IsNullOrEmpty(_title) && !dominant;
        var hasDetail = _appearance.ShowDetail && _state != 0 && !string.IsNullOrEmpty(_detail) && !dominant;
        var hasText = hasTitle || hasDetail;
        var symbolSize = dominant ? Math.Min(Width, Height) - 1 :
            Math.Min(Math.Clamp(_appearance.SymbolSize, 6, 160), Math.Min(Width, Height) - 8);
        var symbolX = dominant ? 0 : hasText ? 12 : (Width - symbolSize) / 2;
        var symbolY = dominant ? 0 : (Height - symbolSize) / 2;
        var bounds = new Rectangle(symbolX, symbolY, symbolSize, symbolSize);
        var symbolOpacity = _state switch
        {
            1 => _appearance.ActiveOpacityPercent,
            2 => _appearance.InactiveOpacityPercent,
            _ => _appearance.RunningOpacityPercent
        };
        var stateImage = _state switch { 1 => _activeImage, 2 => _inactiveImage, _ => _runningImage } ?? _legacyImage;
        if (stateImage != null) DrawImage(graphics, stateImage, bounds, symbolOpacity);
        else if (symbolOpacity > 0)
        {
            using var symbol = new SolidBrush(Color.FromArgb(OpacityByte(symbolOpacity), _light));
            if (_appearance.Symbol == "Квадрат") graphics.FillRectangle(symbol, bounds);
            else if (_appearance.Symbol == "Ромб")
            {
                var x = symbolX + symbolSize / 2;
                var y = symbolY + symbolSize / 2;
                graphics.FillPolygon(symbol, [new Point(x, symbolY), new Point(symbolX + symbolSize, y),
                    new Point(x, symbolY + symbolSize), new Point(symbolX, y)]);
            }
            else graphics.FillEllipse(symbol, bounds);
        }

        if (!hasText) return;
        var textX = symbolX + symbolSize + 10;
        var textWidth = Math.Max(0, Width - textX - 6);
        if (textWidth <= 0) return;
        using var titleBrush = new SolidBrush(Color.White);
        using var detailBrush = new SolidBrush(Color.FromArgb(210, 218, 228));
        if (hasTitle)
            graphics.DrawString(_title, _titleFont, titleBrush,
                new RectangleF(textX, hasDetail ? Math.Max(3, Height / 2f - 21) : Math.Max(2, Height / 2f - 10),
                    textWidth, hasDetail ? 21 : Height - 4));
        if (hasDetail)
            graphics.DrawString(_detail, _detailFont, detailBrush,
                new RectangleF(textX, hasTitle ? Height / 2f : Math.Max(2, Height / 2f - 9),
                    textWidth, hasTitle ? Math.Max(0, Height / 2f - 2) : Height - 4));
    }

    private static int OpacityByte(int percent) => Math.Clamp(percent, 0, 100) * 255 / 100;

    private static void DrawImage(Graphics graphics, Image image, Rectangle bounds, int opacityPercent)
    {
        if (opacityPercent <= 0) return;
        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix(new float[][]
        {
            [1, 0, 0, 0, 0], [0, 1, 0, 0, 0], [0, 0, 1, 0, 0],
            [0, 0, 0, Math.Clamp(opacityPercent, 0, 100) / 100f, 0], [0, 0, 0, 0, 1]
        });
        attributes.SetColorMatrix(matrix);
        graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _detailFont.Dispose();
            _backgroundImage?.Dispose();
            _runningImage?.Dispose();
            _activeImage?.Dispose();
            _inactiveImage?.Dispose();
            _legacyImage?.Dispose();
        }
        base.Dispose(disposing);
    }
}
