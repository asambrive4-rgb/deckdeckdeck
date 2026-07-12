using System.Windows;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Diagnostics;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views.Imaging;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;

namespace DeckDeckDeck.App;

public partial class App : Application
{
    private IAppInstanceCoordinator? _appInstanceCoordinator;
    private FormsNotifyIcon? _trayIcon;
    private AppIconProvider? _appIconProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var startupTiming = new StartupTimingLog();
            var appInstanceCoordinator = new AppInstanceCoordinator();
            AppStartupDecision startupDecision;
            using (startupTiming.Measure("app instance decision"))
            {
                startupDecision = new AppStartupUseCase(appInstanceCoordinator)
                    .ExecuteAsync(AppStartupRequest.Default)
                    .GetAwaiter()
                    .GetResult();
            }

            switch (startupDecision.Kind)
            {
                case AppStartupDecisionKind.RunPrimary:
                    StartPrimaryInstance(appInstanceCoordinator, startupTiming);
                    break;
                case AppStartupDecisionKind.ForwardedToPrimaryAndExit:
                    appInstanceCoordinator.Dispose();
                    Shutdown();
                    break;
                case AppStartupDecisionKind.FailedButExit:
                    appInstanceCoordinator.Dispose();
                    ReportStartupDecisionFailure(startupDecision);
                    Shutdown();
                    break;
            }
        }
        catch (Exception ex)
        {
            _appInstanceCoordinator?.Dispose();
            _appInstanceCoordinator = null;
            ReportStartupFailure(ex);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeTrayIcon();
        _appInstanceCoordinator?.Dispose();
        _appInstanceCoordinator = null;
        _appIconProvider = null;
        base.OnExit(e);
    }

    private void StartPrimaryInstance(
        IAppInstanceCoordinator appInstanceCoordinator,
        StartupTimingLog startupTiming)
    {
        _appInstanceCoordinator = appInstanceCoordinator;
        _appIconProvider = new AppIconProvider();

        // 1) Shell first: frame is visible while storage/DB work runs off the UI thread.
        MainWindow mainWindow;
        using (startupTiming.Measure("main window construction"))
        {
            mainWindow = new MainWindow(_appIconProvider, startupTiming);
        }

        MainWindow = mainWindow;
        mainWindow.ContentRendered += (_, _) => startupTiming.Mark("first content rendered");

        using (startupTiming.Measure("shell show"))
        {
            mainWindow.Show();
        }

        // Accept secondary-instance "show" requests as soon as the shell exists.
        _appInstanceCoordinator.StartListening(ShowMainWindow);

        // 2) Heavy I/O off UI → 3) compose + home on UI dispatcher.
        _ = Task.Run(() => PrepareBootstrapThenFinishStartup(mainWindow, startupTiming));
    }

    private void PrepareBootstrapThenFinishStartup(
        MainWindow mainWindow,
        StartupTimingLog startupTiming)
    {
        AppCompositionBootstrap bootstrap;
        try
        {
            bootstrap = AppComposition.PrepareStorageAndDatabase(startupTiming);
        }
        catch (Exception ex)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                ReportStartupFailure(ex);
                Shutdown();
            });
            return;
        }

        _ = Dispatcher.BeginInvoke(
            () => FinishStartupOnUiThread(mainWindow, bootstrap, startupTiming),
            DispatcherPriority.Normal);
    }

    private void FinishStartupOnUiThread(
        MainWindow mainWindow,
        AppCompositionBootstrap bootstrap,
        StartupTimingLog startupTiming)
    {
        try
        {
            AppComposition services;
            using (startupTiming.Measure("service composition"))
            {
                services = AppComposition.CreateFromBootstrap(bootstrap, startupTiming);
            }

            MainViewModel viewModel;
            using (startupTiming.Measure("main view model construction"))
            {
                viewModel = MainViewModelFactory.Create(
                    services,
                    mainWindow.GetPasteTargetWindowHandle,
                    mainWindow.HideAfterPaste,
                    mainWindow.EnterEditMode,
                    createPasteSelectionCompletion: mainWindow.CreatePasteSelectionCompletion);
            }

            mainWindow.AttachViewModel(viewModel);
            RegisterThumbnailPrewarm(viewModel);

            try
            {
                using (startupTiming.Measure("initial home load"))
                {
                    viewModel.InitializeHome();
                }
            }
            catch (Exception ex)
            {
                services.FileLogger?.Log("Initial home loading failed.", ex);
                viewModel.ReportBackgroundStatus("홈 화면을 불러오지 못했습니다.");
            }

            // Prewarm as soon as home slots exist (no wait for ApplicationIdle).
            QueueThumbnailPrewarm(viewModel);

            using (startupTiming.Measure("tray icon creation"))
            {
                if (_appIconProvider is not null)
                {
                    CreateTrayIcon(_appIconProvider);
                }
            }

            startupTiming.Mark("startup ready");
            startupTiming.FlushAsync(services.FileLogger);
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
            Shutdown();
        }
    }

    private void CreateTrayIcon(AppIconProvider iconProvider)
    {
        var menu = new FormsContextMenuStrip();
        menu.Items.Add(new FormsToolStripMenuItem("열기", null, (_, _) => ShowMainWindow()));
        menu.Items.Add(new FormsToolStripMenuItem("종료", null, (_, _) => ExitApplication()));

        _trayIcon = new FormsNotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = iconProvider.CreateTrayIcon(),
            Text = "DeckDeckDeck 실행 중",
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static void RegisterThumbnailPrewarm(MainViewModel viewModel)
    {
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentViewModel))
            {
                QueueThumbnailPrewarm(viewModel);
            }
        };
    }

    private static void QueueThumbnailPrewarm(MainViewModel viewModel)
    {
        try
        {
            // Decode off UI and push frozen ImageSource onto each visible slot.
            ThumbnailLoadScheduler.ScheduleGrid(viewModel.GetVisibleNumpadGrid());
        }
        catch
        {
            // Thumbnail scheduling must never break navigation.
        }
    }

    private void ExitApplication()
    {
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.AllowCloseForExit();
        }

        Shutdown();
    }

    private void ShowMainWindow()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(ShowMainWindow);
            return;
        }

        if (MainWindow is null)
        {
            return;
        }

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        if (!MainWindow.IsVisible)
        {
            MainWindow.Show();
        }

        MainWindow.Activate();
        MainWindow.Focus();
    }

    private static void ReportStartupDecisionFailure(AppStartupDecision decision)
    {
        LogStartupFailure(decision.Message ?? "DeckDeckDeck startup was cancelled.", decision.Detail);
        MessageBox.Show(
            "DeckDeckDeck이 이미 실행 중이지만 기존 창을 열지 못했습니다. 트레이 아이콘에서 열기를 선택해 주세요.",
            "DeckDeckDeck 시작 안내",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void ReportStartupFailure(Exception exception)
    {
        LogStartupFailure("DeckDeckDeck startup failed.", exception);
        MessageBox.Show(
            "DeckDeckDeck 시작 중 문제가 발생했습니다. 앱을 다시 실행해 보고, 문제가 계속되면 로그를 확인해 주세요.",
            "DeckDeckDeck 시작 실패",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void LogStartupFailure(string message, string? detail)
    {
        LogStartupFailure(
            string.IsNullOrWhiteSpace(detail)
                ? message
                : $"{message} {detail}",
            exception: null);
    }

    private static void LogStartupFailure(string message, Exception? exception)
    {
        try
        {
            new FileLogger(new AppStoragePaths()).Log(message, exception);
        }
        catch
        {
            // Startup logging must not cause another startup failure.
        }
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Icon?.Dispose();
        _trayIcon.Dispose();
        _trayIcon = null;
    }
}
