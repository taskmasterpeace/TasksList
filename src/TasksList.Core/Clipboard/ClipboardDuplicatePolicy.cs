using System.Security.Cryptography;
using System.Text;
using TasksList.Core.Models;

namespace TasksList.Core.Clipboard;

public static class ClipboardDuplicatePolicy
{
    public static string ComputeHash(Capture capture)
    {
        var builder = new StringBuilder().Append((int)capture.Kind).Append('\n');
        if (capture.TextRepresentations.Count == 0)
        {
            builder.Append("preview\0").Append(capture.PreviewText);
        }
        else
        {
            foreach (var representation in capture.TextRepresentations
                         .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(representation.Key.ToLowerInvariant())
                    .Append('\0')
                    .Append(representation.Value)
                    .Append('\n');
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static Capture Promote(Capture existing, DateTimeOffset copiedAt) =>
        existing.Promote(copiedAt);
}
