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
        Assert.Equal(100, entries[0].Frequency);
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
    public void Load_InvalidFrequency_Throws()
    {
        using var stream = CreateStream("apple\tzero\n");

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
    }

    [Theory]
    [InlineData("apple\t0\n")]
    [InlineData("apple\t-1\n")]
    [InlineData("apple\t1.5\n")]
    public void Load_NonPositiveOrNonIntegerFrequency_Throws(string input)
    {
        using var stream = CreateStream(input);

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
    }

    [Fact]
    public void Load_DuplicateWordsIgnoringCase_Throws()
    {
        using var stream = CreateStream(
            "apple\t100\nAPPLE\t50\n");

        Assert.Throws<InvalidDataException>(
            () => DictionaryLoader.Load(stream));
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
}
