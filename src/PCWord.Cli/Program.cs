
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Ranking;

var dictionaryPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(
        Path.Combine(
    AppContext.BaseDirectory,
    "../../../../PCWord.Prediction/Dictionary/sample.tsv"));

if (!File.Exists(dictionaryPath))
{
    Console.Error.WriteLine(
        $"Dictionary file not found: {dictionaryPath}");
    Console.Error.WriteLine(
        "Pass the dictionary path as the first argument.");
    return 1;
}

try
{
    var entries = DictionaryLoader.Load(dictionaryPath);
    var index = new PrefixIndex(entries);
    var ranker = new CandidateRanker(index);
    
    Console.WriteLine($"Loaded {entries.Count} words.");
    Console.WriteLine("Enter a prefix to get suggestions.");
    Console.WriteLine("Type :quit to exit.");

    while (true)
    {
        Console.Write("> ");
        var prefix = Console.ReadLine();

        if (prefix is null ||
            prefix.Equals(":quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        var suggestions = ranker.GetSuggestions(prefix);

        if (suggestions.Count == 0)
        {
            Console.WriteLine("(no suggestions)");
            continue;
        }

        foreach (var suggestion in suggestions)
        {
            var formatted = SuggestionFormatter.MatchPrefixCasing(
                prefix, suggestion.Word);

            Console.WriteLine(
                $"{formatted}  (frequency: {suggestion.Frequency})");
        }
    }

    return 0;
}
catch (Exception ex) when (
    ex is IOException or UnauthorizedAccessException
       or InvalidDataException)
{
    Console.Error.WriteLine($"Could not load dictionary: {ex.Message}");
    return 1;
}
