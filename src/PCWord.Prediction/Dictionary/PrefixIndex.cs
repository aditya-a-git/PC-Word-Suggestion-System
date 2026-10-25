
namespace PCWord.Prediction.Dictionary;

public sealed class PrefixIndex
{
    private readonly DictionaryEntry[] _entries;

    public PrefixIndex(IEnumerable<DictionaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries
            .OrderBy(e => e.Word, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<DictionaryEntry> FindByPrefix(
        string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (prefix.Length == 0)
            return Array.Empty<DictionaryEntry>();

        var start = FindFirstMatch(prefix);
        var matches = new List<DictionaryEntry>();

        for (var i = start; i < _entries.Length; i++)
        {
            var entry = _entries[i];

            if (!entry.Word.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            matches.Add(entry);
        }

        return matches;
    }

    private int FindFirstMatch(string prefix)
    {
        var low = 0;
        var high = _entries.Length;

        while (low < high)
        {
            var mid = low + (high - low) / 2;

            if (StringComparer.OrdinalIgnoreCase.Compare(
                    _entries[mid].Word, prefix) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
