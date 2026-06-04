namespace DeckDeckDeck.App.Services;

public sealed class AutoBackupCoordinator : IAutoBackupCoordinator, IDisposable
{
    private readonly BackupService _backupService;
    private readonly TimeSpan _delay;
    private readonly LoggingService? _loggingService;
    private readonly object _syncRoot = new();
    private readonly SettingsService _settingsService;
    private readonly Action<string> _showStatus;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _pendingRequest;
    private bool _disposed;

    public AutoBackupCoordinator(
        BackupService backupService,
        SettingsService settingsService,
        Action<string> showStatus,
        LoggingService? loggingService = null,
        TimeSpan? delay = null,
        SynchronizationContext? synchronizationContext = null)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _delay = delay ?? TimeSpan.FromSeconds(5);
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
    }

    public void RequestAutoBackup()
    {
        CancellationTokenSource request;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _pendingRequest?.Cancel();
            request = new CancellationTokenSource();
            _pendingRequest = request;
        }

        _ = RunDelayedBackupAsync(request);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
            _pendingRequest?.Cancel();
            _pendingRequest?.Dispose();
            _pendingRequest = null;
        }
    }

    private async Task RunDelayedBackupAsync(CancellationTokenSource request)
    {
        try
        {
            await Task.Delay(_delay, request.Token);

            lock (_syncRoot)
            {
                if (!ReferenceEquals(_pendingRequest, request) || _disposed)
                {
                    return;
                }

                _pendingRequest = null;
            }

            var settings = _settingsService.Load();
            var result = _backupService.CreateAutomaticBackup(settings);

            if (!result.Succeeded && !result.Skipped)
            {
                ReportStatus(result.ErrorMessage ?? "자동 백업을 만들지 못했습니다.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Automatic backup request failed.", ex);
            ReportStatus("자동 백업을 만들지 못했습니다.");
        }
        finally
        {
            request.Dispose();
        }
    }

    private void ReportStatus(string message)
    {
        if (_synchronizationContext is null)
        {
            _showStatus(message);
            return;
        }

        _synchronizationContext.Post(_ => _showStatus(message), null);
    }
}
