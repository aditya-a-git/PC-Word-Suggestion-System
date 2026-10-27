namespace PCWord.Core.Input;

/// <summary>Verified screen-space caret bounds supplied by an adapter when available.</summary>
public readonly record struct CaretScreenBounds(int Left, int Top, int Right, int Bottom, int Dpi);

/// <summary>
/// Describes a bounded view around the caret. Adapters must not send a whole document.
/// Versions must increase monotonically for each adapter instance.
/// </summary>
public sealed record InputContextSnapshot
{
    public const int MaximumFieldIdLength = 256;
    public const int MaximumTextBeforeCaret = 128;
    public const int MaximumTextAfterCaret = 32;

    public InputContextSnapshot(
        long Version,
        string? FieldId,
        string? TextBeforeCaret,
        string? TextAfterCaret,
        int SelectionLength,
        bool HasFocus,
        InputFieldSensitivity Sensitivity,
        bool IsComposing = false,
        bool ContextAvailable = true,
        CaretScreenBounds? CaretBounds = null,
        int? CaretOffset = null)
    {
        if (Version < 0)
            throw new ArgumentOutOfRangeException(nameof(Version));
        if (FieldId?.Length > MaximumFieldIdLength)
            throw new ArgumentOutOfRangeException(nameof(FieldId));
        if (TextBeforeCaret?.Length > MaximumTextBeforeCaret)
            throw new ArgumentOutOfRangeException(nameof(TextBeforeCaret));
        if (TextAfterCaret?.Length > MaximumTextAfterCaret)
            throw new ArgumentOutOfRangeException(nameof(TextAfterCaret));
        if (SelectionLength < 0)
            throw new ArgumentOutOfRangeException(nameof(SelectionLength));
        if (CaretOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(CaretOffset));
        if (!IsWellFormed(FieldId) || !IsWellFormed(TextBeforeCaret) || !IsWellFormed(TextAfterCaret))
            throw new ArgumentException("Snapshot strings must contain well-formed UTF-16.");
        if (CaretBounds is { } bounds &&
            (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top || bounds.Dpi <= 0))
            throw new ArgumentOutOfRangeException(nameof(CaretBounds));

        this.Version = Version;
        this.FieldId = FieldId;
        this.TextBeforeCaret = TextBeforeCaret;
        this.TextAfterCaret = TextAfterCaret;
        this.SelectionLength = SelectionLength;
        this.HasFocus = HasFocus;
        this.Sensitivity = Sensitivity;
        this.IsComposing = IsComposing;
        this.ContextAvailable = ContextAvailable;
        this.CaretBounds = CaretBounds;
        this.CaretOffset = CaretOffset;
    }

    public long Version { get; init; }
    public string? FieldId { get; init; }
    public string? TextBeforeCaret { get; init; }
    public string? TextAfterCaret { get; init; }
    public int SelectionLength { get; init; }
    public bool HasFocus { get; init; }
    public InputFieldSensitivity Sensitivity { get; init; }
    public bool IsComposing { get; init; }
    public bool ContextAvailable { get; init; }
    public CaretScreenBounds? CaretBounds { get; init; }
    public int? CaretOffset { get; init; }

    private static bool IsWellFormed(string? value)
    {
        if (value is null)
            return true;

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    return false;
                index++;
            }
            else if (char.IsLowSurrogate(current))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>Unknown sensitivity is treated conservatively as ineligible.</summary>
public enum InputFieldSensitivity
{
    Unknown = 0,
    KnownSafe = 1,
    Sensitive = 2
}
