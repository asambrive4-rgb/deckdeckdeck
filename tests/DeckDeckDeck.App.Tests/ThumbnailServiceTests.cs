using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using DeckDeckDeck.App.Views.Converters;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class ThumbnailServiceTests
{
    [Fact]
    public void ThumbnailServiceCopiesImageAndCreatesThumbnail()
    {
        var services = CreateServices();
        var sourcePath = CreateTinyBmp(services.Storage.TempPath);

        var storedImage = RunInSta(() => services.ThumbnailService.StoreImage(sourcePath));

        Assert.True(File.Exists(storedImage.ImagePath));
        Assert.True(File.Exists(storedImage.ThumbnailPath));
        Assert.EndsWith(".bmp", storedImage.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", storedImage.ThumbnailPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            Path.GetFullPath(services.Storage.ImageOriginalsPath),
            Path.GetFullPath(storedImage.ImagePath),
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            Path.GetFullPath(services.Storage.ImageThumbnailsPath),
            Path.GetFullPath(storedImage.ThumbnailPath),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThumbnailServiceRejectsMissingImage()
    {
        var services = CreateServices();
        var missingPath = Path.Combine(services.Storage.TempPath, "missing.png");

        var exception = Assert.Throws<InvalidOperationException>(() => services.ThumbnailService.StoreImage(missingPath));

        Assert.Contains("존재하지 않습니다", exception.Message);
    }

    [Fact]
    public void ThumbnailServiceRejectsUnsupportedImageType()
    {
        var services = CreateServices();
        var textPath = Path.Combine(services.Storage.TempPath, "not-image.txt");
        File.WriteAllText(textPath, "not an image");

        var exception = Assert.Throws<InvalidOperationException>(() => services.ThumbnailService.StoreImage(textPath));

        Assert.Contains("지원하지 않는 이미지 형식", exception.Message);
    }

    [Fact]
    public void SlotViewModelReportsThumbnailWhenPathIsPresent()
    {
        var slot = new SlotViewModel(
            SlotKey.Numpad1,
            "Writing",
            "thumbnail.png",
            true,
            _ => { },
            _ => { });

        Assert.True(slot.HasThumbnail);
        Assert.Equal("thumbnail.png", slot.ThumbnailPath);
    }

    [Fact]
    public void SlotServiceUsesCustomImageBeforeAutoIcon()
    {
        var autoIconPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllText(autoIconPath, "auto");
        var snippet = new Snippet
        {
            SlotKey = SlotKey.Numpad1,
            Title = "Tool",
            ActionType = SnippetActionType.LaunchFile,
            LaunchPath = @"C:\tool.exe",
            SlotImageMode = SlotImageMode.Custom,
            ThumbnailPath = "custom-thumbnail.png",
            AutoIconPath = autoIconPath
        };

        var grid = new SlotService().BuildSnippetGrid(
            [snippet],
            new AppSettings(),
            (_, _) => { },
            (_, _) => { });

        Assert.Equal("custom-thumbnail.png", grid.Numpad1.ThumbnailPath);
    }

    [Fact]
    public void SlotServiceUsesAutoIconWhenCustomImageIsMissing()
    {
        var autoIconPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllText(autoIconPath, "auto");
        var snippet = new Snippet
        {
            SlotKey = SlotKey.Numpad1,
            Title = "Tool",
            ActionType = SnippetActionType.LaunchFile,
            LaunchPath = @"C:\tool.exe",
            SlotImageMode = SlotImageMode.Auto,
            AutoIconPath = autoIconPath
        };

        var grid = new SlotService().BuildSnippetGrid(
            [snippet],
            new AppSettings(),
            (_, _) => { },
            (_, _) => { });

        Assert.Equal(autoIconPath, grid.Numpad1.ThumbnailPath);
    }

    [Fact]
    public void SlotServiceUsesMediaDefaultIconWhenCustomImageIsMissing()
    {
        var snippet = new Snippet
        {
            SlotKey = SlotKey.Numpad1,
            Title = "Mute",
            ActionType = SnippetActionType.MediaAction,
            MediaProvider = SnippetMediaProvider.System,
            MediaCommand = SnippetMediaCommand.Mute,
            SlotImageMode = SlotImageMode.Auto
        };

        var grid = new SlotService().BuildSnippetGrid(
            [snippet],
            new AppSettings(),
            (_, _) => { },
            (_, _) => { });

        Assert.Equal(MediaIconResources.GetIconResourcePath(SnippetMediaCommand.Mute), grid.Numpad1.ThumbnailPath);
        Assert.True(grid.Numpad1.HasThumbnail);
    }

    [Fact]
    public void CachedImageSourceConverterLoadsMediaDefaultIcon()
    {
        var converter = new CachedImageSourceConverter();

        var image = converter.Convert(
            MediaIconResources.GetIconResourcePath(SnippetMediaCommand.PlayPause),
            typeof(ImageSource),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.IsAssignableFrom<ImageSource>(image);
    }

    [Fact]
    public void SlotServiceLeavesThumbnailEmptyWhenNoImageIsAvailable()
    {
        var snippet = new Snippet
        {
            SlotKey = SlotKey.Numpad1,
            Title = "Tool",
            ActionType = SnippetActionType.LaunchFile,
            LaunchPath = @"C:\tool.exe",
            SlotImageMode = SlotImageMode.None
        };

        var grid = new SlotService().BuildSnippetGrid(
            [snippet],
            new AppSettings(),
            (_, _) => { },
            (_, _) => { });

        Assert.Null(grid.Numpad1.ThumbnailPath);
        Assert.False(grid.Numpad1.HasThumbnail);
    }
}
