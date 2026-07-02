using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class AutoBackupCoordinatorTests
{
    [Fact]
    public async Task BackupRunsThroughExecutorWhenCurrentSynchronizationContextDoesNotPump()
    {
        var services = CreateServices();
        EnableAutoBackup(services);
        var context = new RecordingSynchronizationContext(executePostedCallbacks: false);
        var previousContext = SynchronizationContext.Current;
        var backupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new AutoBackupCoordinator(
            services.BackupGateway,
            services.SettingsRepository,
            _ => { },
            services.FileLogger,
            TimeSpan.FromMilliseconds(10),
            context,
            _ =>
            {
                backupStarted.TrySetResult();
                return Task.FromResult(BackupResult.Skip());
            });

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            coordinator.RequestAutoBackup();
            SynchronizationContext.SetSynchronizationContext(previousContext);

            await backupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0, context.PostCount);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            coordinator.Dispose();
        }
    }

    [Fact]
    public async Task RequestAutoBackupReturnsBeforeBackupWorkCompletes()
    {
        var services = CreateServices();
        EnableAutoBackup(services);
        var backupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowBackupToFinish = new TaskCompletionSource<BackupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new AutoBackupCoordinator(
            services.BackupGateway,
            services.SettingsRepository,
            _ => { },
            services.FileLogger,
            TimeSpan.Zero,
            synchronizationContext: null,
            async _ =>
            {
                backupStarted.TrySetResult();
                var result = await allowBackupToFinish.Task;
                backupFinished.TrySetResult();
                return result;
            });

        try
        {
            var requestTask = Task.Run(coordinator.RequestAutoBackup);

            await requestTask.WaitAsync(TimeSpan.FromSeconds(1));
            await backupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(backupFinished.Task.IsCompleted);
        }
        finally
        {
            allowBackupToFinish.TrySetResult(BackupResult.Skip());
            await backupFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            coordinator.Dispose();
        }
    }

    [Fact]
    public async Task MultipleRequestsBeforeDelayRunsOnlyLatestPendingBackup()
    {
        var services = CreateServices();
        EnableAutoBackup(services);
        var backupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backupCount = 0;
        var coordinator = new AutoBackupCoordinator(
            services.BackupGateway,
            services.SettingsRepository,
            _ => { },
            services.FileLogger,
            TimeSpan.FromMilliseconds(100),
            synchronizationContext: null,
            _ =>
            {
                Interlocked.Increment(ref backupCount);
                backupStarted.TrySetResult();
                return Task.FromResult(BackupResult.Skip());
            });

        try
        {
            coordinator.RequestAutoBackup();
            coordinator.RequestAutoBackup();

            await backupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.Equal(1, backupCount);
        }
        finally
        {
            coordinator.Dispose();
        }
    }

    [Fact]
    public async Task FailureStatusIsPostedToSynchronizationContext()
    {
        var services = CreateServices();
        EnableAutoBackup(services);
        var context = new RecordingSynchronizationContext(executePostedCallbacks: true);
        var statusReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new AutoBackupCoordinator(
            services.BackupGateway,
            services.SettingsRepository,
            message => statusReceived.TrySetResult(message),
            services.FileLogger,
            TimeSpan.Zero,
            context,
            _ => Task.FromResult(BackupResult.Failure("backup failed")));

        try
        {
            coordinator.RequestAutoBackup();

            var status = await statusReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("backup failed", status);
            Assert.Equal(1, context.PostCount);
        }
        finally
        {
            coordinator.Dispose();
        }
    }

    [Fact]
    public async Task RunningBackupsAreSerialized()
    {
        var services = CreateServices();
        EnableAutoBackup(services);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstToFinish = new TaskCompletionSource<BackupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSecondToFinish = new TaskCompletionSource<BackupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var maxActiveBackups = 0;
        var activeBackups = 0;
        var runCount = 0;
        var countLock = new object();
        var coordinator = new AutoBackupCoordinator(
            services.BackupGateway,
            services.SettingsRepository,
            _ => { },
            services.FileLogger,
            TimeSpan.Zero,
            synchronizationContext: null,
            async _ =>
            {
                var runNumber = Interlocked.Increment(ref runCount);
                var activeCount = Interlocked.Increment(ref activeBackups);
                lock (countLock)
                {
                    maxActiveBackups = Math.Max(maxActiveBackups, activeCount);
                }

                try
                {
                    if (runNumber == 1)
                    {
                        firstStarted.TrySetResult();
                        return await allowFirstToFinish.Task;
                    }

                    if (runNumber == 2)
                    {
                        secondStarted.TrySetResult();
                        return await allowSecondToFinish.Task;
                    }

                    return BackupResult.Skip();
                }
                finally
                {
                    Interlocked.Decrement(ref activeBackups);
                    if (runNumber == 2)
                    {
                        secondFinished.TrySetResult();
                    }
                }
            });

        try
        {
            coordinator.RequestAutoBackup();
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            coordinator.RequestAutoBackup();
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            Assert.False(secondStarted.Task.IsCompleted);

            allowFirstToFinish.TrySetResult(BackupResult.Skip());
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(2, runCount);
            Assert.Equal(1, maxActiveBackups);
        }
        finally
        {
            allowFirstToFinish.TrySetResult(BackupResult.Skip());
            allowSecondToFinish.TrySetResult(BackupResult.Skip());
            await secondFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            coordinator.Dispose();
        }
    }

    [Fact]
    public async Task RequestAutoBackupSkipsWorkWhenAutoBackupIsDisabled()
    {
        var services = CreateServices();
        var backupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new AutoBackupCoordinator(
            services.BackupGateway,
            services.SettingsRepository,
            _ => { },
            services.FileLogger,
            TimeSpan.Zero,
            synchronizationContext: null,
            _ =>
            {
                backupStarted.TrySetResult();
                return Task.FromResult(BackupResult.Skip());
            });

        try
        {
            coordinator.RequestAutoBackup();

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.False(backupStarted.Task.IsCompleted);
        }
        finally
        {
            coordinator.Dispose();
        }
    }

    private static void EnableAutoBackup(TestServices services)
    {
        var settings = services.SettingsRepository.Load();
        settings.AutoBackupEnabled = true;
        settings.BackupFolderPath = Path.Combine(
            Path.GetTempPath(),
            "DeckDeckDeckTests",
            Guid.NewGuid().ToString("N"));
        services.SettingsRepository.Save(settings);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly bool _executePostedCallbacks;
        private int _postCount;

        public RecordingSynchronizationContext(bool executePostedCallbacks)
        {
            _executePostedCallbacks = executePostedCallbacks;
        }

        public int PostCount => _postCount;

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            if (_executePostedCallbacks)
            {
                ThreadPool.QueueUserWorkItem(_ => d(state));
            }
        }
    }
}
