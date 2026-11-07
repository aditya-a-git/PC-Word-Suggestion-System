
using System.Globalization;
using System.Text;

namespace PCWord.Prediction.Dictionary;

public static class DictionaryLoader
{
    private const int MaxLineLength = 256;
    private const int MaxEntries = 5_000_000;
    private const string FormatHeader = "#pcword-dictionary-v2";

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
        var formatVersion = 1;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (lineNumber == 1 && line == FormatHeader)
            {
                formatVersion = 2;
                continue;
            }

            if (line.Length > MaxLineLength)
            {
                throw new InvalidDataException(
                    $"Line {lineNumber} exceeds the maximum length.");
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = line.Split('\t');
            if ((formatVersion == 1 && fields.Length != 2) ||
                (formatVersion == 2 && fields.Length != 7))
            {
                throw new InvalidDataException(
                    $"Invalid format on line {lineNumber}. " +
                    (formatVersion == 2
                        ? "Expected word, dialects, variant group, commonness size, part of speech, variant level, and lemmas."
                        : "Expected word<TAB>frequency."));
            }

            var word = fields[0].Trim().Normalize(NormalizationForm.FormC);

            if (word.Length == 0 ||
                word.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException(
                    $"Invalid word on line {lineNumber}.");
            }

            if (formatVersion == 1)
            {
                if (!long.TryParse(fields[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long legacyWeight) ||
                    legacyWeight <= 0)
                    throw new InvalidDataException($"Invalid legacy weight on line {lineNumber}.");

                if (seenWords.Add(word))
                {
                    if (entries.Count >= MaxEntries)
                        throw new InvalidDataException($"Dictionary exceeds {MaxEntries} entries.");
                    entries.Add(new DictionaryEntry(word, DictionaryDialect.All, LegacyWeight: legacyWeight));
                }
                continue;
            }

            DictionaryDialect dialects = ParseDialects(fields[1], lineNumber);
            int? commonnessSize = ParseOptionalInt(fields[3], 35, 95, "commonness size", lineNumber);
            int? variantLevel = ParseOptionalInt(fields[5], 0, 9, "variant level", lineNumber);
            if (dialects == DictionaryDialect.None)
                throw new InvalidDataException($"Missing dialect on line {lineNumber}.");
            if (!seenWords.Add(word))
                continue;

            if (entries.Count >= MaxEntries)
            {
                throw new InvalidDataException(
                    $"Dictionary exceeds {MaxEntries} entries.");
            }

            entries.Add(new DictionaryEntry(word, dialects, fields[2], commonnessSize,
                fields[4], variantLevel, fields[6] == "-" ? string.Empty : fields[6]));
        }

        return entries.AsReadOnly();
    }

    private static int? ParseOptionalInt(string value, int min, int max, string field, int lineNumber)
    {
        if (value.Length == 0) return null;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < min || parsed > max)
            throw new InvalidDataException($"Invalid {field} on line {lineNumber}.");
        return parsed;
    }

    private static DictionaryDialect ParseDialects(string value, int lineNumber)
    {
        DictionaryDialect result = DictionaryDialect.None;
        foreach (string code in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= code switch
            {
                "A" => DictionaryDialect.American,
                "B" => DictionaryDialect.BritishIse,
                "Z" => DictionaryDialect.BritishIze,
                _ => throw new InvalidDataException($"Unknown dialect on line {lineNumber}.")
            };
        }
        return result;
    }
}
