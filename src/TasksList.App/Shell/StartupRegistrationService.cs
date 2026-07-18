using System.Security;
using System.IO;
using Microsoft.Win32;

namespace TasksList.App.Shell;

public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TasksList";

    public static string BuildCommand(string executablePath) =>
        $"\"{Path.GetFullPath(executablePath)}\" --startup";

    public static string? TryApply(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return "Windows startup registration is unavailable.";
            if (enabled)
            {
                key.SetValue(ValueName, BuildCommand(executablePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            return $"Could not update Start with Windows: {exception.Message}";
        }
    }
}
