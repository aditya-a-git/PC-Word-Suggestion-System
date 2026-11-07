using System.Text;
using PCWord.Prediction.Dictionary;
using Xunit;

namespace PCWord.Tests;

public class DictionaryTests
{
    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void Load_ValidDictionary_ReturnsEntries()
    {
        using var stream = CreateStream(
            "apple\t100\napplication\t80\napply\t60\n");

        var entries = DictionaryLoader.Load(stream);

        Assert.Equal(3, entries.Count);
        Assert.Equal("apple", entries[0].Word);
        Assert.Equal(100, entries[0].LegacyWeight);
    }

    [Fact]
    public void Load_UnicodeWords_PreservesText()
    {
        using var stream = CreateStream("café\t100\nnaïve\t80\n");

        var entries = DictionaryLoader.Load(stream);

        Assert.Equal(new[] { "café", "naïve" },
            entries.Select(entry => entry.Word));
    }

    [Fact]
    public void Load_InvalidLegacyWeight_Throws()
    {
        using var stream = CreateStream("apple\tzero\n");

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
    }

    [Theory]
    [InlineData("apple\t0\n")]
    [InlineData("apple\t-1\n")]
    [InlineData("apple\t1.5\n")]
    public void Load_InvalidLegacyWeights_Throws(string input)
    {
        using var stream = CreateStream(input);

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
    }

    [Fact]
    public void Load_DuplicateWordsIgnoringCase_UsesFirstEntry()
    {
        using var stream = CreateStream(
            "apple\t100\nAPPLE\t50\n");

        var entries = DictionaryLoader.Load(stream);
        Assert.Single(entries);
        Assert.Equal("apple", entries[0].Word);
        Assert.Equal(100, entries[0].LegacyWeight);
    }

    [Theory]
    [InlineData("apple 100\n")]
    [InlineData("apple\t100\textra\n")]
    [InlineData("new york\t100\n")]
    public void Load_MalformedRows_Throws(string input)
    {
        using var stream = CreateStream(input);

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
    }

    [Fact]
    public void Load_OverlongLine_Throws()
    {
        using var stream = CreateStream(new string('a', 257) + "\t1\n");

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
    }

