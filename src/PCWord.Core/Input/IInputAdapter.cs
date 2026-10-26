namespace PCWord.Core.Input;

/// <summary>
/// Platform-specific input integrations implement this contract. The contract does
/// not imply that any particular Windows API can provide the requested information.
/// </summary>
public interface IInputAdapter
{
    event EventHandler<InputContextChangedEventArgs>? ContextChanged;

    ValueTask<TextEditResult> ApplyEditAsync(
        TextEditRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InputContextChangedEventArgs(InputContextSnapshot snapshot) : EventArgs
{
    public InputContextSnapshot Snapshot { get; } = snapshot;
}
