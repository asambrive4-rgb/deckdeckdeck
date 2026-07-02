using System.Diagnostics;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Diagnostics;

internal sealed class StartupTimingLog
{
    private readonly object _syncRoot = new();
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly List<StartupTimingEntry> _entries = [];
    private bool _flushed;

    public IDisposable Measure(string name)
    {
        return new StartupTimingMeasurement(this, name);
    }

    public void Mark(string name)
    {
        Add(name, duration: null);
    }

    public void FlushAsync(IAppLogger? logger)
    {
        if (logger is null)
        {
            return;
        }

        StartupTimingEntry[] entries;
        long totalMilliseconds;
        lock (_syncRoot)
        {
            if (_flushed)
            {
                return;
            }

            _flushed = true;
            totalMilliseconds = _total.ElapsedMilliseconds;
            entries = _entries.ToArray();
        }

        _ = Task.Run(() =>
        {
            var details = string.Join(
                "; ",
                entries.Select(entry => entry.DurationMilliseconds is null
                    ? $"{entry.Name}@{entry.ElapsedMilliseconds}ms"
                    : $"{entry.Name}={entry.DurationMilliseconds}ms@{entry.ElapsedMilliseconds}ms"));

            logger.Log($"Startup timing total={totalMilliseconds}ms: {details}");
        });
    }

    private void Add(string name, long? duration)
    {
        lock (_syncRoot)
        {
            if (_flushed)
            {
                return;
            }

            _entries.Add(new StartupTimingEntry(
                name,
                _total.ElapsedMilliseconds,
                duration));
        }
    }

    private sealed class StartupTimingMeasurement : IDisposable
    {
        private readonly StartupTimingLog _log;
        private readonly string _name;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public StartupTimingMeasurement(StartupTimingLog log, string name)
        {
            _log = log;
            _name = name;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _log.Add(_name, _stopwatch.ElapsedMilliseconds);
        }
    }

    private sealed record StartupTimingEntry(
        string Name,
        long ElapsedMilliseconds,
        long? DurationMilliseconds);
}
