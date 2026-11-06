namespace PCWord.WindowsIntegration.Protocol;

/// <summary>
/// Single-current-request gate. Starting new context invalidates and cancels any
/// outstanding prediction. Cancellation is advisory; callers must still use IsCurrent
/// immediately before publishing a result.
/// </summary>
public sealed class StaleRequestGate : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _pending;
    private PredictionTicket? _current;
    private bool _disposed;

    public PredictionTicket Begin(long contextVersion, string? fieldId)
    {
        if (contextVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(contextVersion));
        if (fieldId?.Length > PCWord.Core.Input.InputContextSnapshot.MaximumFieldIdLength)
            throw new ArgumentOutOfRangeException(nameof(fieldId));

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            InvalidateCore();
            _pending = new CancellationTokenSource();
            _current = new PredictionTicket(Guid.NewGuid(), contextVersion, fieldId,
                _pending.Token);
            return _current;
        }
    }

    public bool IsCurrent(Guid requestId, long contextVersion, string? fieldId)
    {
        lock (_sync)
        {
            return !_disposed && _current is not null &&
                _current.RequestId == requestId &&
                _current.ContextVersion == contextVersion &&
                StringComparer.Ordinal.Equals(_current.FieldId, fieldId) &&
                !_current.CancellationToken.IsCancellationRequested;
        }
    }

    public void Invalidate()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            InvalidateCore();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            InvalidateCore();
        }
    }

    private void InvalidateCore()
    {
        _current = null;
        if (_pending is null) return;
        _pending.Cancel();
        _pending.Dispose();
        _pending = null;
    }
}
