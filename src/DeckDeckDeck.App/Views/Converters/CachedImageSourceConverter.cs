using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Views.Converters;

public sealed class CachedImageSourceConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<CacheKey, Lazy<ImageSource?>> Cache = new();
    private static readonly ConcurrentDictionary<string, PathProbe> PathProbeCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan PathProbeCacheDuration = TimeSpan.FromSeconds(2);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (MediaIconResources.TryGetImage(path, out var resourceImage))
        {
            return resourceImage;
        }

        if (!TryCreateCacheKey(path, parameter, out var key))
        {
            return null;
        }

        return GetOrLoadImage(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    public static void PrewarmFiles(IEnumerable<string?> paths, int decodePixelWidth)
    {
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

    private static bool TryCreateCacheKey(string path, object? parameter, out CacheKey key)
    {
        return TryCreateCacheKey(path, ParseDecodePixelWidth(parameter), out key);
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
        if (!File.Exists(fullPath))
        {
            return new PathProbe(false, 0, observedAtTimestamp);
        }

        return new PathProbe(
            true,
            File.GetLastWriteTimeUtc(fullPath).Ticks,
            observedAtTimestamp);
    }

    private static bool IsFresh(long observedAtTimestamp, long now)
    {
        return Stopwatch.GetElapsedTime(observedAtTimestamp, now) <= PathProbeCacheDuration;
    }

    private static ImageSource? LoadImage(CacheKey key)
    {
        try
        {
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
            image.Freeze();
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
            static cacheKey => new Lazy<ImageSource?>(
                () => LoadImage(cacheKey),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var image = lazyImage.Value;
        if (image is null)
        {
            Cache.TryRemove(key, out _);
        }

        return image;
    }

    private static int ParseDecodePixelWidth(object? parameter)
    {
        return parameter switch
        {
            int value => value,
            string value when int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    private readonly record struct CacheKey(string Path, int DecodePixelWidth, long LastWriteTicks);

    private readonly record struct PathProbe(bool Exists, long LastWriteTicks, long ObservedAtTimestamp);
}
