using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace TasksList.App.Shell;

public static class TrayIconLoader
{
    public static Icon Load(string? sourcePath)
    {
        try
        {
            if (sourcePath is { Length: > 0 } resolvedPath && File.Exists(resolvedPath))
            {
                if (string.Equals(Path.GetExtension(resolvedPath), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    return new Icon(resolvedPath);
                }

                using var associated = Icon.ExtractAssociatedIcon(resolvedPath);
                if (associated is not null)
                {
                    return (Icon)associated.Clone();
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                                         UnauthorizedAccessException or ExternalException or NotSupportedException)
        {
            // A missing or damaged visual resource must not prevent the notification-area controls from loading.
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
