using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FormsScreen = System.Windows.Forms.Screen;
using WpfWindow = System.Windows.Window;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class WindowPlacementResolver
{
    public const double DefaultMargin = 24;

    private const double MinimumVisibleSize = 48;

    public WindowPlacement ResolveForWindow(
        WpfWindow window,
        AppSettings settings,
        IntPtr fallbackWindowHandle)
    {
        var workAreas = GetWorkAreas(window);
        var fallbackWorkArea = fallbackWindowHandle == IntPtr.Zero
            ? null
            : ToWorkArea(FormsScreen.FromHandle(fallbackWindowHandle), GetTransformFromDevice(window));

        return Resolve(settings, GetWindowWidth(window), GetWindowHeight(window), workAreas, fallbackWorkArea);
    }

    public string GetScreenDeviceName(WpfWindow window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        return handle == IntPtr.Zero
            ? string.Empty
            : FormsScreen.FromHandle(handle).DeviceName;
    }

    public static WindowPlacement Resolve(
        AppSettings settings,
        double windowWidth,
        double windowHeight,
        IReadOnlyList<WindowWorkArea> workAreas,
        WindowWorkArea? fallbackWorkArea = null)
    {
        if (workAreas.Count == 0)
        {
            return new WindowPlacement(0, 0, string.Empty);
        }

        if (settings.LastWindowLeft is { } savedLeft
            && settings.LastWindowTop is { } savedTop
            && !IsUnsetOrWpfManualOrigin(savedLeft, savedTop)
            && !IsLikelyShellFirstCorruption(savedLeft, savedTop, workAreas))
        {
            var savedWorkArea = workAreas.FirstOrDefault(workArea =>
                IsPlacementVisible(savedLeft, savedTop, windowWidth, windowHeight, workArea));

            if (savedWorkArea is not null)
            {
                return new WindowPlacement(savedLeft, savedTop, savedWorkArea.DeviceName);
            }
        }

        return CreateDefaultBottomRight(workAreas, fallbackWorkArea, windowWidth, windowHeight);
    }

    private static WindowPlacement CreateDefaultBottomRight(
        IReadOnlyList<WindowWorkArea> workAreas,
        WindowWorkArea? fallbackWorkArea,
        double windowWidth,
        double windowHeight)
    {
        var defaultWorkArea = fallbackWorkArea
            ?? workAreas.FirstOrDefault(workArea => workArea.IsPrimary)
            ?? workAreas[0];
        var left = Math.Max(
            defaultWorkArea.Left + DefaultMargin,
            defaultWorkArea.Right - windowWidth - DefaultMargin);
        var top = Math.Max(
            defaultWorkArea.Top + DefaultMargin,
            defaultWorkArea.Bottom - windowHeight - DefaultMargin);

        return new WindowPlacement(left, top, defaultWorkArea.DeviceName);
    }

    private static IReadOnlyList<WindowWorkArea> GetWorkAreas(WpfWindow window)
    {
        var transformFromDevice = GetTransformFromDevice(window);

        return FormsScreen.AllScreens
            .Select(screen => ToWorkArea(screen, transformFromDevice))
            .ToArray();
    }

    private static WindowWorkArea ToWorkArea(FormsScreen screen, Matrix transformFromDevice)
    {
        var area = screen.WorkingArea;
        var topLeft = transformFromDevice.Transform(new Point(area.Left, area.Top));
        var bottomRight = transformFromDevice.Transform(new Point(area.Right, area.Bottom));

        return new WindowWorkArea(
            screen.DeviceName,
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y,
            screen.Primary);
    }

    private static Matrix GetTransformFromDevice(WpfWindow window)
    {
        return PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
    }

    private static double GetWindowWidth(WpfWindow window)
    {
        if (window.ActualWidth > 0)
        {
            return window.ActualWidth;
        }

        return double.IsNaN(window.Width) || window.Width <= 0 ? window.MinWidth : window.Width;
    }

    private static double GetWindowHeight(WpfWindow window)
    {
        if (window.ActualHeight > 0)
        {
            return window.ActualHeight;
        }

        return double.IsNaN(window.Height) || window.Height <= 0 ? window.MinHeight : window.Height;
    }

    private static bool IsPlacementVisible(
        double left,
        double top,
        double windowWidth,
        double windowHeight,
        WindowWorkArea workArea)
    {
        var visibleWidth = Math.Min(left + windowWidth, workArea.Right) - Math.Max(left, workArea.Left);
        var visibleHeight = Math.Min(top + windowHeight, workArea.Bottom) - Math.Max(top, workArea.Top);

        return visibleWidth >= Math.Min(windowWidth, MinimumVisibleSize)
            && visibleHeight >= Math.Min(windowHeight, MinimumVisibleSize);
    }

    /// <summary>
    /// Delegates to <see cref="WindowPlacementRules.IsUnsetOrWpfManualOrigin"/>.
    /// Kept for existing Infrastructure call sites and tests.
    /// </summary>
    internal static bool IsUnsetOrWpfManualOrigin(double left, double top)
    {
        return WindowPlacementRules.IsUnsetOrWpfManualOrigin(left, top);
    }

    /// <summary>
    /// Shell-first startup could leave the window near the primary work-area top-left and save that
    /// (e.g. 130,130). WPF Manual default is on the primary origin, not other monitors.
    /// </summary>
    internal static bool IsLikelyShellFirstCorruption(
        double left,
        double top,
        IReadOnlyList<WindowWorkArea> workAreas)
    {
        if (workAreas.Count == 0)
        {
            return false;
        }

        // Only primary: secondary top-left (e.g. 1920+x) must still be a valid user save.
        var band = WindowPlacementRules.ShellFirstTopLeftBand;
        var primary = workAreas.FirstOrDefault(workArea => workArea.IsPrimary) ?? workAreas[0];
        return left >= primary.Left - WindowPlacementRules.UnsetOriginEpsilon
            && left <= primary.Left + band
            && top >= primary.Top - WindowPlacementRules.UnsetOriginEpsilon
            && top <= primary.Top + band;
    }
}

public sealed record WindowPlacement(double Left, double Top, string ScreenDeviceName);

public sealed record WindowWorkArea(
    string DeviceName,
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary = false)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}
