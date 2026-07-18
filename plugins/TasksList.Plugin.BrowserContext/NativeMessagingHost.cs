using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace TasksList.Plugin.BrowserContext;

public static class NativeMessagingHost
{
    public static async Task RunAsync(
        Stream input,
        Stream output,
        string bridgePath,
        CancellationToken cancellationToken = default)
    {
        var lengthBytes = new byte[4];
        while (await ReadExactlyOrEofAsync(input, lengthBytes, cancellationToken))
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (length is <= 0 or > 8_388_608)
            {
                throw new InvalidDataException("The browser companion message size is invalid.");
            }

            var payload = new byte[length];
            await input.ReadExactlyAsync(payload, cancellationToken);
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("type", out var type) && type.GetString() == "snapshot")
            {
                var directory = Path.GetDirectoryName(bridgePath)
                    ?? throw new InvalidOperationException("The browser bridge path has no directory.");
                Directory.CreateDirectory(directory);
                var temporaryPath = $"{bridgePath}.{Guid.NewGuid():N}.tmp";
                await File.WriteAllBytesAsync(temporaryPath, payload, cancellationToken);
                File.Move(temporaryPath, bridgePath, true);
            }

            var response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                ok = true,
                receivedAt = DateTimeOffset.UtcNow,
            });
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, response.Length);
            await output.WriteAsync(lengthBytes, cancellationToken);
            await output.WriteAsync(response, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
    }

    private static async Task<bool> ReadExactlyOrEofAsync(
        Stream input,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await input.ReadAsync(buffer[offset..], cancellationToken);
            if (count == 0)
            {
                return offset == 0
                    ? false
                    : throw new EndOfStreamException("The native browser message ended early.");
            }
            offset += count;
        }
        return true;
    }
}

