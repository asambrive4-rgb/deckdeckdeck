using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
namespace DeckDeckDeck.App.Composition;

public sealed class AutoBackupCoordinator : IAutoBackupCoordinator, IDisposable
{
    private readonly BackupGateway _backupService;
    private readonly SemaphoreSlim _backupExecutionLock = new(1, 1);
    private readonly TimeSpan _delay;
    private readonly FileLogger? _loggingService;
    private readonly Func<Func<BackupResult>, Task<BackupResult>> _runBackupAsync;
    private readonly object _syncRoot = new();
    private readonly SettingsRepository _settingsService;
    private readonly Action<string> _showStatus;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _pendingRequest;
    private bool _disposed;

    public AutoBackupCoordinator(
        BackupGateway backupService,
        SettingsRepository settingsService,
        Action<string> showStatus,
        FileLogger? loggingService = null,
        TimeSpan? delay = null,
        SynchronizationContext? synchronizationContext = null)
        : this(
            backupService,
            settingsService,
            showStatus,
            loggingService,
            delay,
            synchronizationContext,
            static backupWork => Task.Run(backupWork))
    {
    }

    internal AutoBackupCoordinator(
        BackupGateway backupService,
        SettingsRepository settingsService,
        Action<string> showStatus,
        FileLogger? loggingService,
        TimeSpan? delay,
        SynchronizationContext? synchronizationContext,
        Func<Func<BackupResult>, Task<BackupResult>> runBackupAsync)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _delay = delay ?? TimeSpan.FromSeconds(5);
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
        _runBackupAsync = runBackupAsync;
    }

    public void RequestAutoBackup()
    {
        if (!ShouldScheduleBackup())
        {
            return;
        }

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
        var hasExecutionLock = false;

        try
        {
            await Task.Delay(_delay, request.Token).ConfigureAwait(false);
            await _backupExecutionLock.WaitAsync(request.Token).ConfigureAwait(false);
            hasExecutionLock = true;

            lock (_syncRoot)
            {
                if (!ReferenceEquals(_pendingRequest, request) || _disposed)
                {
                    return;
                }

                _pendingRequest = null;
            }

            var result = await _runBackupAsync(CreateAutomaticBackup).ConfigureAwait(false);

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
            if (hasExecutionLock)
            {
                _backupExecutionLock.Release();
            }

            request.Dispose();
        }
    }

    private BackupResult CreateAutomaticBackup()
    {
        var settings = _settingsService.Load();
        return _backupService.CreateAutomaticBackup(settings);
    }

    private bool ShouldScheduleBackup()
    {
        try
        {
            var settings = _settingsService.Load();
            return settings.AutoBackupEnabled && !string.IsNullOrWhiteSpace(settings.BackupFolderPath);
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Automatic backup settings check failed.", ex);
            return false;
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