    [Fact]
    public void FindByPrefix_ReturnsMatchingWords()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("apple", 100),
            new DictionaryEntry("application", 80),
            new DictionaryEntry("banana", 70),
            new DictionaryEntry("apply", 60)
        });

        var matches = index.FindByPrefix("app");

        Assert.Equal(
            new[] { "apple", "application", "apply" },
            matches.Select(entry => entry.Word));
    }

    [Fact]
    public void FindByPrefix_IsCaseInsensitive()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("program", 100),
            new DictionaryEntry("progress", 80)
        });

        var matches = index.FindByPrefix("PRO");

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void FindByPrefix_UnicodePrefixIsCaseInsensitive()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("café", 100),
            new DictionaryEntry("naïve", 80)
        });

        var matches = index.FindByPrefix("CAF");

        Assert.Equal(new[] { "café" },
            matches.Select(entry => entry.Word));
    }

    [Fact]
    public void FindByPrefix_NumericPrefixWorks()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("123abc", 100),
            new DictionaryEntry("42", 80),
            new DictionaryEntry("apple", 60)
        });

        var matches = index.FindByPrefix("12");

        Assert.Equal(new[] { "123abc" },
            matches.Select(entry => entry.Word));
    }

    [Fact]
    public void FindByPrefix_PunctuationInsideWordIsPreserved()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("can't", 100),
            new DictionaryEntry("cannot", 80)
        });

        var matches = index.FindByPrefix("can'");

        Assert.Equal(new[] { "can't" },
            matches.Select(entry => entry.Word));
    }

    [Fact]
    public void FindByPrefix_PunctuationAttachedToPrefixDoesNotMatch()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("apple", 100),
            new DictionaryEntry("application", 80)
        });

        Assert.Empty(index.FindByPrefix("app,"));
    }

    [Fact]
    public void FindByPrefix_EmptyPrefix_ReturnsNoMatches()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("apple", 100)
        });

        Assert.Empty(index.FindByPrefix(""));
    }

    [Fact]
    public void FindByPrefix_UnknownPrefix_ReturnsNoMatches()
    {
        var index = new PrefixIndex(new[]
        {
            new DictionaryEntry("apple", 100),
            new DictionaryEntry("banana", 80)
        });

        Assert.Empty(index.FindByPrefix("xyz"));
    }

    [Fact]
    public void FindByPrefix_EmptyDictionary_ReturnsNoMatches()
    {
        var index = new PrefixIndex(
            Array.Empty<DictionaryEntry>());

        Assert.Empty(index.FindByPrefix("app"));
    }

    [Fact]
    public void Load_V2Format_PreservesDialectAndVariantMetadata()
    {
        using var stream = CreateStream(
            "#pcword-dictionary-v2\n" +
            "color\tA\t123\t35\tn0\t0\tcolor\n" +
            "colour\tB,Z\t123\t35\tn0\t0\tcolour\n");

        IReadOnlyList<DictionaryEntry> entries = DictionaryLoader.Load(stream);

        Assert.Equal(2, entries.Count);
        Assert.Equal(DictionaryDialect.American, entries[0].Dialects);
        Assert.Equal(DictionaryDialect.BritishIse | DictionaryDialect.BritishIze, entries[1].Dialects);
        Assert.Equal("123", entries[0].VariantGroup);
        Assert.Equal(35, entries[1].CommonnessSize);
        Assert.Equal("n0", entries[1].PartOfSpeech);
        Assert.Equal("colour", entries[1].Lemmas);
    }

    [Fact]
    public void Load_V2Format_RejectsUnknownDialectAndMalformedMetadata()
    {
        using var badDialect = CreateStream("#pcword-dictionary-v2\ncolor\tC\t1\t35\tn0\t0\tcolor\n");
        using var badSize = CreateStream("#pcword-dictionary-v2\ncolor\tA\t1\t999\tn0\t0\tcolor\n");

        Assert.Throws<InvalidDataException>(() => DictionaryLoader.Load(badDialect));
        Assert.Throws<InvalidDataException>(() => DictionaryLoader.Load(badSize));
    }

    [Fact]
    public void BundledDictionary_ContainsIndependentAmericanAndBritishSpellings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Dictionary", "esdb-en-bilingual.tsv");
        Assert.True(File.Exists(path), $"Bundled dictionary was not copied to {path}.");
        IReadOnlyList<DictionaryEntry> entries = DictionaryLoader.Load(path);
        var ranker = new PCWord.Prediction.Ranking.CandidateRanker(new PrefixIndex(entries));
        Assert.True(entries.Count > 80_000);
        Assert.Contains("color", ranker.GetSuggestions("colo", 20).Select(entry => entry.Word));
        Assert.Contains("colour", ranker.GetSuggestions("colou", 20).Select(entry => entry.Word));
        Assert.Contains("center", ranker.GetSuggestions("cent", 20).Select(entry => entry.Word));
        Assert.Contains("centre", ranker.GetSuggestions("cent", 20).Select(entry => entry.Word));
        Assert.Contains("organize", ranker.GetSuggestions("organi", 20).Select(entry => entry.Word));
        Assert.Contains("organise", ranker.GetSuggestions("organi", 20).Select(entry => entry.Word));
        Assert.Contains("analyze", ranker.GetSuggestions("analy", 20).Select(entry => entry.Word));
        Assert.Contains("analyse", ranker.GetSuggestions("analy", 20).Select(entry => entry.Word));
        Assert.Contains("traveling", ranker.GetSuggestions("travel", 20).Select(entry => entry.Word));
        Assert.Contains("travelling", ranker.GetSuggestions("travel", 20).Select(entry => entry.Word));
        Assert.Contains("computer", ranker.GetSuggestions("comput", 20).Select(entry => entry.Word));
        Assert.Equal(entries.Single(entry => entry.Word == "color").VariantGroup,
            entries.Single(entry => entry.Word == "colour").VariantGroup);
        Assert.NotEqual(DictionaryDialect.None,
            entries.Single(entry => entry.Word == "color").Dialects & DictionaryDialect.American);
        Assert.NotEqual(DictionaryDialect.None,
            entries.Single(entry => entry.Word == "colour").Dialects & DictionaryDialect.BritishIse);
    }
}
