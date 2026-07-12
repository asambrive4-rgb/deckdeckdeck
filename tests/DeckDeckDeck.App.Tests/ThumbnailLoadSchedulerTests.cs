using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views.Imaging;
using Xunit.Abstractions;

namespace DeckDeckDeck.App.Tests;

public sealed class ThumbnailLoadSchedulerTests
{
    private readonly ITestOutputHelper _output;

    public ThumbnailLoadSchedulerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ScheduleGridAppliesFrozenThumbnailSourceFromFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ddd-thumb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var imagePath = Path.Combine(tempDir, "slot.png");
            WriteTinyPng(imagePath);

            var slots = SlotKeyCatalog.All.Select(key =>
                key == SlotKey.Numpad1
                    ? new SlotViewModel(key, "With image", imagePath, isEnabledSlot: true, _ => { }, _ => { })
                    : new SlotViewModel(key, null, isEnabledSlot: true, _ => { }))
                .ToList();
            var grid = new NumpadGridViewModel(slots);

            ThumbnailLoadScheduler.ScheduleGrid(grid, decodePixelWidth: 42);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (grid.Numpad1.ThumbnailSource is null && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }

            Assert.NotNull(grid.Numpad1.ThumbnailSource);
            Assert.True(grid.Numpad1.ThumbnailSource.IsFrozen);
            Assert.Null(grid.Numpad2.ThumbnailSource);
            _output.WriteLine("ScheduleGrid applied frozen ThumbnailSource for Numpad1.");
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void StaleGenerationDoesNotOverwriteThumbnailSource()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ddd-thumb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var imagePath = Path.Combine(tempDir, "slot.png");
            WriteTinyPng(imagePath);

            var slot = new SlotViewModel(
                SlotKey.Numpad1,
                "Slot",
                imagePath,
                isEnabledSlot: true,
                _ => { },
                _ => { });

            var gen1 = slot.BeginThumbnailLoadGeneration();
            var gen2 = slot.BeginThumbnailLoadGeneration();
            Assert.True(slot.IsCurrentThumbnailLoad(gen2));
            Assert.False(slot.IsCurrentThumbnailLoad(gen1));

            // Simulate an older decode completing after a newer request started.
            slot.ApplyThumbnailSource(null);
            if (slot.IsCurrentThumbnailLoad(gen1))
            {
                slot.ApplyThumbnailSource(CreateFrozenBitmap(imagePath));
            }

            Assert.Null(slot.ThumbnailSource);

            if (slot.IsCurrentThumbnailLoad(gen2))
            {
                slot.ApplyThumbnailSource(CreateFrozenBitmap(imagePath));
            }

            Assert.NotNull(slot.ThumbnailSource);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static ImageSource CreateFrozenBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.DecodePixelWidth = 42;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void WriteTinyPng(string path)
    {
        // 1x1 opaque PNG
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(path, bytes);
    }
}
