using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeckDeckDeck.App.Infrastructure.Storage;

internal sealed class ThumbnailGenerator
{
    private const int ThumbnailMaxPixels = 96;

    public void CreateThumbnail(string sourcePath, string thumbnailPath)
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.UriSource = new Uri(sourcePath, UriKind.Absolute);
        source.EndInit();
        source.Freeze();

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            throw new InvalidOperationException("이미지를 불러올 수 없습니다.");
        }

        var longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        var scale = (double)ThumbnailMaxPixels / longestSide;
        var thumbnail = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        thumbnail.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(thumbnail));

        using var stream = File.Create(thumbnailPath);
        encoder.Save(stream);
    }
}

