using Microsoft.Win32;

namespace KlikerBlocks;

internal static class AutostartSettings
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KlikerBlocks";

    internal static string CommandFor(string executable) => "\"" + executable + "\" --windows-startup";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var command = key?.GetValue(ValueName) as string;
        if (string.Equals(command, CommandFor(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(command, "\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase))
            return false;
        SetEnabled(true); // Upgrade an existing Windows startup entry to the scenario-aware command.
        return true;
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
            ?? throw new InvalidOperationException("Cannot open the Windows startup settings.");
        if (enabled) key.SetValue(ValueName, CommandFor(Application.ExecutablePath), RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
