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
            services.CategoryRepository,
            services.DialogAdapter,
            services.SettingsRepository,
            services.SlotGridViewModelFactory,
            services.SnippetRepository,
            services.ClipboardPasteGateway,
            services.FileLaunchGatewayAdapter,
            services.UrlLaunchGatewayAdapter,
            services.SystemMediaActionGatewayAdapter,
            services.SpotifyMediaActionGatewayAdapter,
            services.TerminalCommandGatewayAdapter,
            services.SpotifyConnectionUseCase,
            services.ClipboardAdapter,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            createPasteSelectionCompletion,
            services.FileLogger,
            services.ImageFileRepository,
            services.SnippetImageResolver,
            services.BackupGateway,
            autoBackupCoordinator,
            services.StoredImagePathResolver);

        return viewModel;
    }
}
