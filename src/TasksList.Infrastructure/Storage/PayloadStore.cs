using System.Security.Cryptography;

namespace TasksList.Infrastructure.Storage;

public sealed record PayloadDescriptor(string Hash, string Path, long Size, string MediaType);

public sealed class PayloadStore
{
    private readonly string _rootPath;

    public PayloadStore(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<PayloadDescriptor> PutAsync(
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        var directory = System.IO.Path.Combine(_rootPath, hash[..2]);
        var finalPath = System.IO.Path.Combine(directory, hash);
        Directory.CreateDirectory(directory);

        if (!File.Exists(finalPath))
        {
            var temporaryPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, content.ToArray(), cancellationToken);
                try
                {
                    File.Move(temporaryPath, finalPath);
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        return new PayloadDescriptor(hash, finalPath, content.Length, mediaType);
    }
}
