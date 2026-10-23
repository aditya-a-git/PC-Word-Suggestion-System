using PCWord.Core;

namespace PCWord.Tests;

public sealed class TextContextTests
{
    private static TextContext At(string text, int caret, bool focus = true, bool sensitive = false) => TextContext.Capture(text, caret, 0, focus, sensitive);

    [Theory]
    [InlineData("hello world", 0, "hello")]
    [InlineData("hello world", 5, "hello")]
    [InlineData("hello world", 6, "world")]
    [InlineData("hello world", 11, "world")]
    [InlineData("one two three", 7, "two")]
    [InlineData("", 0, "")]
    [InlineData("  word!", 0, "")]
    [InlineData("word!", 5, "")]
    [InlineData("can't-stop", 7, "stop")]
    [InlineData("abc_def", 5, "abc_def")]
    public void CurrentWordUsesCaretAndWordBoundaries(string text, int caret, string expected) => Assert.Equal(expected, At(text, caret).CurrentWord);

    [Fact] public void CaretAtMiddleUsesOnlyPrefixForCurrentWord() => Assert.Equal("de", At("developer", 2).CurrentWord);
    [Fact] public void UnicodeLettersAreWordCharacters() => Assert.Equal("café", At("café noir", 4).CurrentWord);
    [Fact] public void SupplementaryUnicodeRuneIsNotSplit() => Assert.Equal("a𐐀", At("a𐐀b", 3).CurrentWord);

    [Fact] public void SelectionMakesContextIneligibleAndEditReplacesSelection()
    {
        var context = TextContext.Capture("hello world", 6, 5, true);
        Assert.True(context.HasSelection); Assert.False(context.IsEligible); Assert.Equal("", context.CurrentWord);
        // Explicit edits of a selected range are permitted by the controlled editor.
        var selectedEdit = new TextContext("hello world", 6, 5, 6, true).ApplyExplicitEdit("earth");
        Assert.Equal("hello earth", selectedEdit.Text); Assert.Equal(11, selectedEdit.Caret);
    }

    [Fact] public void EditReplacesOnlyCurrentWordAndPreservesSuffixAndPunctuation()
    {
        var result = At("say he!", 6).ApplyExplicitEdit("hello");
        Assert.Equal("say hello!", result.Text); Assert.Equal(9, result.Caret);
    }

    [Fact] public void BackspaceCaretAndWordStartEditDoNotDropSuffix()
    {
        var result = At("hello world", 6).ApplyExplicitEdit("earth");
        Assert.Equal("hello earth", result.Text); Assert.Equal(11, result.Caret);
    }

    [Fact] public void BoundedContextStopsAtCaretAndRespectsMaximum()
    {
        Assert.Equal("2345", At("012345678", 6).PrecedingContext(4));
        Assert.Equal("", At("abc", 0).PrecedingContext(10));
        Assert.Equal("abc", At("abc", 3).PrecedingContext(100));
    }

    [Fact] public void ContextIsUnavailableWithoutFocusOrForSensitiveField()
    {
        Assert.False(At("secret", 3, focus: false).IsEligible);
        Assert.Equal("", At("secret", 3, focus: false).CurrentWord);
        Assert.False(At("secret", 3, sensitive: true).IsEligible);
        Assert.Equal("", At("secret", 3, sensitive: true).PrecedingContext());
        Assert.Throws<InvalidOperationException>(() => At("secret", 3, sensitive: true).ApplyExplicitEdit("x"));
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(5, 0)] [InlineData(2, 5)]
    public void RejectsInvalidSelections(int start, int length) => Assert.Throws<ArgumentOutOfRangeException>(() => TextContext.Capture("abcd", start, length, true));

    [Fact] public void RejectsNegativeContextLimit() => Assert.Throws<ArgumentOutOfRangeException>(() => At("abc", 3).PrecedingContext(-1));
}
