using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Views.Imaging;

/// <summary>
/// Decodes slot thumbnails on a dedicated STA worker (WPF <see cref="System.Windows.Media.Imaging.BitmapImage"/>),
/// caches via <see cref="FrozenImageCache"/>, then applies frozen sources on the UI dispatcher.
/// </summary>
public static class ThumbnailLoadScheduler
{
    public const int DefaultSlotDecodePixelWidth = 42;

    private static readonly BlockingCollection<Action> WorkQueue = new();
    private static int _workerStarted;

    public static void ScheduleGrid(
        NumpadGridViewModel? grid,
        int decodePixelWidth = DefaultSlotDecodePixelWidth)
    {
        if (grid is null)
        {
            return;
        }

        var requests = new List<SlotLoadRequest>(grid.Slots.Count);
        foreach (var slot in grid.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.ThumbnailPath))
            {
                continue;
            }

            requests.Add(new SlotLoadRequest(
                slot,
                slot.BeginThumbnailLoadGeneration(),
                slot.ThumbnailPath));
        }

        if (requests.Count == 0)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        EnsureWorker();
        WorkQueue.Add(() => DecodeAndApply(requests, decodePixelWidth, dispatcher));
    }

    public static void SchedulePaths(
        IEnumerable<string?> paths,
        int decodePixelWidth = DefaultSlotDecodePixelWidth)
    {
        var snapshot = paths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray()
            ?? [];
        if (snapshot.Length == 0)
        {
            return;
        }

        EnsureWorker();
        WorkQueue.Add(() => FrozenImageCache.PrewarmFiles(snapshot, decodePixelWidth));
    }

    private static void EnsureWorker()
    {
        if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) != 0)
        {
            return;
        }

        var thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "DeckDeckDeck.ThumbnailLoader"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void WorkerLoop()
    {
        foreach (var work in WorkQueue.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
            catch
            {
                // Individual jobs must not stop the loader thread.
            }
        }
    }

    private static void DecodeAndApply(
        IReadOnlyList<SlotLoadRequest> requests,
        int decodePixelWidth,
        Dispatcher? dispatcher)
    {
        foreach (var request in requests)
        {
            if (!request.Slot.IsCurrentThumbnailLoad(request.Generation))
            {
                continue;
            }

            ImageSource? image = null;
            try
            {
                image = FrozenImageCache.GetOrLoad(request.Path, decodePixelWidth);
            }
            catch
            {
                // Non-fatal: leave ThumbnailSource null; path-based HasThumbnail still drives layout.
            }

            ApplyOnUi(request.Slot, request.Generation, image, dispatcher);
        }
    }

    private static void ApplyOnUi(
        SlotViewModel slot,
        int generation,
        ImageSource? image,
        Dispatcher? dispatcher)
    {
        void Apply()
        {
            if (!slot.IsCurrentThumbnailLoad(generation))
            {
                return;
            }

            slot.ApplyThumbnailSource(image);
        }

        // Frozen ImageSource is free-threaded; only the live UI dispatcher needs marshalling.
        // Dead/shutdown dispatchers (e.g. after WPF test threads) must not swallow updates.
        if (dispatcher is null
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished
            || dispatcher.CheckAccess())
        {
            Apply();
            return;
        }

        try
        {
            _ = dispatcher.BeginInvoke(Apply, DispatcherPriority.Background);
        }
        catch
        {
            Apply();
        }
    }

    private readonly record struct SlotLoadRequest(
        SlotViewModel Slot,
        int Generation,
        string Path);
}
