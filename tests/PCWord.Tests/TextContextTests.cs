using PCWord.Core;
using Xunit;

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


    [Fact]
    public void CurrentWordAndTypedPrefixAreSeparate()
    {
        var context = At("developer", 2);

        Assert.Equal("developer", context.CurrentWord);
        Assert.Equal("de", context.TypedPrefix);
    }

    [Fact]
    public void TypedPrefixAtWordStartIsEmpty()
    {
        var context = At("hello world", 6);

        Assert.Equal("world", context.CurrentWord);
        Assert.Equal("", context.TypedPrefix);
    }

    [Fact]
    public void TypedPrefixAtWordEndIsTheFullWord()
    {
        var context = At("hello!", 5);

        Assert.Equal("hello", context.CurrentWord);
        Assert.Equal("hello", context.TypedPrefix);
    }

    [Fact] public void UnicodeLettersAreWordCharacters() => Assert.Equal("café", At("café noir", 4).CurrentWord);
    [Fact]
    public void SupplementaryUnicodeRuneIsNotSplit()
    {
        var context = At("a𐐀b", 3);

        Assert.Equal("a𐐀b", context.CurrentWord);
        Assert.Equal("a𐐀", context.TypedPrefix);
    }
    [Fact]
    public void SelectionMakesContextIneligibleAndEditReplacesSelection()
    {
        var context = TextContext.Capture("hello world", 6, 5, true);
        Assert.True(context.HasSelection); Assert.False(context.IsEligible); Assert.Equal("", context.CurrentWord);
        // Explicit edits of a selected range are permitted by the controlled editor.
        var selectedEdit = new TextContext("hello world", 6, 5, 6, true).ApplyExplicitEdit("earth");
        Assert.Equal("hello earth", selectedEdit.Text); Assert.Equal(11, selectedEdit.Caret);
    }

    [Fact]
    public void EditReplacesOnlyCurrentWordAndPreservesSuffixAndPunctuation()
    {
        var result = At("say he!", 6).ApplyExplicitEdit("hello");
        Assert.Equal("say hello!", result.Text); Assert.Equal(9, result.Caret);
    }

    [Fact]
    public void BackspaceCaretAndWordStartEditDoNotDropSuffix()
    {
        var result = At("hello world", 6).ApplyExplicitEdit("earth");
        Assert.Equal("hello earth", result.Text); Assert.Equal(11, result.Caret);
    }

    [Fact]
    public void BoundedContextStopsAtCaretAndRespectsMaximum()
    {
        Assert.Equal("2345", At("012345678", 6).PrecedingContext(4));
        Assert.Equal("", At("abc", 0).PrecedingContext(10));
        Assert.Equal("abc", At("abc", 3).PrecedingContext(100));
    }

    [Fact]
    public void ContextIsUnavailableWithoutFocusOrForSensitiveField()
    {
        Assert.False(At("secret", 3, focus: false).IsEligible);
        Assert.Equal("", At("secret", 3, focus: false).CurrentWord);
        Assert.False(At("secret", 3, sensitive: true).IsEligible);
        Assert.Equal("", At("secret", 3, sensitive: true).PrecedingContext());
        Assert.Throws<InvalidOperationException>(() => At("secret", 3, sensitive: true).ApplyExplicitEdit("x"));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    [InlineData(2, 5)]
    public void RejectsInvalidSelections(int start, int length) => Assert.Throws<ArgumentOutOfRangeException>(() => TextContext.Capture("abcd", start, length, true));

    [Fact] public void RejectsNegativeContextLimit() => Assert.Throws<ArgumentOutOfRangeException>(() => At("abc", 3).PrecedingContext(-1));


    [Theory]
    [InlineData("hello!", 5, "earth!", 11)]
    [InlineData("hello world", 6, "hello earth", 11)]
    [InlineData("say he!", 6, "say hello!", 9)]
    public void ExplicitEditReplacesWordAndPreservesSuffix(
        string text, int caret, string expected, int expectedCaret)
    {
        var result = At(text, caret).ApplyExplicitEdit(
            caret == 6 && text == "say he!" ? "hello" : "earth");

        Assert.Equal(expected, result.Text);
        Assert.Equal(expectedCaret, result.Caret);
    }

    [Fact]
    public void EditAtWordStartDoesNotDuplicateWord()
    {
        var result = At("hello world!", 6).ApplyExplicitEdit("earth");

        Assert.Equal("hello earth!", result.Text);
        Assert.Equal(11, result.Caret);
    }

    [Fact]
    public void EditAtWordEndPreservesPunctuation()
    {
        var result = At("hello!", 5).ApplyExplicitEdit("greetings");

        Assert.Equal("greetings!", result.Text);
        Assert.Equal(9, result.Caret);
    }

}
