using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class AppInstanceCoordinator : IAppInstanceCoordinator
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private CancellationTokenSource? _listeningCancellation;
    private Task? _listeningTask;
    private Mutex? _mutex;

    public AppInstanceCoordinator()
        : this(CreateUserScopedNameSuffix())
    {
    }

    internal AppInstanceCoordinator(string nameSuffix)
    {
        _mutexName = $@"Local\DeckDeckDeck.{nameSuffix}";
        _pipeName = $"DeckDeckDeck.{nameSuffix}";
    }

    public bool TryBecomePrimary()
    {
        if (_mutex is not null)
        {
            return true;
        }

        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public async Task<bool> RequestShowPrimaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            var showRequest = new byte[] { 1 };
            await pipe.WriteAsync(showRequest, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void StartListening(Action onShowRequested)
    {
        if (_listeningCancellation is not null)
        {
            return;
        }

        _listeningCancellation = new CancellationTokenSource();
        _listeningTask = ListenAsync(onShowRequested, _listeningCancellation.Token);
    }

    public void Dispose()
    {
        if (_listeningCancellation is not null)
        {
            _listeningCancellation.Cancel();
            _listeningCancellation.Dispose();
            _listeningCancellation = null;
        }

        _listeningTask = null;

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }

    private async Task ListenAsync(Action onShowRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[1];
                await pipe.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                onShowRequested();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static string CreateUserScopedNameSuffix()
    {
        var identity = WindowsIdentity.GetCurrent();
        var value = identity.User?.Value ?? Environment.UserName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
