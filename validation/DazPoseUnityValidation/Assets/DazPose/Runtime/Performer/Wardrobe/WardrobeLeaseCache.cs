using System;
using System.Collections.Generic;
using System.Linq;

namespace DazPose.Performer
{
    /// <summary>Per-owner bounded MRU resource cache. Active and scoped snapshot pins are never evicted.</summary>
    public sealed class WardrobeLeaseCache<T> : IDisposable where T : class
    {
        private sealed class Entry { public T Value; public long LastUse; public int Pins; }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Action<T> dispose;
        private readonly int unpinnedCapacity;
        private long clock;
        private string activeKey;
        private bool disposed;

        public WardrobeLeaseCache(int unpinnedCapacity, Action<T> dispose)
        {
            if (unpinnedCapacity < 1) throw new ArgumentOutOfRangeException(nameof(unpinnedCapacity));
            this.unpinnedCapacity = unpinnedCapacity;
            this.dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }
        public int Count => entries.Count;
        public int UnpinnedCount => entries.Count(pair => pair.Value.Pins == 0 && pair.Key != activeKey);
        public string ActiveKey => activeKey;

        public T GetOrCreate(string key, Func<T> factory)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A stable cache key is required.", nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!entries.TryGetValue(key, out var entry))
            {
                var value = factory();
                if (value == null) throw new InvalidOperationException("Wardrobe cache factory returned null for " + key + ".");
                entry = new Entry { Value = value };
                entries.Add(key, entry);
            }
            entry.LastUse = ++clock;
            Trim();
            return entry.Value;
        }

        public bool TryGet(string key, out T value)
        {
            ThrowIfDisposed();
            if (entries.TryGetValue(key ?? string.Empty, out var entry))
            { entry.LastUse = ++clock; value = entry.Value; return true; }
            value = null; return false;
        }

        public void SetActive(string key)
        {
            ThrowIfDisposed();
            if (!string.IsNullOrEmpty(key) && !entries.ContainsKey(key)) throw new KeyNotFoundException("Cannot activate an uncached wardrobe resource: " + key);
            activeKey = string.IsNullOrEmpty(key) ? null : key;
            if (activeKey != null) entries[activeKey].LastUse = ++clock;
            Trim();
        }

        public IDisposable Pin(string key)
        {
            ThrowIfDisposed();
            if (!entries.TryGetValue(key ?? string.Empty, out var entry)) throw new KeyNotFoundException("Cannot pin an uncached wardrobe resource: " + key);
            ++entry.Pins; entry.LastUse = ++clock;
            return new PinToken(this, key);
        }

        private void ReleasePin(string key)
        {
            if (disposed || !entries.TryGetValue(key, out var entry)) return;
            if (entry.Pins <= 0) throw new InvalidOperationException("Wardrobe resource pin underflow: " + key);
            --entry.Pins; Trim();
        }
        private void Trim()
        {
            while (UnpinnedCount > unpinnedCapacity)
            {
                var victim = entries.Where(pair => pair.Key != activeKey && pair.Value.Pins == 0)
                    .OrderBy(pair => pair.Value.LastUse).FirstOrDefault();
                if (victim.Value == null) return;
                entries.Remove(victim.Key); dispose(victim.Value.Value);
            }
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(WardrobeLeaseCache<T>)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entry in entries.Values) dispose(entry.Value);
            entries.Clear(); activeKey = null;
        }
        private sealed class PinToken : IDisposable
        {
            private WardrobeLeaseCache<T> owner; private readonly string key;
            public PinToken(WardrobeLeaseCache<T> cache, string stableKey) { owner = cache; key = stableKey; }
            public void Dispose() { var cache = owner; owner = null; cache?.ReleasePin(key); }
        }
    }
}
