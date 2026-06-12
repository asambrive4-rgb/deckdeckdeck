using System.Windows.Media;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public static class MediaIconResources
{
    private const string ResourcePrefix = "resource:";
    private const string PlayPauseIcon = ResourcePrefix + "Deck.Icon.Media.PlayPause";
    private const string PreviousTrackIcon = ResourcePrefix + "Deck.Icon.Media.PreviousTrack";
    private const string NextTrackIcon = ResourcePrefix + "Deck.Icon.Media.NextTrack";
    private const string StopIcon = ResourcePrefix + "Deck.Icon.Media.Stop";
    private const string MuteIcon = ResourcePrefix + "Deck.Icon.Media.Mute";
    private const string VolumeUpIcon = ResourcePrefix + "Deck.Icon.Media.VolumeUp";
    private const string VolumeDownIcon = ResourcePrefix + "Deck.Icon.Media.VolumeDown";

    private static readonly IReadOnlyDictionary<string, ImageSource> Images = CreateImages();

    public static string GetIconResourcePath(SnippetMediaCommand? command)
    {
        return command switch
        {
            SnippetMediaCommand.PreviousTrack => PreviousTrackIcon,
            SnippetMediaCommand.NextTrack => NextTrackIcon,
            SnippetMediaCommand.Stop => StopIcon,
            SnippetMediaCommand.Mute => MuteIcon,
            SnippetMediaCommand.VolumeUp => VolumeUpIcon,
            SnippetMediaCommand.VolumeDown => VolumeDownIcon,
            _ => PlayPauseIcon
        };
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
                "M15.5 8.5 C17.5 10.5 17.5 13.5 15.5 15.5")
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
