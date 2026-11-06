using System.Globalization;
using System.Security.Cryptography;
using System.Text;

if (args.Length != 3 || args[0] != "--sha256" ||
    args[1].Length != 64 || args[1].Any(c => !Uri.IsHexDigit(c)))
{
    Console.Error.WriteLine("Usage: PCWord.DictionaryImport --sha256 <expected-source-sha256> <source-wordlist.txt>");
    return 2;
}

string sourcePath = Path.GetFullPath(args[2]);
string expectedHash = args[1].ToLowerInvariant();
string actualHash;
using (FileStream source = File.OpenRead(sourcePath))
    actualHash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();

if (!StringComparer.Ordinal.Equals(expectedHash, actualHash))
{
    Console.Error.WriteLine($"Source checksum mismatch: expected {expectedHash}, received {actualHash}.");
    return 1;
}

var words = new SortedDictionary<string, long>(StringComparer.Ordinal);
int lines = 0, accepted = 0, rejected = 0, duplicate = 0;
using (var reader = new StreamReader(sourcePath, new UTF8Encoding(false, true)))
{
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        lines++;
        string word = line.Trim().Normalize(NormalizationForm.FormC);
        if (!IsWord(word))
        {
            rejected++;
            continue;
        }

        // The official en_US list is alphabetized, not frequency-ranked.
        // Use one deterministic tier weight for all accepted entries.
        if (!words.TryAdd(word, 1))
        {
            duplicate++;
            continue;
        }
        accepted++;
    }
}

string outputPath = Path.Combine("artifacts", "dictionary", "esdb-en-US.tsv");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
{
    foreach ((string word, long tierWeight) in words)
        writer.WriteLine($"{word}\t{tierWeight.ToString(CultureInfo.InvariantCulture)}");
}

Console.WriteLine($"Source SHA-256: {actualHash}");
Console.WriteLine($"Lines: {lines}; unique accepted: {accepted}; duplicates: {duplicate}; rejected: {rejected}");
Console.WriteLine($"Output: {outputPath}");
return 0;

static bool IsWord(string word)
{
    if (word.Length == 0 || word.Contains(' ') || word.Contains('\t'))
        return false;

    // Exclude possessive forms and entries not made solely of letters or combining marks.
    if (word.EndsWith("'s", StringComparison.OrdinalIgnoreCase))
        return false;

    bool hasLetter = false;
    foreach (Rune rune in word.EnumerateRunes())
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter)
        {
            hasLetter = true;
            continue;
        }
        if (category == UnicodeCategory.NonSpacingMark ||
            category == UnicodeCategory.SpacingCombiningMark ||
            category == UnicodeCategory.EnclosingMark)
            continue;
        return false;
    }
    return hasLetter && !word.Any(char.IsUpper);
}
