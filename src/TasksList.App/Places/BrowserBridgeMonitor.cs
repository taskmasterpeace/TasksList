using System.IO;
using System.Text.Json;
using TasksList.Core.Models;

namespace TasksList.App.Places;

public sealed record LiveBrowserTab(
    string Id,
    string WindowId,
    int Index,
    string Title,
    string Url,
    bool IsActive,
    bool IsPinned,
    PlaceKind Kind);

public sealed record LiveBrowserSnapshot(
    string Browser,
    DateTimeOffset CapturedAt,
    IReadOnlyList<LiveBrowserTab> Tabs);

public sealed class BrowserBridgeMonitor : IDisposable
{
    private readonly string _bridgePath;
    private readonly Func<LiveBrowserSnapshot, Task> _onSnapshot;
    private readonly FileSystemWatcher _watcher;
    private CancellationTokenSource? _refreshCancellation;

    public BrowserBridgeMonitor(string bridgePath, Func<LiveBrowserSnapshot, Task> onSnapshot)
    {
        _bridgePath = bridgePath;
        _onSnapshot = onSnapshot;
        var directory = Path.GetDirectoryName(bridgePath)
            ?? throw new ArgumentException("Bridge path must have a directory.", nameof(bridgePath));
        Directory.CreateDirectory(directory);
        _watcher = new FileSystemWatcher(directory, Path.GetFileName(bridgePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += BridgeChanged;
        _watcher.Created += BridgeChanged;
        _watcher.Renamed += BridgeChanged;
        if (File.Exists(_bridgePath))
        {
            ScheduleRead();
        }
    }

    public static LiveBrowserSnapshot Parse(string json)
    {
        var source = JsonSerializer.Deserialize<SnapshotFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("The browser snapshot is empty.");
        var tabs = (source.Windows ?? [])
            .SelectMany(window => window.Tabs ?? [])
            .Where(tab => !tab.Incognito &&
                          Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri) &&
                          uri.Scheme is "http" or "https")
            .Select(tab => new LiveBrowserTab(
                tab.Id ?? string.Empty,
                tab.WindowId ?? string.Empty,
                tab.Index,
                tab.Title ?? tab.Url ?? "Untitled tab",
                tab.Url ?? string.Empty,
                tab.Active,
                tab.Pinned,
                IsConversation(tab.Url) ? PlaceKind.Conversation : PlaceKind.BrowserTab))
            .OrderBy(tab => tab.WindowId, StringComparer.Ordinal)
            .ThenBy(tab => tab.Index)
            .ToArray();
        return new LiveBrowserSnapshot(
            source.Browser ?? "chromium",
            source.CapturedAt,
            tabs);
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
    }

    private void BridgeChanged(object sender, FileSystemEventArgs e) => ScheduleRead();

    private void ScheduleRead()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        _ = ReadAfterWriteSettlesAsync(_refreshCancellation.Token);
    }

    private async Task ReadAfterWriteSettlesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_bridgePath, cancellationToken);
                    await _onSnapshot(Parse(json));
                    return;
                }
                catch (IOException) when (attempt < 3)
                {
                    await Task.Delay(40 * (attempt + 1), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool IsConversation(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Host.EndsWith("chatgpt.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith("claude.ai", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/chat/", StringComparison.OrdinalIgnoreCase));

    private sealed record SnapshotFile(string? Browser, DateTimeOffset CapturedAt, BrowserWindowFile[]? Windows);

    private sealed record BrowserWindowFile(string? Id, bool Focused, BrowserTabFile[]? Tabs);

    private sealed record BrowserTabFile(
        string? Id,
        string? WindowId,
        int Index,
        string? Title,
        string? Url,
        bool Active,
        bool Pinned,
        bool Incognito);
}
