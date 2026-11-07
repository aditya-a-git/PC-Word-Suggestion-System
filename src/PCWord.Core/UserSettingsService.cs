namespace PCWord.Core;

/// <summary>
/// Owns the persisted settings snapshot. A change is visible only after validation,
/// durable save, and synchronous notification to runtime subscribers.
/// </summary>
public sealed class UserSettingsService
{
    private readonly object _gate = new();
    private readonly IUserSettingsStore _store;
    private UserSettings _current;

    public UserSettingsService(IUserSettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        var loaded = _store.Load();
        _current = Copy(loaded.Settings);
        LoadStatus = loaded.Status;
    }

    public string? LoadStatus { get; }
    public UserSettings Current { get { lock (_gate) return Copy(_current); } }
    public event EventHandler<UserSettingsChangedEventArgs>? Changed;

    public bool TryUpdate(UserSettings candidate, out string? error)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.IsValid(out var validationError))
        {
            error = validationError;
            return false;
        }

        UserSettings previous;
        UserSettings next = Copy(candidate);
        lock (_gate)
        {
            previous = _current;
            if (AreEqual(previous, next))
            {
                error = null;
                return true;
            }

            try
            {
                _store.Save(next);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                error = $"Settings were not applied because they could not be saved: {ex.Message}";
                return false;
            }

            _current = next;
        }

        try
        {
            Changed?.Invoke(this, new UserSettingsChangedEventArgs(Copy(previous), Copy(next)));
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                try
                {
                    _store.Save(previous);
                    _current = previous;
                    Changed?.Invoke(this, new UserSettingsChangedEventArgs(Copy(next), Copy(previous)));
                    error = $"Settings were rolled back because runtime application failed: {ex.Message}";
                }
                catch (Exception rollbackError)
                {
                    error = $"Runtime application failed and settings rollback failed. Restart PCWord and inspect its settings file. {rollbackError.Message}";
                }
            }
            return false;
        }
    }

    private static bool AreEqual(UserSettings left, UserSettings right) =>
        left.SchemaVersion == right.SchemaVersion && left.Enabled == right.Enabled &&
        left.Pause == right.Pause && left.Acceptance == right.Acceptance &&
        left.PersonalizationEnabled == right.PersonalizationEnabled && left.Hosts.SequenceEqual(right.Hosts, StringComparer.OrdinalIgnoreCase);

    private static UserSettings Copy(UserSettings value) => value with
    {
        HostAllowlist = value.Hosts.ToArray(),
        PauseShortcut = value.Pause,
        AcceptanceShortcut = value.Acceptance
    };
}

public sealed class UserSettingsChangedEventArgs(UserSettings previous, UserSettings current) : EventArgs
{
    public UserSettings Previous { get; } = previous;
    public UserSettings Current { get; } = current;
}