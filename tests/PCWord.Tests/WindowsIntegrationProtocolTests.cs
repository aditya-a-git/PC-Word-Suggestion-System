using PCWord.Core.Input;
using PCWord.WindowsIntegration.Protocol;
using Xunit;

namespace PCWord.Tests;

public sealed class WindowsIntegrationProtocolTests
{
    private static AdapterMessage Context(long version = 4, string field = "field-1") =>
        new(AdapterMessageCodec.CurrentProtocolVersion, AdapterMessageKind.ContextUpdate,
            Guid.NewGuid(), version, field,
            new InputContextSnapshot(version, field, "hel", "", 0, true,
                InputFieldSensitivity.KnownSafe));

    [Fact]
    public void ContextMessage_RoundTripsBoundedSnapshot()
    {
        AdapterMessage input = Context();
        AdapterMessage output = AdapterMessageCodec.Deserialize(
            AdapterMessageCodec.Serialize(input));
        Assert.Equal(input, output);
        Assert.Equal("hel", output.Snapshot!.TextBeforeCaret);
    }

    [Fact]
    public void ContextMessage_RejectsMismatchedVersionAndField()
    {
        AdapterMessage message = Context() with { ContextVersion = 5 };
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(message));

        message = Context() with { FieldId = "other" };
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(message));
    }

    [Fact]
    public void Deserialize_RejectsMalformedOversizedAndUnknownVersion()
    {
        Assert.Throws<InvalidDataException>(() =>
            AdapterMessageCodec.Deserialize("{broken"u8));

        Assert.Throws<InvalidDataException>(() =>
            AdapterMessageCodec.Deserialize(new byte[AdapterMessageCodec.MaximumFrameBytes + 1]));

        AdapterMessage message = Context() with { ProtocolVersion = 2 };
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(message));
    }

    [Fact]
    public void Suggestions_RejectMoreThanThreeOrOversizedCandidates()
    {
        AdapterMessage tooMany = new(1, AdapterMessageKind.Suggestions, Guid.NewGuid(),
            4, "field-1", Suggestions: ["a", "b", "c", "d"]);
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(tooMany));

        AdapterMessage longCandidate = tooMany with
        {
            Suggestions = [new string('a', 257)]
        };
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(longCandidate));
    }

    [Fact]
    public void Acceptance_RequiresFieldAndCandidate()
    {
        AdapterMessage missing = new(1, AdapterMessageKind.AcceptSuggestion, Guid.NewGuid(),
            4, "", Candidate: "hello");
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(missing));
    }

    [Fact]
    public void Gate_CancelsPriorRequestAndRejectsStaleIdentity()
    {
        using var gate = new StaleRequestGate();
        PredictionTicket first = gate.Begin(1, "field-a");
        PredictionTicket second = gate.Begin(2, "field-a");

        Assert.True(first.CancellationToken.IsCancellationRequested);
        Assert.False(gate.IsCurrent(first.RequestId, 1, "field-a"));
        Assert.False(gate.IsCurrent(second.RequestId, 2, "field-b"));
        Assert.True(gate.IsCurrent(second.RequestId, 2, "field-a"));

        gate.Invalidate();
        Assert.True(second.CancellationToken.IsCancellationRequested);
        Assert.False(gate.IsCurrent(second.RequestId, 2, "field-a"));
    }

    [Fact]
    public void Gate_DisposeRejectsFurtherRequests()
    {
        var gate = new StaleRequestGate();
        gate.Dispose();
        Assert.Throws<ObjectDisposedException>(() => gate.Begin(1, "field"));
    }
}
