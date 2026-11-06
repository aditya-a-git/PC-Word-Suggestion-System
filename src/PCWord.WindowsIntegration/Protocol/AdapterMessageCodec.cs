using System.Text.Json;
using System.Text.Json.Serialization;
using PCWord.Core.Input;

namespace PCWord.WindowsIntegration.Protocol;

public static class AdapterMessageCodec
{
    public const int CurrentProtocolVersion = 1;
    public const int MaximumFrameBytes = 8192;
    private const int MaximumCandidateLength = 256;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] Serialize(AdapterMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Validate(message);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, Options);
        if (bytes.Length > MaximumFrameBytes)
            throw new InvalidDataException("IPC frame exceeds the configured limit.");
        return bytes;
    }

    public static AdapterMessage Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumFrameBytes)
            throw new InvalidDataException("IPC frame length is invalid.");

        AdapterMessage message;
        try
        {
            message = JsonSerializer.Deserialize<AdapterMessage>(bytes, Options)
                ?? throw new InvalidDataException("IPC frame is empty.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("IPC frame is malformed.");
        }

        Validate(message);
        return message;
    }

    public static void Validate(AdapterMessage message)
    {
        if (message.ProtocolVersion != CurrentProtocolVersion)
            throw new InvalidDataException("Unsupported IPC protocol version.");
        if (message.RequestId == Guid.Empty || message.ContextVersion < 0)
            throw new InvalidDataException("IPC request identity is invalid.");

        if (message.FieldId?.Length > InputContextSnapshot.MaximumFieldIdLength)
            throw new InvalidDataException("IPC field identity is too long.");

        switch (message.Kind)
        {
            case AdapterMessageKind.ContextUpdate:
                if (message.Snapshot is null ||
                    message.Snapshot.Version != message.ContextVersion ||
                    !StringComparer.Ordinal.Equals(message.Snapshot.FieldId, message.FieldId) ||
                    message.Suggestions is not null || message.Candidate is not null ||
                    message.EditStatus is not null)
                    throw new InvalidDataException("Context update metadata is inconsistent.");
                break;
            case AdapterMessageKind.Suggestions:
                if (message.Snapshot is not null || message.Candidate is not null ||
                    message.EditStatus is not null || message.Suggestions is null ||
                    message.Suggestions.Count > 3 ||
                    message.Suggestions.Any(value => !IsValidCandidate(value)))
                    throw new InvalidDataException("Suggestion response is invalid.");
                break;
            case AdapterMessageKind.AcceptSuggestion:
                if (message.Snapshot is not null || message.Suggestions is not null ||
                    message.EditStatus is not null || string.IsNullOrWhiteSpace(message.FieldId) ||
                    !IsValidCandidate(message.Candidate))
                    throw new InvalidDataException("Acceptance request is invalid.");
                break;
            case AdapterMessageKind.EditResult:
                if (message.Snapshot is not null || message.Suggestions is not null ||
                    message.Candidate is not null || message.EditStatus is null ||
                    !Enum.IsDefined(message.EditStatus.Value))
                    throw new InvalidDataException("Edit result is invalid.");
                break;
            case AdapterMessageKind.Pause:
                if (message.Snapshot is not null || message.Suggestions is not null ||
                    message.Candidate is not null || message.EditStatus is not null)
                    throw new InvalidDataException("Pause message contains an unexpected payload.");
                break;
            default:
                throw new InvalidDataException("Unknown IPC message kind.");
        }
    }

    private static bool IsValidCandidate(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumCandidateLength &&
        !value.Any(char.IsControl);
}
