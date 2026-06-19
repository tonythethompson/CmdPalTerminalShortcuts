namespace QuickShell.Services;

using System.Threading;

internal sealed partial class SearchDebouncer : IDisposable
{
    private readonly Timer _timer;
    private readonly Action<string> _callback;
    private readonly object _sync = new();
    private string _pending = string.Empty;
    private bool _disposed;

    public SearchDebouncer(Action<string> callback, int delayMilliseconds = 200)
    {
        _callback = callback;
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        DelayMilliseconds = delayMilliseconds;
    }

    public int DelayMilliseconds { get; }

    public void Schedule(string query)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            _pending = query ?? string.Empty;
        }

        _timer.Change(DelayMilliseconds, Timeout.Infinite);
    }

    public void FlushNow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        Flush();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
    }

    private void Flush()
    {
        string query;
        lock (_sync)
        {
            query = _pending;
        }

        _callback(query);
    }
}
