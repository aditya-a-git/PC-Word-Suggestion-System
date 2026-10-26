namespace PCWord.Core.Input;

/// <summary>
/// Describes a bounded view around the caret. Adapters must not send a whole document.
/// Versions must increase monotonically for each adapter instance.
/// </summary>
public sealed record InputContextSnapshot(
    long Version,
    string? FieldId,
    string? TextBeforeCaret,
    string? TextAfterCaret,
    int SelectionLength,
    bool HasFocus,
    InputFieldSensitivity Sensitivity,
    bool IsComposing = false,
    bool ContextAvailable = true);

/// <summary>Unknown sensitivity is treated conservatively as ineligible.</summary>
public enum InputFieldSensitivity
{
    Unknown = 0,
    KnownSafe = 1,
    Sensitive = 2
}
