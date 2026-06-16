using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Composition;

internal static class MainViewModelFactory
{
    public static MainViewModel CreateDefault(
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null,
        Func<Action>? createPasteSelectionCompletion = null)
    {
        var services = AppComposition.CreateDefault();
        MainViewModel? viewModel = null;
        var autoBackupCoordinator = services.BackupGateway is null
            ? null
            : new AutoBackupCoordinator(
                services.BackupGateway,
                services.SettingsRepository,
                message => viewModel?.ReportBackgroundStatus(message),
                services.FileLogger);

        viewModel = new MainViewModel(
            services.CreateMainViewModelDependencies(autoBackupCoordinator),
            new MainViewModelCallbacks(
                getPasteTargetWindowHandle ?? (() => IntPtr.Zero),
                hideWindowAfterPaste ?? (() => { }),
                enterEditMode ?? (() => { }),
                createPasteSelectionCompletion
                    ?? (() => completePasteSelection ?? (() => { }))));

        return viewModel;
    }
}
