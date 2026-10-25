
namespace PCWord.Prediction.Ranking;

public static class SuggestionFormatter
{
    public static string MatchPrefixCasing(
        string prefix,
        string candidate)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(candidate);

        if (prefix.Length == 0 || candidate.Length == 0)
            return candidate;

        // If the user typed the prefix entirely in uppercase,
        // uppercase the whole suggestion.
        if (prefix.All(char.IsUpper))
            return candidate.ToUpperInvariant();

        // If the first character is uppercase and the rest
        // are lowercase, use title-style casing.
        if (char.IsUpper(prefix[0]) &&
            prefix.Skip(1).All(char.IsLower))
        {
            return char.ToUpperInvariant(candidate[0]) +
                   candidate[1..];
        }

        // Otherwise, preserve the dictionary's casing.
        return candidate;
    }
}
