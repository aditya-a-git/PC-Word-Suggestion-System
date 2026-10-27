using PCWord.Core.Input;
using Xunit;

namespace PCWord.Tests;

public sealed class InputContextSnapshotTests
{
    [Fact]
    public void RejectsOversizedFieldIdentityAndContextPayloads()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(fieldId: new string('x', 257)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(before: new string('x', 129)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(after: new string('x', 33)));
    }

    [Fact]
    public void RejectsNegativeVersionOrSelectionLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(version: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(selectionLength: -1));
    }

    [Fact]
    public void RejectsMalformedUtf16AndInvalidCaretGeometry()
    {
        Assert.Throws<ArgumentException>(() => Create(before: "broken\uD800"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(
            caretBounds: new CaretScreenBounds(10, 10, 10, 20, 96)));
    }

    [Fact]
    public void CarriesCaretOffsetAndDpiAwareScreenBounds()
    {
        var bounds = new CaretScreenBounds(100, 200, 101, 220, 144);
        InputContextSnapshot snapshot = Create(caretBounds: bounds, caretOffset: 17);

        Assert.Equal(bounds, snapshot.CaretBounds);
        Assert.Equal(17, snapshot.CaretOffset);
    }

    private static InputContextSnapshot Create(
        long version = 1,
        string? fieldId = "field-1",
        string? before = "hello",
        string? after = "!",
        int selectionLength = 0,
        CaretScreenBounds? caretBounds = null,
        int? caretOffset = 5) =>
        new(version, fieldId, before, after, selectionLength, true,
            InputFieldSensitivity.KnownSafe, ContextAvailable: true,
            CaretBounds: caretBounds, CaretOffset: caretOffset);
}
