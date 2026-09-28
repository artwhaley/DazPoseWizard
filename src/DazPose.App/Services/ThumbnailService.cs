using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace DazPose.App.Services;

/// <summary>Decodes small DAZ preview thumbnails on demand and keeps a bounded in-memory cache.</summary>
public sealed class ThumbnailService : IDisposable
{
    private readonly int _capacity;
    private readonly SemaphoreSlim _decodeLimit = new(4, 4);
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LinkedListNode<(string Key, Bitmap Image)>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<(string Key, Bitmap Image)> _lru = new();
    private readonly object _lock = new();
    private int _disposed;

    public ThumbnailService(int capacity = 96) => _capacity = Math.Max(8, capacity);

    public async Task<Bitmap?> LoadAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0 || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var key = Path.GetFullPath(path) + "|" + File.GetLastWriteTimeUtc(path).Ticks;
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                _lru.Remove(cached);
                _lru.AddFirst(cached);
                return cached.Value.Image;
            }
        }

        var task = _inFlight.GetOrAdd(key, _ => DecodeAndCacheAsync(key, path));
        _ = task.ContinueWith(completed =>
        {
            if (_inFlight.TryGetValue(key, out var current) && ReferenceEquals(current, completed))
                _inFlight.TryRemove(key, out _);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try { return await task.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { throw; }
    }

    private async Task<Bitmap?> DecodeAndCacheAsync(string key, string path)
    {
        await _decodeLimit.WaitAsync();
        try
        {
            Bitmap? image = await Task.Run(() =>
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    return Bitmap.DecodeToWidth(stream, 360, BitmapInterpolationMode.HighQuality);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    return null;
                }
            });
            if (image is null) return null;
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    image.Dispose();
                    return null;
                }
                if (_cache.TryGetValue(key, out var existing))
                {
                    image.Dispose();
                    _lru.Remove(existing);
                    _lru.AddFirst(existing);
                    return existing.Value.Image;
                }
                var node = _lru.AddFirst((key, image));
                _cache[key] = node;
                while (_cache.Count > _capacity)
                {
                    var last = _lru.Last!;
                    _lru.RemoveLast();
                    _cache.Remove(last.Value.Key);
                    last.Value.Image.Dispose();
                }
            }
            return image;
        }
        finally { _decodeLimit.Release(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        lock (_lock)
        {
            foreach (var node in _lru) node.Image.Dispose();
            _lru.Clear();
            _cache.Clear();
        }
    }
}
