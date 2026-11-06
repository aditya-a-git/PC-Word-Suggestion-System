using PCWord.Core.Input;

namespace PCWord.WindowsIntegration.Protocol;

public enum AdapterMessageKind
{
    ContextUpdate = 1,
    Suggestions = 2,
    AcceptSuggestion = 3,
    EditResult = 4,
    Pause = 5
}

/// <summary>
/// Bounded versioned local-IPC contract. Context is intentionally transient and must
/// never be logged or persisted by the transport.
/// </summary>
public sealed record AdapterMessage(
    int ProtocolVersion,
    AdapterMessageKind Kind,
    Guid RequestId,
    long ContextVersion,
    string? FieldId = null,
    InputContextSnapshot? Snapshot = null,
    IReadOnlyList<string>? Suggestions = null,
    string? Candidate = null,
    TextEditStatus? EditStatus = null);

public sealed record PredictionTicket(Guid RequestId, long ContextVersion, string? FieldId,
    CancellationToken CancellationToken);
