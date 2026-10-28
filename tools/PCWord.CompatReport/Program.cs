using PCWord.CompatReport;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: PCWord.CompatReport <debugview-log> <app-name> <app-version> <field-category>");
    return 2;
}

try
{
    using var reader = new StreamReader(args[0]);
    IReadOnlyList<CompatProcessSummary> summaries = CompatLogParser.Parse(ReadLines(reader));
    Console.WriteLine("| Application/version | Field type | Context access | Caret location | Text insertion | Popup behavior | Sensitive-field behavior | Regression status |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|");
    Console.WriteLine(CompatLogParser.FormatMatrixRows(summaries, args[1], args[2], args[3],
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")));
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("Unable to process the compatibility log safely.");
    return 1;
}

static IEnumerable<string> ReadLines(TextReader reader)
{
    string? line;
    while ((line = reader.ReadLine()) is not null)
        yield return line;
}
