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
    [Fact]
    public void SnapshotCopiesCannotBypassProtocolBounds()
    {
        var valid = Context();
        InputContextSnapshot[] invalid =
        [
            valid.Snapshot! with { TextBeforeCaret = new string('x', 129) },
            valid.Snapshot! with { TextAfterCaret = new string('x', 33) },
            valid.Snapshot! with { SelectionLength = -1 },
            valid.Snapshot! with { Sensitivity = (InputFieldSensitivity)99 },
            valid.Snapshot! with { CaretOffset = -1 },
            valid.Snapshot! with { TextBeforeCaret = "\uD800" }
        ];
        foreach (var snapshot in invalid)
            Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(valid with { Snapshot = snapshot }));
    }

    [Theory]
    [InlineData("Sensitivity", "Sensitive")]
    [InlineData("Sensitivity", "Unknown")]
    [InlineData("HasFocus", false)]
    [InlineData("ContextAvailable", false)]
    [InlineData("IsComposing", true)]
    [InlineData("SelectionLength", 2)]
    public void IneligibleWireSnapshotsCannotCarryText(string name, object value)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(AdapterMessageCodec.Serialize(Context()))!;
        json["Snapshot"]![name] = System.Text.Json.JsonSerializer.SerializeToNode(value);
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Deserialize(
            System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void InvalidationMayCarryMetadataButNoContextText()
    {
        var valid = Context();
        var paused = valid with { Snapshot = valid.Snapshot! with
        {
            HasFocus = false, TextBeforeCaret = null, TextAfterCaret = null
        } };
        Assert.Equal(paused, AdapterMessageCodec.Deserialize(AdapterMessageCodec.Serialize(paused)));
    }

    [Fact]
    public void MalformedConstructorValuesAreSanitizedAtDecodeBoundary()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(AdapterMessageCodec.Serialize(Context()))!;
        json["Snapshot"]!["SelectionLength"] = -1;
        var error = Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Deserialize(
            System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("hel", error.Message);
    }

    [Fact]
    public void RejectsDuplicateAndMissingRequiredWireProperties()
    {
        string json = System.Text.Encoding.UTF8.GetString(AdapterMessageCodec.Serialize(Context()));
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Deserialize(
            System.Text.Encoding.UTF8.GetBytes(json.Replace("{", "{\"ProtocolVersion\":1,", StringComparison.Ordinal))));
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node.AsObject().Remove("ContextVersion");
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Deserialize(
            System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Fact]
    public void RejectsMalformedCandidatesAndDuplicateSuggestions()
    {
        var message = new AdapterMessage(1, AdapterMessageKind.Suggestions, Guid.NewGuid(), 1, "field",
            Suggestions: ["hello", "HELLO"]);
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(message));
        Assert.Throws<InvalidDataException>(() => AdapterMessageCodec.Serialize(message with { Suggestions = ["\uD800"] }));
    }

    [Fact]
    public void GateRejectsDuplicateOrDecreasingVersionsWithoutInvalidatingCurrent()
    {
        using var gate = new StaleRequestGate();
        var current = gate.Begin(5, "a");
        Assert.Throws<ArgumentOutOfRangeException>(() => gate.Begin(5, "a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => gate.Begin(4, "b"));
        Assert.True(gate.IsCurrent(current.RequestId, 5, "a"));
        gate.Invalidate();
        Assert.Throws<ArgumentOutOfRangeException>(() => gate.Begin(5, "a"));
    }

    [Fact]
    public void ReentrantCancellationCannotOverwriteNewerRequest()
    {
        using var gate = new StaleRequestGate();
        var first = gate.Begin(1, "a");
        PredictionTicket? nested = null;
        using var registration = first.CancellationToken.Register(() => nested = gate.Begin(3, "b"));
        var second = gate.Begin(2, "a");
        Assert.NotNull(nested);
        Assert.True(gate.IsCurrent(nested.RequestId, 3, "b"));
        Assert.True(second.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void ThrowingCancellationCannotLeaveOldRequestCurrent()
    {
        using var gate = new StaleRequestGate();
        var first = gate.Begin(1, "a");
        using var registration = first.CancellationToken.Register(() => throw new InvalidOperationException("private callback"));
        var second = gate.Begin(2, "a");
        Assert.True(gate.IsCurrent(second.RequestId, 2, "a"));
        Assert.False(gate.IsCurrent(first.RequestId, 1, "a"));
        Assert.Equal(1, gate.CancellationFailureCount);
    }

    [Fact]
    public async Task CancellationCallbackCanInspectGateFromAnotherThread()
    {
        using var gate = new StaleRequestGate();
        var first = gate.Begin(1, "a");
        bool callbackCompleted = false;
        using var registration = first.CancellationToken.Register(() =>
        {
            callbackCompleted = Task.Run(() => gate.IsCurrent(first.RequestId, 1, "a"))
                .Wait(TimeSpan.FromSeconds(2));
        });
        await Task.Run(gate.Invalidate).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(callbackCompleted);
    }

}
