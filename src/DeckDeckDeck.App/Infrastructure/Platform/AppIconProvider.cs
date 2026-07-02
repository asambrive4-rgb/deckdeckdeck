using System.IO;
using System.Collections;
using System.Resources;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class AppIconProvider
{
    private const string IconResourceName = "assets/brand/deckduck/app-icon.ico";
    private const string IconResourceUri =
        "pack://application:,,,/DeckDeckDeck.App;component/assets/brand/deckduck/app-icon.ico";

    private readonly object _syncRoot = new();
    private byte[]? _iconBytes;
    private ImageSource? _windowIcon;

    public ImageSource? GetWindowIcon()
    {
        lock (_syncRoot)
        {
            if (_windowIcon is not null)
            {
                return _windowIcon;
            }

            var iconBytes = GetIconBytesLocked();
            if (iconBytes is null)
            {
                return null;
            }

            try
            {
                using var stream = new MemoryStream(iconBytes);
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames
                    .OrderByDescending(item => item.PixelWidth)
                    .ThenByDescending(item => item.PixelHeight)
                    .FirstOrDefault();
                frame?.Freeze();
                _windowIcon = frame;
                return _windowIcon;
            }
            catch
            {
                return null;
            }
        }
    }

    public DrawingIcon CreateTrayIcon()
    {
        lock (_syncRoot)
        {
            var iconBytes = GetIconBytesLocked();
            if (iconBytes is not null)
            {
                try
                {
                    using var stream = new MemoryStream(iconBytes);
                    using var icon = new DrawingIcon(stream);
                    return (DrawingIcon)icon.Clone();
                }
                catch
                {
                    // Fall back to the executable or system icon below.
                }
            }
        }

        return CreateFallbackIcon();
    }

    private byte[]? GetIconBytesLocked()
    {
        if (_iconBytes is not null)
        {
            return _iconBytes;
        }

        _iconBytes = LoadIconBytesFromAssemblyResource()
            ?? LoadIconBytesFromApplicationResource();
        return _iconBytes;
    }

    private static byte[]? LoadIconBytesFromAssemblyResource()
    {
        try
        {
            var assembly = typeof(AppIconProvider).Assembly;
            var resourceName = $"{assembly.GetName().Name}.g.resources";
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                return null;
            }

            using var reader = new ResourceReader(resourceStream);
            foreach (DictionaryEntry entry in reader)
            {
                if (entry.Key is not string key
                    || !string.Equals(key, IconResourceName, StringComparison.OrdinalIgnoreCase)
                    || entry.Value is not Stream iconStream)
                {
                    continue;
                }

                using var buffer = new MemoryStream();
                iconStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static byte[]? LoadIconBytesFromApplicationResource()
    {
        try
        {
            var resourceInfo = Application.GetResourceStream(new Uri(IconResourceUri, UriKind.Absolute));
            if (resourceInfo is null)
            {
                return null;
            }

            using var stream = resourceInfo.Stream;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static DrawingIcon CreateFallbackIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            var icon = DrawingIcon.ExtractAssociatedIcon(processPath);
            if (icon is not null)
            {
                return icon;
            }
        }

        return (DrawingIcon)DrawingSystemIcons.Application.Clone();
    }
}
