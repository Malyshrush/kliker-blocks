using System.Reflection;

namespace KlikerBlocks;

internal static class AppIdentity
{
    internal static string Title => TitleFor(Assembly.GetExecutingAssembly().GetName().Version);

    internal static string TitleFor(Version? version) => version is null
        ? "Kliker Blocks"
        : $"Kliker Blocks v{version.Major}.{version.Minor}" +
          (version.Build > 0 ? $".{version.Build}" : "");
}
