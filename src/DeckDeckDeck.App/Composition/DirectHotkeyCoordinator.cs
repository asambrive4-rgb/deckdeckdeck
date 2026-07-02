using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Composition;

internal sealed class DirectHotkeyCoordinator : IDisposable
{
    private readonly IDirectHotkeyRegistrar _directHotkeyRegistrar;
    private readonly MainViewModel _viewModel;
    private bool _isStarted;

    public DirectHotkeyCoordinator(
        IDirectHotkeyRegistrar directHotkeyRegistrar,
        MainViewModel viewModel)
    {
        _directHotkeyRegistrar = directHotkeyRegistrar;
        _viewModel = viewModel;
        _directHotkeyRegistrar.DirectHotkeyPressed += OnDirectHotkeyPressed;
        _viewModel.DirectHotkeyCaptureStateChanged += OnDirectHotkeyCaptureStateChanged;
        _viewModel.DirectHotkeysChanged += OnDirectHotkeysChanged;
    }

    public event EventHandler<DirectHotkeyPressedEventArgs>? DirectHotkeyPressed;

    public IReadOnlyList<string> Start()
    {
        return RefreshAndStartWhenNeeded();
    }

    public void Refresh()
    {
        var failures = RefreshAndStartWhenNeeded();
        if (failures.Count > 0)
        {
            _viewModel.ReportHotkeyRegistrationFailure(failures);
        }
    }

    public void Dispose()
    {
        _viewModel.DirectHotkeyCaptureStateChanged -= OnDirectHotkeyCaptureStateChanged;
        _viewModel.DirectHotkeysChanged -= OnDirectHotkeysChanged;
        _directHotkeyRegistrar.DirectHotkeyPressed -= OnDirectHotkeyPressed;
        _directHotkeyRegistrar.Dispose();
    }

    private void OnDirectHotkeyCaptureStateChanged(object? sender, EventArgs e)
    {
        _directHotkeyRegistrar.IsSuspended = _viewModel.IsCapturingHotkeyInput;
    }

    private void OnDirectHotkeysChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private IReadOnlyList<string> RefreshAndStartWhenNeeded()
    {
        var registrations = _viewModel.LoadActiveDirectHotkeys();
        _directHotkeyRegistrar.Refresh(registrations);
        _directHotkeyRegistrar.IsSuspended = _viewModel.IsCapturingHotkeyInput;

        if (_isStarted || registrations.Count == 0)
        {
            return [];
        }

        var failures = _directHotkeyRegistrar.Start();
        _isStarted = failures.Count == 0;
        return failures;
    }

    private void OnDirectHotkeyPressed(object? sender, DirectHotkeyPressedEventArgs e)
    {
        DirectHotkeyPressed?.Invoke(this, e);
    }
}
