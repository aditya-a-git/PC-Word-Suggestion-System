using PCWord.Core.Input;
using PCWord.Prediction.Context;
using Xunit;

namespace PCWord.Tests;

public sealed class PredictionContextManagerTests
{
    private static InputContextSnapshot Snapshot(
        long version,
        string? before,
        string? after = "",
        string? field = "editor-1",
        bool focus = true,
        InputFieldSensitivity sensitivity = InputFieldSensitivity.KnownSafe,
        int selectionLength = 0,
        bool composing = false,
        bool available = true) =>
        new(version, field, before, after, selectionLength, focus, sensitivity, composing, available);

    [Fact]
    public void PartialWordProducesCompletionPrefixAndBoundedContext()
    {
        var manager = new PredictionContextManager(maximumPrecedingCharacters: 8, maximumFollowingCharacters: 3);

        Assert.True(manager.TryUpdate(Snapshot(1, "older hello dev", "eloper")));

        Assert.Equal(PredictionContextStatus.PartialWord, manager.Current.Status);
        Assert.Equal("dev", manager.Current.Prefix);
        Assert.Equal("hello dev", manager.Current.PrecedingText);
        Assert.Equal("elo", manager.Current.FollowingText);
        Assert.True(manager.Current.CanPredictCompletion);
        Assert.False(manager.Current.CanPredictNextWord);
    }

    [Theory]
    [InlineData("hello ", PredictionContextStatus.WordBoundary)]
    [InlineData("hello.", PredictionContextStatus.WordBoundary)]
    [InlineData("hello,", PredictionContextStatus.WordBoundary)]
    [InlineData("hello", PredictionContextStatus.PartialWord)]
    [InlineData("", PredictionContextStatus.WordBoundary)]
    public void DistinguishesWordFromBoundary(string before, PredictionContextStatus expected)
    {
        var manager = new PredictionContextManager();

        Assert.True(manager.TryUpdate(Snapshot(1, before)));

        Assert.Equal(expected, manager.Current.Status);
        Assert.Equal(expected == PredictionContextStatus.PartialWord, manager.Current.CanPredictCompletion);
        Assert.Equal(expected == PredictionContextStatus.WordBoundary, manager.Current.CanPredictNextWord);
    }

    [Fact]
    public void IgnoresOlderOrDuplicateVersions()
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(3, "newer"));

        Assert.False(manager.TryUpdate(Snapshot(2, "stale")));
        Assert.False(manager.TryUpdate(Snapshot(3, "duplicate")));
        Assert.Equal(3, manager.Current.Version);
        Assert.Equal("newer", manager.Current.Prefix);
    }

    [Fact]
    public void FieldChangeReplacesPreviousContext()
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(1, "secret-ish", field: "field-a"));

        Assert.True(manager.TryUpdate(Snapshot(2, "ok", field: "field-b")));

        Assert.Equal("field-b", manager.Current.FieldId);
        Assert.Equal("ok", manager.Current.Prefix);
        Assert.DoesNotContain("secret-ish", manager.Current.PrecedingText);
    }

    [Theory]
    [InlineData(false, InputFieldSensitivity.KnownSafe, 0, false, true)]
    [InlineData(true, InputFieldSensitivity.Unknown, 0, false, true)]
    [InlineData(true, InputFieldSensitivity.Sensitive, 0, false, true)]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 1, false, true)]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 0, true, true)]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 0, false, false)]
    public void UnsafeOrUnsupportedContextIsNotPredictable(
        bool focus,
        InputFieldSensitivity sensitivity,
        int selectionLength,
        bool composing,
        bool available)
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(1, "do not expose", focus: focus, sensitivity: sensitivity,
            selectionLength: selectionLength, composing: composing, available: available));

        Assert.NotEqual(PredictionContextStatus.PartialWord, manager.Current.Status);
        Assert.NotEqual(PredictionContextStatus.WordBoundary, manager.Current.Status);
        Assert.Equal(string.Empty, manager.Current.Prefix);
        Assert.Equal(string.Empty, manager.Current.PrecedingText);
        Assert.Equal(string.Empty, manager.Current.FollowingText);
    }

    [Fact]
    public void MissingFieldIdentityIsIneligible()
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(1, "hello", field: null));

        Assert.Equal(PredictionContextStatus.Ineligible, manager.Current.Status);
        Assert.Empty(manager.Current.Prefix);
    }

    [Fact]
    public void UnavailableContextIsRepresentedExplicitly()
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(1, null, null, available: false));

        Assert.Equal(PredictionContextStatus.Unavailable, manager.Current.Status);
    }

    [Fact]
    public void InvalidateClearsContextAndRejectsOlderSnapshots()
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(1, "hello"));
        manager.Invalidate(2);

        Assert.Equal(PredictionContextStatus.Unavailable, manager.Current.Status);
        Assert.False(manager.TryUpdate(Snapshot(1, "stale")));
    }

    [Fact]
    public void TrimmingDoesNotSplitSurrogatePairs()
    {
        var manager = new PredictionContextManager(maximumPrecedingCharacters: 2, maximumFollowingCharacters: 2);
        manager.TryUpdate(Snapshot(1, "x𐐀", "𐐀y"));

        Assert.Equal("𐐀", manager.Current.PrecedingText);
        Assert.Equal("𐐀", manager.Current.FollowingText);
    }

    [Fact]
    public void PunctuationInsideTextDoesNotBecomePartOfWordPrefix()
    {
        var manager = new PredictionContextManager();
        manager.TryUpdate(Snapshot(1, "we can't"));

        Assert.Equal("can't", manager.Current.Prefix);
    }

    [Fact]
    public void RejectsNegativeSelectionLength()
    {
        var manager = new PredictionContextManager();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.TryUpdate(Snapshot(1, "hello", selectionLength: -1)));
    }
}
