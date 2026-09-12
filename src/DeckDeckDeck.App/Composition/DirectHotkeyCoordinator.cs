// 역할: 사용자가 지정한 단축키 입력을 실시간으로 감시하고 해당 슬롯의 동작을 실행하도록 연결합니다.
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Composition;

internal sealed class DirectHotkeyCoordinator : IDisposable
{
    private readonly IDirectHotkeyRegistrar _directHotkeyRegistrar;
    private readonly MainViewModel _viewModel;
    private bool _isPasteSelectionActive;
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

    public IReadOnlyList<string> Start() => RefreshAndStartWhenNeeded();

    public void Refresh()
    {
        var failures = RefreshAndStartWhenNeeded();
        if (failures.Count > 0)
        {
            _viewModel.ReportHotkeyRegistrationFailure(failures);
        }
    }

    public void SetPasteSelectionActive(bool isActive)
    {
        _isPasteSelectionActive = isActive;
        UpdateSuspension();
    }

    public void Dispose()
    {
        _viewModel.DirectHotkeyCaptureStateChanged -= OnDirectHotkeyCaptureStateChanged;
        _viewModel.DirectHotkeysChanged -= OnDirectHotkeysChanged;
        _directHotkeyRegistrar.DirectHotkeyPressed -= OnDirectHotkeyPressed;
        _directHotkeyRegistrar.Dispose();
    }

    private void OnDirectHotkeyCaptureStateChanged(object? sender, EventArgs e) => UpdateSuspension();
    private void OnDirectHotkeysChanged(object? sender, EventArgs e) => Refresh();

    private IReadOnlyList<string> RefreshAndStartWhenNeeded()
    {
        var registrations = _viewModel.LoadActiveDirectHotkeys();
        _directHotkeyRegistrar.Refresh(registrations);
        UpdateSuspension();

        if (_isStarted || registrations.Count == 0)
        {
            return [];
        }

        var failures = _directHotkeyRegistrar.Start();
        _isStarted = failures.Count == 0;
        return failures;
    }

    private void UpdateSuspension() =>
        _directHotkeyRegistrar.IsSuspended = _isPasteSelectionActive || _viewModel.IsCapturingHotkeyInput;

    private void OnDirectHotkeyPressed(object? sender, DirectHotkeyPressedEventArgs e) =>
        DirectHotkeyPressed?.Invoke(this, e);
}
