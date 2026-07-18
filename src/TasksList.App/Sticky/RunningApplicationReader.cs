using System.Diagnostics;
using TasksList.Core.Models;

namespace TasksList.App.Sticky;

public static class RunningApplicationReader
{
    public static IReadOnlySet<string> GetExecutablePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { Length: > 0 } path)
                    {
                        paths.Add(path);
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or
                    System.ComponentModel.Win32Exception or
                    NotSupportedException)
                {
                    // Protected and short-lived processes are expected here.
                }
            }
        }
        return paths;
    }

    public static bool IsRunning(ContextRef context, IReadOnlySet<string> executablePaths)
    {
        var separator = context.StableIdentity.IndexOf('|');
        var executable = separator >= 0
            ? context.StableIdentity[..separator]
            : context.StableIdentity;
        return executablePaths.Contains(executable);
    }
}
