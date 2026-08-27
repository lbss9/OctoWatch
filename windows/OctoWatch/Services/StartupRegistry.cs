using Microsoft.Win32;

namespace OctoWatch;

internal static class StartupRegistry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OctoWatch";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        if (key is null)
            return;
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;
        key.SetValue(ValueName, $"\"{exe}\"");
    }
}
