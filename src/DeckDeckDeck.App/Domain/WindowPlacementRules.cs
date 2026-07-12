using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

/// <summary>
/// Pure window placement policy (no work-area geometry).
/// Work-area-relative checks stay in the platform resolver.
/// </summary>
public static class WindowPlacementRules
{
    /// <summary>
    /// WPF Manual origin near (0,0) is treated as unset.
    /// </summary>
    public const double UnsetOriginEpsilon = 0.5;

    /// <summary>
    /// Band used to ignore shell-first top-left saves without needing monitor work areas.
    /// </summary>
    public const double ShellFirstTopLeftBand = 200;

    /// <summary>
    /// WPF <c>WindowStartupLocation.Manual</c> without Left/Top lands at (0,0).
    /// Shell-first startup could persist that as "last position".
    /// </summary>
    public static bool IsUnsetOrWpfManualOrigin(double left, double top)
    {
        return Math.Abs(left) < UnsetOriginEpsilon && Math.Abs(top) < UnsetOriginEpsilon;
    }

    /// <summary>
    /// Whether saved coordinates are usable for re-applying placement after shell-first attach.
    /// Unlike work-area-relative corruption checks, this only uses the fixed top-left band.
    /// </summary>
    public static bool HasUsableSavedCoordinates(double left, double top)
    {
        if (IsUnsetOrWpfManualOrigin(left, top))
        {
            return false;
        }

        return left > ShellFirstTopLeftBand || top > ShellFirstTopLeftBand;
    }

    public static bool HasUsableSavedWindowPlacement(AppSettings settings)
    {
        if (settings.LastWindowLeft is not { } left
            || settings.LastWindowTop is not { } top)
        {
            return false;
        }

        return HasUsableSavedCoordinates(left, top);
    }
}
