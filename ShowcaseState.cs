using System.Text.Json;
using System.Reflection;

namespace KlikerBlocks;

internal sealed record ShowcaseLaunchState(DateTime FirstShownUtc, DateTime? FirstRunUtc);

internal static class ShowcaseState
{
    public static string DemoDirectory => Path.Combine(
        Path.GetDirectoryName(ComputerCapabilities.SavedFilePath) ?? AppContext.BaseDirectory, "Showcase");
    public static string StatePath
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            return Path.Combine(
                Path.GetDirectoryName(ComputerCapabilities.SavedFilePath) ?? AppContext.BaseDirectory,
                $"showcase-state-v{version.Major}.{version.Minor:00}.{Math.Max(0, version.Build)}.json");
        }
    }

    public static DateTime? FirstShownUtc => ReadFirstShownUtc(StatePath);
    public static DateTime? FirstRunUtc => ReadState(StatePath)?.FirstRunUtc;

    internal static DateTime? ReadFirstShownUtc(string path)
        => ReadState(path)?.FirstShownUtc;

    private static ShowcaseLaunchState? ReadState(string path)
    {
        try { return JsonSerializer.Deserialize<ShowcaseLaunchState>(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static DateTime MarkFirstShown() => MarkFirstShown(StatePath);

    internal static DateTime MarkFirstShown(string path)
    {
        if (ReadFirstShownUtc(path) is { } existing) return existing;
        var now = DateTime.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new ShowcaseLaunchState(now, null),
            new JsonSerializerOptions { WriteIndented = true }));
        return now;
    }

    public static DateTime MarkFirstRun() => MarkFirstRun(StatePath);

    internal static DateTime MarkFirstRun(string path)
    {
        var state = ReadState(path);
        if (state?.FirstRunUtc is { } existing) return existing;
        var now = DateTime.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new ShowcaseLaunchState(
            state?.FirstShownUtc ?? now, now), new JsonSerializerOptions { WriteIndented = true }));
        return now;
    }
}
