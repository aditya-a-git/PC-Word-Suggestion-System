using PCWord.Prediction.Dictionary;

namespace PCWord.Prediction.Ranking;

public sealed class CandidateRanker
{
    private readonly PrefixIndex _index;

    public CandidateRanker(PrefixIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        _index = index;
    }

    public IReadOnlyList<DictionaryEntry> GetSuggestions(
        string prefix,
        int maxResults = 3)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (string.IsNullOrWhiteSpace(prefix) || maxResults <= 0)
            return Array.Empty<DictionaryEntry>();

        return _index.FindByPrefix(prefix)
            .OrderByDescending(entry => entry.Frequency)
            .ThenBy(entry => entry.Word, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Word, StringComparer.Ordinal)
            .DistinctBy(
                entry => entry.Word,
                StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }
}
