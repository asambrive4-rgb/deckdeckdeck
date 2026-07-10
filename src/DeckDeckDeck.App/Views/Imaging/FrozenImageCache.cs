using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeckDeckDeck.App.Infrastructure.Storage;

namespace DeckDeckDeck.App.Views.Imaging;

/// <summary>
/// Shared decode cache for frozen <see cref="ImageSource"/> values.
/// After freeze, sources may be used from any thread.
/// </summary>
public static class FrozenImageCache
{
    private const int MaxCachedImages = 128;
    private static readonly ConcurrentDictionary<CacheKey, Lazy<ImageSource?>> Cache = new();
    private static readonly ConcurrentDictionary<string, PathProbe> PathProbeCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<CacheKey> InsertionOrder = new();
    private static readonly TimeSpan PathProbeCacheDuration = TimeSpan.FromSeconds(2);
    private static int _approximateCacheCount;

    public static ImageSource? GetOrLoad(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (MediaIconResources.TryGetImage(path, out var resourceImage))
        {
            return resourceImage;
        }

        if (!TryCreateCacheKey(path, decodePixelWidth, out var key))
        {
            return null;
        }

        return GetOrLoadImage(key);
    }

    public static void PrewarmFiles(IEnumerable<string?> paths, int decodePixelWidth)
    {
        if (paths is null)
        {
            return;
        }

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !TryCreateCacheKey(path, decodePixelWidth, out var key)
                || Cache.ContainsKey(key))
            {
                continue;
            }

            _ = GetOrLoadImage(key);
        }
    }

    public static int ParseDecodePixelWidth(object? parameter)
    {
        return parameter switch
        {
            int value => Math.Max(0, value),
            string value when int.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
                => Math.Max(0, parsed),
            _ => 0
        };
    }

    private static bool TryCreateCacheKey(string path, int decodePixelWidth, out CacheKey key)
    {
        key = default;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var probe = GetPathProbe(fullPath);
            if (!probe.Exists)
            {
                return false;
            }

            key = new CacheKey(
                fullPath,
                decodePixelWidth,
                probe.LastWriteTicks);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PathProbe GetPathProbe(string fullPath)
    {
        var now = Stopwatch.GetTimestamp();
        if (PathProbeCache.TryGetValue(fullPath, out var cachedProbe)
            && IsFresh(cachedProbe.ObservedAtTimestamp, now))
        {
            return cachedProbe;
        }

        var refreshedProbe = ProbePath(fullPath, now);
        PathProbeCache[fullPath] = refreshedProbe;
        return refreshedProbe;
    }

    private static PathProbe ProbePath(string fullPath, long observedAtTimestamp)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                return new PathProbe(false, 0, observedAtTimestamp);
            }

            return new PathProbe(
                true,
                File.GetLastWriteTimeUtc(fullPath).Ticks,
                observedAtTimestamp);
        }
        catch
        {
            // Network paths / locked files / permission errors: treat as missing.
            return new PathProbe(false, 0, observedAtTimestamp);
        }
    }

    private static bool IsFresh(long observedAtTimestamp, long now)
    {
        return Stopwatch.GetElapsedTime(observedAtTimestamp, now) <= PathProbeCacheDuration;
    }

    private static ImageSource? LoadImage(CacheKey key)
    {
        try
        {
            if (!File.Exists(key.Path))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(key.Path, UriKind.Absolute);
            if (key.DecodePixelWidth > 0)
            {
                image.DecodePixelWidth = key.DecodePixelWidth;
            }

            image.EndInit();
            if (image.CanFreeze)
            {
                image.Freeze();
            }

            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? GetOrLoadImage(CacheKey key)
    {
        var lazyImage = Cache.GetOrAdd(
            key,
            static cacheKey =>
            {
                Interlocked.Increment(ref _approximateCacheCount);
                InsertionOrder.Enqueue(cacheKey);
                return new Lazy<ImageSource?>(
                    () => LoadImage(cacheKey),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            });

        TrimIfNeeded();

        ImageSource? image;
        try
        {
            image = lazyImage.Value;
        }
        catch
        {
            Cache.TryRemove(key, out _);
            Interlocked.Decrement(ref _approximateCacheCount);
            return null;
        }

        if (image is null)
        {
            // Failed decode: drop so a later attempt can retry after file appears/repairs.
            if (Cache.TryRemove(key, out _))
            {
                Interlocked.Decrement(ref _approximateCacheCount);
            }
        }

        return image;
    }

    private static void TrimIfNeeded()
    {
        while (Volatile.Read(ref _approximateCacheCount) > MaxCachedImages
            && InsertionOrder.TryDequeue(out var oldest))
        {
            if (Cache.TryRemove(oldest, out _))
            {
                Interlocked.Decrement(ref _approximateCacheCount);
            }
        }

        // Path probes are tiny; still bound growth after long sessions.
        if (PathProbeCache.Count > MaxCachedImages * 4)
        {
            PathProbeCache.Clear();
        }
    }

    private readonly record struct CacheKey(string Path, int DecodePixelWidth, long LastWriteTicks);

    private readonly record struct PathProbe(bool Exists, long LastWriteTicks, long ObservedAtTimestamp);
}
