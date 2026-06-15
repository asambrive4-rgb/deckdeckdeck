using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.Windows.Media;

namespace DeckDeckDeck.App.Infrastructure.Storage;

public static class MediaIconResources
{
    private const string ResourcePrefix = "resource:";
    private static readonly string PlayPauseIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.PlayPause);
    private static readonly string PreviousTrackIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.PreviousTrack);
    private static readonly string NextTrackIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.NextTrack);
    private static readonly string StopIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.Stop);
    private static readonly string MuteIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.Mute);
    private static readonly string VolumeUpIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.VolumeUp);
    private static readonly string VolumeDownIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.VolumeDown);
    private static readonly string ShuffleIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.ToggleShuffle);
    private static readonly string RepeatIcon =
        MediaIconResourcePaths.GetIconResourcePath(SnippetMediaCommand.CycleRepeat);

    private static readonly IReadOnlyDictionary<string, ImageSource> Images = CreateImages();

    public static string GetIconResourcePath(SnippetMediaCommand? command)
    {
        return MediaIconResourcePaths.GetIconResourcePath(command);
    }

    public static bool TryGetImage(string value, out ImageSource? image)
    {
        image = null;
        if (!value.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return Images.TryGetValue(value, out image);
    }

    private static IReadOnlyDictionary<string, ImageSource> CreateImages()
    {
        return new Dictionary<string, ImageSource>
        {
            [PlayPauseIcon] = CreateIcon("M5 3 L19 12 L5 21 Z"),
            [PreviousTrackIcon] = CreateIcon("M19 20 L9 12 L19 4 Z", "M5 19 L5 5"),
            [NextTrackIcon] = CreateIcon("M5 4 L15 12 L5 20 Z", "M19 5 L19 19"),
            [StopIcon] = CreateIcon("M5 5 H19 V19 H5 Z"),
            [MuteIcon] = CreateIcon("M11 5 L6 9 H2 V15 H6 L11 19 Z", "M17 9 L23 15", "M23 9 L17 15"),
            [VolumeUpIcon] = CreateIcon(
                "M11 5 L6 9 H2 V15 H6 L11 19 Z",
                "M15.5 8.5 C17.5 10.5 17.5 13.5 15.5 15.5",
                "M18.5 5.5 C22.5 9.5 22.5 14.5 18.5 18.5"),
            [VolumeDownIcon] = CreateIcon(
                "M11 5 L6 9 H2 V15 H6 L11 19 Z",
                "M15.5 8.5 C17.5 10.5 17.5 13.5 15.5 15.5"),
            [ShuffleIcon] = CreateIcon(
                "M2 6 H5 C8 6 9 18 13 18 H16",
                "M2 18 H5 C8 18 9 6 13 6 H16",
                "M16 3 L21 6 L16 9",
                "M16 15 L21 18 L16 21"),
            [RepeatIcon] = CreateIcon(
                "M17 2 L21 6 L17 10",
                "M3 11 V10 C3 7.8 4.8 6 7 6 H21",
                "M7 22 L3 18 L7 14",
                "M21 13 V14 C21 16.2 19.2 18 17 18 H3")
        };
    }

    private static DrawingImage CreateIcon(params string[] pathData)
    {
        var stroke = new SolidColorBrush(Color.FromRgb(0xA7, 0x77, 0x43));
        stroke.Freeze();

        var pen = new Pen(stroke, 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        var group = new DrawingGroup();
        foreach (var path in pathData)
        {
            group.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse(path)));
        }

        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();

        return image;
    }
}
