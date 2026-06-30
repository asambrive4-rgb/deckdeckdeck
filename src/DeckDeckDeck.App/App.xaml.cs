using System.IO;
using System.Windows;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace DeckDeckDeck.App;

public partial class App : Application
{
    private IAppInstanceCoordinator? _appInstanceCoordinator;
    private FormsNotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var appInstanceCoordinator = new AppInstanceCoordinator();
            var startupDecision = new AppStartupUseCase(appInstanceCoordinator)
                .ExecuteAsync(AppStartupRequest.Default)
                .GetAwaiter()
                .GetResult();

            switch (startupDecision.Kind)
            {
                case AppStartupDecisionKind.RunPrimary:
                    StartPrimaryInstance(appInstanceCoordinator);
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
        base.OnExit(e);
    }

    private void StartPrimaryInstance(IAppInstanceCoordinator appInstanceCoordinator)
    {
        _appInstanceCoordinator = appInstanceCoordinator;

        var services = AppComposition.CreateDefault();
        var mainWindow = new MainWindow();
        var viewModel = MainViewModelFactory.Create(
            services,
            mainWindow.GetPasteTargetWindowHandle,
            mainWindow.HideAfterPaste,
            mainWindow.EnterEditMode,
            createPasteSelectionCompletion: mainWindow.CreatePasteSelectionCompletion);
        mainWindow.AttachViewModel(viewModel);

        MainWindow = mainWindow;
        CreateTrayIcon();
        _appInstanceCoordinator.StartListening(ShowMainWindow);
        MainWindow.Show();
    }

    private void CreateTrayIcon()
    {
        var menu = new FormsContextMenuStrip();
        menu.Items.Add(new FormsToolStripMenuItem("열기", null, (_, _) => ShowMainWindow()));
        menu.Items.Add(new FormsToolStripMenuItem("종료", null, (_, _) => ExitApplication()));

        _trayIcon = new FormsNotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = CreateTrayIconImage(),
            Text = "DeckDeckDeck 실행 중",
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static DrawingIcon CreateTrayIconImage()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            var icon = DrawingIcon.ExtractAssociatedIcon(processPath);
            if (icon is not null)
            {
                return icon;
            }
        }

        return (DrawingIcon)DrawingSystemIcons.Application.Clone();
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

