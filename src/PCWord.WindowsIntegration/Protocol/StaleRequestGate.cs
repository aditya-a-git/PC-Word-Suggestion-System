namespace PCWord.WindowsIntegration.Protocol;

/// <summary>
/// One current request per adapter lifetime. Versions strictly increase, including
/// across invalidations. Create a new gate for a new authenticated adapter lifetime.
/// Cancellation is advisory: recheck IsCurrent immediately before delivery.
/// </summary>
public sealed class StaleRequestGate : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _pending;
    private PredictionTicket? _current;
    private long _lastVersion = -1;
    private long _cancellationFailures;
    private bool _disposed;

    /// <summary>Privacy-safe diagnostic count; contains no exception messages or user text.</summary>
    public long CancellationFailureCount => Interlocked.Read(ref _cancellationFailures);

    public PredictionTicket Begin(long contextVersion, string? fieldId)
    {
        if (contextVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(contextVersion));
        if (fieldId?.Length > PCWord.Core.Input.InputContextSnapshot.MaximumFieldIdLength)
            throw new ArgumentOutOfRangeException(nameof(fieldId));

        CancellationTokenSource? previous;
        PredictionTicket ticket;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (contextVersion <= _lastVersion)
                throw new ArgumentOutOfRangeException(nameof(contextVersion), "Context versions must increase.");
            previous = _pending;
            _pending = new CancellationTokenSource();
            _lastVersion = contextVersion;
            ticket = _current = new PredictionTicket(Guid.NewGuid(), contextVersion, fieldId, _pending.Token);
        }
        Cancel(previous);
        return ticket;
    }

    public bool IsCurrent(Guid requestId, long contextVersion, string? fieldId)
    {
        lock (_sync)
            return !_disposed && _current is not null &&
                _current.RequestId == requestId && _current.ContextVersion == contextVersion &&
                StringComparer.Ordinal.Equals(_current.FieldId, fieldId) &&
                !_current.CancellationToken.IsCancellationRequested;
    }

    public void Invalidate()
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previous = Detach();
        }
        Cancel(previous);
    }

    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            previous = Detach();
        }
        Cancel(previous);
    }

    private CancellationTokenSource? Detach()
    {
        var previous = _pending;
        _current = null;
        _pending = null;
        return previous;
    }

    private void Cancel(CancellationTokenSource? source)
    {
        if (source is null) return;
        // Cancellation executes arbitrary registered callbacks. Never invoke it
        // under _sync, and never let an old callback overwrite the new request.
        try { source.Cancel(); }
        catch (AggregateException) { Interlocked.Increment(ref _cancellationFailures); }
        finally { source.Dispose(); }
    }
}
