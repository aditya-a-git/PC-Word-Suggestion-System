namespace PCWord.Core.Input;

/// <summary>
/// Requests an explicit replacement. An adapter must reject the request if the
/// active field or text context no longer matches the supplied version/expected text.
/// </summary>
public sealed record TextEditRequest(
    long ContextVersion,
    string FieldId,
    string ExpectedText,
    string Replacement);

public enum TextEditStatus
{
    Applied = 0,
    StaleContext = 1,
    Unsupported = 2,
    RefusedSensitiveField = 3,
    TimedOut = 4,
    Cancelled = 5,
    Failed = 6
}

public sealed record TextEditResult(TextEditStatus Status, string? Detail = null)
{
    public bool WasApplied => Status == TextEditStatus.Applied;
}
