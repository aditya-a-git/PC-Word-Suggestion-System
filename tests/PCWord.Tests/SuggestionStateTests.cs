using Xunit;
using PCWord.Core.Suggestions;

namespace PCWord.Tests;

public sealed class SuggestionStateTests
{
    [Fact]
    public void TryUpdate_CapsSuggestionsAtThreeAndDeduplicatesCaseInsensitively()
    {
        var state = new SuggestionState();

        bool accepted = state.TryUpdate(1, "editor-1",
            new[] { "program", "Program", "progress", "project", "processor" });

        Assert.True(accepted);
        Assert.Equal(new[] { "program", "progress", "project" }, state.Suggestions);
        Assert.True(state.IsVisible);
    }

    [Fact]
    public void TryUpdate_RejectsStaleOrEqualVersions()
    {
        var state = new SuggestionState();
        state.TryUpdate(4, "editor-1", new[] { "current" });

        Assert.False(state.TryUpdate(3, "editor-1", new[] { "stale" }));
        Assert.False(state.TryUpdate(4, "editor-1", new[] { "same-version" }));
        Assert.Equal(new[] { "current" }, state.Suggestions);
    }

    [Fact]
    public void TryUpdate_EmptyCandidatesHidesPopup()
    {
        var state = new SuggestionState();
        state.TryUpdate(1, "editor-1", new[] { "program" });

        Assert.True(state.TryUpdate(2, "editor-1", Array.Empty<string>()));

        Assert.False(state.IsVisible);
        Assert.Empty(state.Suggestions);
    }

    [Fact]
    public void TryUpdate_UnknownFieldClearsCandidates()
    {
        var state = new SuggestionState();
        state.TryUpdate(1, "editor-1", new[] { "program" });

        state.TryUpdate(2, null, new[] { "stale" });

        Assert.False(state.IsVisible);
        Assert.Null(state.FieldId);
    }

    [Fact]
    public void TryAccept_RequiresExactVersionFieldAndValidIndex()
    {
        var state = new SuggestionState();
        state.TryUpdate(10, "editor-1", new[] { "program", "progress" });

        Assert.False(state.TryAccept(9, "editor-1", 0, out _));
        Assert.False(state.TryAccept(10, "editor-2", 0, out _));
        Assert.False(state.TryAccept(10, "editor-1", 2, out _));
        Assert.True(state.TryAccept(10, "editor-1", 1, out string candidate));
        Assert.Equal("progress", candidate);
    }

    [Fact]
    public void Dismiss_ClearsCandidatesAndRejectsOlderDismissal()
    {
        var state = new SuggestionState();
        state.TryUpdate(5, "editor-1", new[] { "program" });

        Assert.False(state.Dismiss(4));
        Assert.True(state.IsVisible);
        Assert.True(state.Dismiss(5));
        Assert.False(state.IsVisible);
    }
}
