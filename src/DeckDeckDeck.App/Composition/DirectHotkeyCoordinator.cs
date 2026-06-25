using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Composition;

internal sealed class DirectHotkeyCoordinator : IDisposable
{
    private readonly DirectHotkeyRegistrar _directHotkeyRegistrar;
    private readonly MainViewModel _viewModel;

    public DirectHotkeyCoordinator(
        DirectHotkeyRegistrar directHotkeyRegistrar,
        MainViewModel viewModel)
    {
        _directHotkeyRegistrar = directHotkeyRegistrar;
        _viewModel = viewModel;
        _directHotkeyRegistrar.DirectHotkeyPressed += OnDirectHotkeyPressed;
        _viewModel.DirectHotkeysChanged += OnDirectHotkeysChanged;
    }

    public event EventHandler<DirectHotkeyPressedEventArgs>? DirectHotkeyPressed;

    public IReadOnlyList<string> Start()
    {
        var failures = _directHotkeyRegistrar.Start();
        Refresh();
        return failures;
    }

    public void Refresh()
    {
        _directHotkeyRegistrar.Refresh(_viewModel.LoadActiveDirectHotkeys());
        _directHotkeyRegistrar.IsSuspended = _viewModel.IsCapturingHotkeyInput;
    }

    public void Dispose()
    {
        _viewModel.DirectHotkeysChanged -= OnDirectHotkeysChanged;
        _directHotkeyRegistrar.DirectHotkeyPressed -= OnDirectHotkeyPressed;
        _directHotkeyRegistrar.Dispose();
    }

    private void OnDirectHotkeysChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void OnDirectHotkeyPressed(object? sender, DirectHotkeyPressedEventArgs e)
    {
        DirectHotkeyPressed?.Invoke(this, e);
    }
}
