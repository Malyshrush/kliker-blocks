using System.Text.RegularExpressions;

namespace KlikerBlocks;

public sealed class LinkAppearance
{
    public string End { get; set; } = "Arrow";
    public string Pattern { get; set; } = "Solid";
    public string Color { get; set; } = "#4C70AB";
    public int OpacityPercent { get; set; } = 100;
    public decimal Thickness { get; set; } = 2.5m;
    public int EndSize { get; set; } = 12;
    public int ExitDistance { get; set; } = 14;
    public int EntryDistance { get; set; } = 14;

    public bool IsValid() =>
        End is "Arrow" or "Line" or "Diamond" or "Circle" &&
        Pattern is "Solid" or "Dash" or "Dot" or "DashDot" or "DashDotDot" &&
        Color is not null && Regex.IsMatch(Color, "^#[0-9A-Fa-f]{6}$") &&
        OpacityPercent is >= 5 and <= 100 && Thickness is >= 1 and <= 12 &&
        EndSize is >= 4 and <= 48 && ExitDistance is >= 0 and <= 200 &&
        EntryDistance is >= 0 and <= 200;

    public LinkAppearance Copy() => (LinkAppearance)MemberwiseClone();
}
