using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.Views.Converters;

public sealed class CachedImageSourceConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<CacheKey, ImageSource> Cache = new();

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

        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = LoadImage(key);
        if (image is not null)
        {
            Cache.TryAdd(key, image);
        }

        return image;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static bool TryCreateCacheKey(string path, object? parameter, out CacheKey key)
    {
        key = default;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            key = new CacheKey(
                fullPath,
                ParseDecodePixelWidth(parameter),
                File.GetLastWriteTimeUtc(fullPath).Ticks);
            return true;
        }
        catch
        {
            return false;
        }
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
}
