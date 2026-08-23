using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

[Collection(WpfViewRenderingCollection.Name)]
public sealed class MainWindowInputTests
{
    [Fact]
    public void PreviewNumpadSelectionIsHandledBeforeSlotActionRuns()
    {
        RunOnWpfThread(() =>
        {
            var services = CreateServices();
            services.CategoryRepository.Create(SlotKey.Numpad1, "Commands", null);
            using var viewModel = CreateMainViewModel(services);
            var window = new MainWindow { DataContext = viewModel };
            using var source = new HwndSource(new HwndSourceParameters("MainWindowInputTests"));
            var keyEvent = CreatePreviewKeyDown(source, Key.NumPad1);

            window.RaiseEvent(keyEvent);

            Assert.True(keyEvent.Handled);
            Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

            FlushDispatcher();

            Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        });
    }

    [Fact]
    public void PreviewNumpadSelectionPassesThroughOutsideSlotGrid()
    {
        RunOnWpfThread(() =>
        {
            var services = CreateServices();
            using var viewModel = CreateMainViewModel(services);
            viewModel.TopBarSettingsCommand!.Execute(null);
            var settings = Assert.IsType<SettingsViewModel>(viewModel.CurrentViewModel);
            var window = new MainWindow { DataContext = viewModel };
            using var source = new HwndSource(new HwndSourceParameters("MainWindowInputTests"));
            var keyEvent = CreatePreviewKeyDown(source, Key.NumPad1);

            window.RaiseEvent(keyEvent);

            Assert.False(keyEvent.Handled);
            FlushDispatcher();
            Assert.Same(settings, viewModel.CurrentViewModel);
        });
    }

    [Fact]
    public void CapturedNumpadSelectionIsDeferredUntilHookCanReturn()
    {
        RunOnWpfThread(() =>
        {
            var services = CreateServices();
            services.CategoryRepository.Create(SlotKey.Numpad1, "Commands", null);
            using var viewModel = CreateMainViewModel(services);
            var window = new MainWindow { DataContext = viewModel };

            window.OnNumpadSlotCaptured(
                sender: null,
                new HotkeyPressedEventArgs(SlotKey.Numpad1));

            Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

            FlushDispatcher();

            Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        });
    }

    [Fact]
    public void DirectHotkeyActionIsDeferredUntilHookCanReturn()
    {
        RunOnWpfThread(() =>
        {
            var services = CreateServices();
            var hotkey = services.HotkeyActionRepository.Create(new HotkeyActionSaveData(
                "Direct paste",
                new HotkeyGesture(0x67, HotkeyModifiers.None),
                IsEnabled: true,
                Content: "Paste after hook return",
                Description: null,
                ImagePath: null,
                ThumbnailPath: null,
                SnippetActionType.PasteText,
                LaunchPath: string.Empty,
                SlotImageMode.Auto,
                AutoIcon: null,
                LaunchUrl: null,
                SnippetMediaProvider.System,
                SnippetMediaCommand.PlayPause,
                PasteShortcutMode.CtrlV,
                TerminalCommand: string.Empty,
                SnippetTerminalShell.Cmd,
                RunAsAdministrator: false));
            var pasteGateway = new RecordingClipboardPasteGateway();
            var window = new MainWindow();
            using var viewModel = CreateMainViewModel(
                services,
                pasteGateway,
                window.GetPasteTargetWindowHandle,
                hideWindowAfterPaste: () => { });
            window.DataContext = viewModel;

            window.OnDirectHotkeyPressed(
                sender: null,
                new DirectHotkeyPressedEventArgs(hotkey.Id));

            Assert.Empty(pasteGateway.Calls);

            FlushDispatcher();

            Assert.Single(pasteGateway.Calls);
        });
    }

    private static KeyEventArgs CreatePreviewKeyDown(HwndSource source, Key key)
    {
        return new KeyEventArgs(
            Keyboard.PrimaryDevice,
            source,
            Environment.TickCount,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
    }

    private static void FlushDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(
            () => { },
            DispatcherPriority.ApplicationIdle);
    }

    private static void RunOnWpfThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ShutdownCurrentThreadApplication();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static void EnsureApplicationResources()
    {
        var application = Application.Current ?? new Application();
        var themeSource = new Uri(
            "/DeckDeckDeck.App;component/Resources/Theme.xaml",
            UriKind.Relative);

        if (application.Resources.MergedDictionaries.Any(dictionary => dictionary.Source == themeSource))
        {
            return;
        }

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = themeSource
        });
    }

    private static void ShutdownCurrentThreadApplication()
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            Application.Current.Shutdown();
        }
    }
}
