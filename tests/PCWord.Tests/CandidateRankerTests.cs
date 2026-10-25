
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
            new DictionaryEntry("program", 90),
            new DictionaryEntry("progress", 70),
            new DictionaryEntry("product", 100),
            new DictionaryEntry("project", 80),
            new DictionaryEntry("banana", 200)
        };

        return new CandidateRanker(new PrefixIndex(entries));
    }

    [Fact]
    public void GetSuggestions_RanksByFrequency()
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
            new[] { "apple", "application", "apply" },
            suggestions.Select(entry => entry.Word));
    }
}
