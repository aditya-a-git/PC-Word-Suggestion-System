using PCWord.Core.Input;
using Xunit;

namespace PCWord.Tests;

public sealed class TextEditValidatorTests
{
    private static InputContextSnapshot Snapshot(
        long version = 3,
        string? field = "field-a",
        string? before = "typed hel",
        string? after = "!",
        int selection = 0,
        bool focus = true,
        InputFieldSensitivity sensitivity = InputFieldSensitivity.KnownSafe,
        bool composing = false,
        bool available = true) => new(version, field, before, after, selection, focus, sensitivity, composing, available);

    private static TextEditRequest Request(long version = 3, string field = "field-a", string expected = "hel", string replacement = "hello") =>
        new(version, field, expected, replacement);

    private static TextEditValidationResult Validate(
        InputContextSnapshot? snapshot = null,
        TextEditRequest? request = null,
        long currentVersion = 3,
        IReadOnlyCollection<string>? candidates = null) => TextEditValidator.Validate(
            snapshot ?? Snapshot(), request ?? Request(), currentVersion, candidates ?? new[] { "hello" });

    [Fact]
    public void AcceptsCurrentCandidateAtWordEndBeforePunctuation() => Assert.True(Validate().IsValid);

    [Fact]
    public void RejectsOldVersion()
    {
        var result = Validate(request: Request(version: 2));
        Assert.False(result.IsValid);
        Assert.Equal(TextEditStatus.StaleContext, result.RefusalStatus);
    }

    [Fact]
    public void RejectsChangedFieldIdentity()
    {
        var result = Validate(request: Request(field: "field-b"));
        Assert.Equal(TextEditStatus.StaleContext, result.RefusalStatus);
    }

    [Fact]
    public void RejectsChangedTypedPrefix()
    {
        var result = Validate(snapshot: Snapshot(before: "typed hi"));
        Assert.Equal(TextEditStatus.StaleContext, result.RefusalStatus);
    }

    [Fact]
    public void RejectsCandidateNotInCurrentPredictionResult()
    {
        var result = Validate(request: Request(replacement: "help"));
        Assert.Equal(TextEditStatus.Failed, result.RefusalStatus);
    }

    [Theory]
    [InlineData(InputFieldSensitivity.Unknown, TextEditStatus.Unsupported)]
    [InlineData(InputFieldSensitivity.Sensitive, TextEditStatus.RefusedSensitiveField)]
    public void RejectsUnknownOrSensitiveField(InputFieldSensitivity sensitivity, TextEditStatus status)
    {
        var result = Validate(snapshot: Snapshot(sensitivity: sensitivity));
        Assert.Equal(status, result.RefusalStatus);
    }

    [Fact]
    public void RejectsUnavailableOrUnfocusedContext()
    {
        Assert.Equal(TextEditStatus.Unsupported, Validate(snapshot: Snapshot(available: false)).RefusalStatus);
        Assert.Equal(TextEditStatus.Unsupported, Validate(snapshot: Snapshot(focus: false)).RefusalStatus);
    }

    [Fact]
    public void RejectsSelectionAndImeComposition()
    {
        Assert.Equal(TextEditStatus.Unsupported, Validate(snapshot: Snapshot(selection: 1)).RefusalStatus);
        Assert.Equal(TextEditStatus.Unsupported, Validate(snapshot: Snapshot(composing: true)).RefusalStatus);
    }

    [Fact]
    public void RejectsCaretInMiddleOfWordBecauseEditSpanIsUnverified()
    {
        var result = Validate(snapshot: Snapshot(after: "low!") );
        Assert.Equal(TextEditStatus.Unsupported, result.RefusalStatus);
    }

    [Fact]
    public void RejectsReplacementThatDoesNotExtendPrefix()
    {
        var result = Validate(request: Request(replacement: "he"), candidates: new[] { "he" });
        Assert.Equal(TextEditStatus.Failed, result.RefusalStatus);
    }
}
