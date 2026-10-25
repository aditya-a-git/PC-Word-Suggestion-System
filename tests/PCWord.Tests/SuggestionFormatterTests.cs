
using PCWord.Prediction.Ranking;
using Xunit;

namespace PCWord.Tests;

public class SuggestionFormatterTests
{
    [Theory]
    [InlineData("pro", "programming", "programming")]
    [InlineData("Pro", "programming", "Programming")]
    [InlineData("PRO", "programming", "PROGRAMMING")]
    [InlineData("pRo", "programming", "programming")]
    [InlineData("", "programming", "programming")]
    [InlineData("pro", "", "")]
    public void MatchPrefixCasing_ReturnsExpectedCase(
        string prefix,
        string candidate,
        string expected)
    {
        Assert.Equal(
            expected,
            SuggestionFormatter.MatchPrefixCasing(prefix, candidate));
    }
}
