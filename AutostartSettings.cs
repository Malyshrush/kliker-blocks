using Microsoft.Win32;

namespace KlikerBlocks;

internal static class AutostartSettings
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KlikerBlocks";

    internal static string CommandFor(string executable) => "\"" + executable + "\"";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(ValueName) as string,
            CommandFor(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase);
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
            ?? throw new InvalidOperationException("Cannot open the Windows startup settings.");
        if (enabled) key.SetValue(ValueName, CommandFor(Application.ExecutablePath), RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
