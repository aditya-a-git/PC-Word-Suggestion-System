
using System.Globalization;
using System.Text;

namespace PCWord.Prediction.Dictionary;

public static class DictionaryLoader
{
    private const int MaxLineLength = 256;
    private const int MaxEntries = 5_000_000;

    public static IReadOnlyList<DictionaryEntry> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static IReadOnlyList<DictionaryEntry> Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var entries = new List<DictionaryEntry>();
        var seenWords = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (line.Length > MaxLineLength)
            {
                throw new InvalidDataException(
                    $"Line {lineNumber} exceeds the maximum length.");
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var separator = line.IndexOf('\t');

            if (separator <= 0 ||
                separator == line.Length - 1 ||
                line.IndexOf('\t', separator + 1) >= 0)
            {
                throw new InvalidDataException(
                    $"Invalid format on line {lineNumber}. " +
                    "Expected word<TAB>frequency.");
            }

            var word = line[..separator].Trim();
            var frequencyText = line[(separator + 1)..].Trim();

            if (word.Length == 0 ||
                word.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException(
                    $"Invalid word on line {lineNumber}.");
            }

            if (!long.TryParse(
                    frequencyText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var frequency) ||
                frequency <= 0)
            {
                throw new InvalidDataException(
                    $"Invalid frequency on line {lineNumber}.");
            }

            if (!seenWords.Add(word))
            {
                throw new InvalidDataException(
                    $"Duplicate word on line {lineNumber}: {word}");
            }

            if (entries.Count >= MaxEntries)
            {
                throw new InvalidDataException(
                    $"Dictionary exceeds {MaxEntries} entries.");
            }

            entries.Add(new DictionaryEntry(word, frequency));
        }

        return entries.AsReadOnly();
    }
}
