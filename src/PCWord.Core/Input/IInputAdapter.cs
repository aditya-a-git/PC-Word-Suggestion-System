namespace PCWord.Core.Input;

/// <summary>
/// Platform-specific input integrations implement this contract. The contract does
/// not imply that any particular Windows API can provide the requested information.
/// </summary>
public interface IInputAdapter
{
    event EventHandler<InputContextChangedEventArgs>? ContextChanged;

    /// <summary>Stops context collection and invalidates active candidates when disabled.</summary>
    void SetEnabled(bool enabled);

    /// <summary>
    /// Supplies the exact current candidate set that an explicit edit may use. Adapters
    /// must reject sets for an older context version or a different field identity.
    /// </summary>
    void SetCandidateSet(long contextVersion, string? fieldId, IReadOnlyList<string> candidates);

    ValueTask<TextEditResult> ApplyEditAsync(
        TextEditRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InputContextChangedEventArgs(InputContextSnapshot snapshot) : EventArgs
{
    public InputContextSnapshot Snapshot { get; } = snapshot;
}
