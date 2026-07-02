using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class DirectHotkeyCoordinatorTests
{
    [Fact]
    public void StartSkipsKeyboardHookWhenNoDirectHotkeysAreRegistered()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services, initializeHome: false);
        using var registrar = new RecordingDirectHotkeyRegistrar();
        using var coordinator = new DirectHotkeyCoordinator(registrar, viewModel);

        var failures = coordinator.Start();

        Assert.Empty(failures);
        Assert.Equal(0, registrar.StartCount);
        Assert.Single(registrar.RefreshCalls);
        Assert.Empty(registrar.RefreshCalls[0]);
    }

    [Fact]
    public void RefreshStartsKeyboardHookWhenDirectHotkeyIsAddedLater()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services, initializeHome: false);
        using var registrar = new RecordingDirectHotkeyRegistrar();
        using var coordinator = new DirectHotkeyCoordinator(registrar, viewModel);
        coordinator.Start();
        var hotkey = services.HotkeyActionRepository.Create(CreatePasteAction(
            "Direct Paste",
            new HotkeyGesture(0x67, HotkeyModifiers.None)));

        viewModel.NotifyDirectHotkeysChanged();

        Assert.Equal(1, registrar.StartCount);
        var registration = Assert.Single(registrar.RefreshCalls.Last());
        Assert.Equal(hotkey.Id, registration.HotkeyActionId);
        Assert.Equal(0x67u, registration.Gesture.VirtualKey);
    }

    private static HotkeyActionSaveData CreatePasteAction(string title, HotkeyGesture gesture)
    {
        return new HotkeyActionSaveData(
            title,
            gesture,
            IsEnabled: true,
            Content: "Paste from hotkey",
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
            RunAsAdministrator: false);
    }

    private sealed class RecordingDirectHotkeyRegistrar : IDirectHotkeyRegistrar
    {
        public event EventHandler<DirectHotkeyPressedEventArgs>? DirectHotkeyPressed
        {
            add { }
            remove { }
        }

        public bool IsSuspended { get; set; }

        public int StartCount { get; private set; }

        public List<IReadOnlyList<DirectHotkeyRegistration>> RefreshCalls { get; } = [];

        public IReadOnlyList<string> Start()
        {
            StartCount++;
            return [];
        }

        public void Refresh(IReadOnlyList<DirectHotkeyRegistration> registrations)
        {
            RefreshCalls.Add(registrations);
        }

        public void Dispose()
        {
        }
    }
}
