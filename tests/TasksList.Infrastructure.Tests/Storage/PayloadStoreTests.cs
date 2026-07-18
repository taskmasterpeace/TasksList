using System.Text;
using TasksList.Infrastructure.Storage;

namespace TasksList.Infrastructure.Tests.Storage;

public sealed class PayloadStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"taskslist-payload-tests-{Guid.NewGuid():N}");

    public PayloadStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public async Task IdenticalPayloadsShareOneContentAddressedFile()
    {
        var store = new PayloadStore(_directory);
        var first = await store.PutAsync(Encoding.UTF8.GetBytes("same clipboard image"), "text/plain");
        var second = await store.PutAsync(Encoding.UTF8.GetBytes("same clipboard image"), "text/plain");

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Path, second.Path);
        Assert.True(File.Exists(first.Path));
        Assert.Single(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }
}
