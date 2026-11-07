using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Ranking;
using Xunit;

namespace PCWord.Tests;

public class CandidateRankerTests
{
    private static CandidateRanker CreateRanker()
    {
        var entries = new[]
        {
            new DictionaryEntry("program", 40),
            new DictionaryEntry("progress", 50),
            new DictionaryEntry("product", 35),
            new DictionaryEntry("project", 45),
            new DictionaryEntry("banana", 20)
        };

        return new CandidateRanker(new PrefixIndex(entries));
    }

    [Fact]
    public void GetSuggestions_RanksByCommonnessSize()
    {
        var ranker = CreateRanker();

        var suggestions = ranker.GetSuggestions("pro");

        Assert.Equal(
            new[] { "product", "program", "project" },
            suggestions.Select(entry => entry.Word));
    }

    [Fact]
    public void GetSuggestions_ReturnsAtMostThreeByDefault()
    {
        var ranker = CreateRanker();

        Assert.Equal(3, ranker.GetSuggestions("pro").Count);
    }

    [Fact]
    public void GetSuggestions_RespectsCustomLimit()
    {
        var ranker = CreateRanker();

        Assert.Single(ranker.GetSuggestions("pro", 1));
    }

    [Fact]
    public void GetSuggestions_EmptyPrefixReturnsNoResults()
    {
        var ranker = CreateRanker();

        Assert.Empty(ranker.GetSuggestions(""));
        Assert.Empty(ranker.GetSuggestions("   "));
    }

    [Fact]
    public void GetSuggestions_NonPositiveLimitReturnsNoResults()
    {
        var ranker = CreateRanker();

        Assert.Empty(ranker.GetSuggestions("pro", 0));
        Assert.Empty(ranker.GetSuggestions("pro", -1));
    }

    [Fact]
    public void GetSuggestions_UnknownPrefixReturnsNoResults()
    {
        var ranker = CreateRanker();

        Assert.Empty(ranker.GetSuggestions("xyz"));
    }

    [Fact]
    public void GetSuggestions_PunctuationBoundaryReturnsNoResults()
    {
        var ranker = CreateRanker();

        Assert.Empty(ranker.GetSuggestions("pro."));
    }

    [Fact]
    public void GetSuggestions_TiesAreAlphabetical()
    {
        var entries = new[]
        {
            new DictionaryEntry("apply", 50),
            new DictionaryEntry("apple", 50),
            new DictionaryEntry("application", 50)
        };

        var ranker = new CandidateRanker(new PrefixIndex(entries));

        var suggestions = ranker.GetSuggestions("app");

        Assert.Equal(
            new[] { "apple", "apply", "application" },
            suggestions.Select(entry => entry.Word));
    }

    [Fact]
    public void GetSuggestions_DeduplicatesCaseInsensitiveWordsKeepingBestCommonnessTier()
    {
        var entries = new[]
        {
            new DictionaryEntry("apple", 50),
            new DictionaryEntry("APPLE", 90),
            new DictionaryEntry("application", 80)
        };

        var ranker = new CandidateRanker(new PrefixIndex(entries));

        var suggestions = ranker.GetSuggestions("app", 10);

        Assert.Equal(2, suggestions.Count);
        Assert.Equal("apple", suggestions[0].Word);
        Assert.Equal(50, suggestions[0].CommonnessSize);
        Assert.Equal("application", suggestions[1].Word);
    }

    [Fact]
    public void GetSuggestions_UsesPrefixCompletionBeforeVariantAndAlphabeticTies()
    {
        var ranker = new CandidateRanker(new PrefixIndex(new[]
        {
            new DictionaryEntry("application", DictionaryDialect.All, CommonnessSize: 35, VariantLevel: 0),
            new DictionaryEntry("apple", DictionaryDialect.All, CommonnessSize: 35, VariantLevel: 3),
            new DictionaryEntry("apply", DictionaryDialect.All, CommonnessSize: 35, VariantLevel: 0)
        }));

        Assert.Equal(new[] { "apply", "apple", "application" },
            ranker.GetSuggestions("app", 10).Select(entry => entry.Word));
    }

    [Fact]
    public void GetSuggestions_MissingMetadataIsSafeAndDeterministic()
    {
        var ranker = new CandidateRanker(new PrefixIndex(new[]
        {
            new DictionaryEntry("zebra"),
            new DictionaryEntry("zero", CommonnessSize: 35)
        }));

        Assert.Equal(new[] { "zero", "zebra" }, ranker.GetSuggestions("ze", 10).Select(entry => entry.Word));
        Assert.Equal(new[] { "zero", "zebra" }, ranker.GetSuggestions("ze", 10).Select(entry => entry.Word));
    }
}
