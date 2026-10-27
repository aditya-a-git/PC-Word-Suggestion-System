using System.Diagnostics;
using PCWord.Core.Input;
using PCWord.Prediction.Context;
using PCWord.Prediction.Diagnostics;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Ranking;

const int warmupIterations = 100;
int iterations = ParseIterations(args);
if (iterations == 0)
    return 2;
string dictionaryPath = Path.Combine(AppContext.BaseDirectory, "Dictionary", "sample.tsv");
var process = Process.GetCurrentProcess();
long startupStarted = Stopwatch.GetTimestamp();
IReadOnlyList<DictionaryEntry> entries = DictionaryLoader.Load(dictionaryPath);
var ranker = new CandidateRanker(new PrefixIndex(entries));
TimeSpan dictionaryStartup = Stopwatch.GetElapsedTime(startupStarted);
var metrics = new CompletionMetrics();
var adapter = new BenchmarkInputAdapter();
using var coordinator = new DictionaryCompletionCoordinator(adapter, ranker, metrics: metrics);
coordinator.SuggestionsChanged += (_, update) =>
{
    long uiStarted = Stopwatch.GetTimestamp();
    // Simulate the small consumer work (candidate count + current result handoff).
    _ = update.Candidates.Count;
    metrics.Record(CompletionMetricStage.UiUpdate, Stopwatch.GetElapsedTime(uiStarted));
};

string[] prefixes = ["pro", "prog", "pre", "pred", "prod", "prx"];
for (int index = 0; index < warmupIterations; index++)
{
    IReadOnlyList<DictionaryEntry> warmup = ranker.GetSuggestions(prefixes[index % prefixes.Length]);
    _ = warmup.Count;
    adapter.Publish(CreateSyntheticSnapshot(index + 1, prefixes[index % prefixes.Length], metrics));
}
metrics.Reset();
metrics.Record(CompletionMetricStage.DictionaryStartup, dictionaryStartup);

for (int index = 0; index < iterations; index++)
{
    long lookupStarted = Stopwatch.GetTimestamp();
    IReadOnlyList<DictionaryEntry> lookup = ranker.GetSuggestions(prefixes[index % prefixes.Length]);
    _ = lookup.Count;
    metrics.Record(CompletionMetricStage.LookupOnly, Stopwatch.GetElapsedTime(lookupStarted));
}

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long workingSetBefore = process.WorkingSet64;
long managedHeapBefore = GC.GetTotalMemory(forceFullCollection: true);
TimeSpan cpuBefore = process.TotalProcessorTime;
int version = warmupIterations;
for (int index = 0; index < iterations; index++)
{
    string prefix = prefixes[index % prefixes.Length];
    adapter.Publish(CreateSyntheticSnapshot(++version, prefix, metrics));
}
process.Refresh();
TimeSpan cpuElapsed = process.TotalProcessorTime - cpuBefore;
long workingSetAfter = process.WorkingSet64;
long managedHeapAfter = GC.GetTotalMemory(forceFullCollection: true);

Console.WriteLine("PCWord Stage 4 completion benchmark (synthetic prefixes; no input text or candidates printed)");
Console.WriteLine($"Dictionary: bundled sample.tsv, {entries.Count} entries");
Console.WriteLine($"Warmup iterations: {warmupIterations}; measured iterations per phase: {iterations}");
Console.WriteLine("End-to-end includes synthetic bounded snapshot construction, context management, dictionary ranking, event delivery, and a synthetic result consumer; it excludes RichTextBox/Win32 calls and actual painting.");
Console.WriteLine($"Dictionary startup (load + index): {dictionaryStartup.TotalMilliseconds:F3} ms (one sample)");
foreach (CompletionMetricStage stage in new[]
{
    CompletionMetricStage.LookupOnly,
    CompletionMetricStage.ContextAcquisition,
    CompletionMetricStage.DictionaryLookupAndRanking,
    CompletionMetricStage.ResultDelivery,
    CompletionMetricStage.UiUpdate,
    CompletionMetricStage.EndToEndPrediction
})
{
    CompletionMetricSummary summary = metrics.GetSnapshot().Single(value => value.Stage == stage);
    PrintSummary(summary);
}
Console.WriteLine($"Process CPU time during end-to-end phase: {cpuElapsed.TotalMilliseconds:F3} ms (aggregate; includes runtime/GC overhead)");
Console.WriteLine($"Working set before/after end-to-end phase: {workingSetBefore} / {workingSetAfter} bytes (delta {workingSetAfter - workingSetBefore:+#;-#;0} bytes)");
Console.WriteLine($"Managed heap before/after end-to-end phase: {managedHeapBefore} / {managedHeapAfter} bytes (delta {managedHeapAfter - managedHeapBefore:+#;-#;0} bytes)");
return 0;

static void PrintSummary(CompletionMetricSummary summary)
{
    string median = summary.MedianMilliseconds?.ToString("F4") ?? "unavailable";
    string p95 = summary.P95Milliseconds?.ToString("F4") ?? "unavailable";
    Console.WriteLine($"{summary.Stage}: n={summary.SampleCount}, percentile window={summary.WindowSampleCount}, median={median} ms, p95={p95} ms");
}

static int ParseIterations(string[] arguments)
{
    if (arguments.Length == 0)
        return 1000;
    if (arguments.Length == 2 && arguments[0] == "--iterations" &&
        int.TryParse(arguments[1], out int value) && value is >= 100 and <= 100_000)
        return value;
    Console.Error.WriteLine("Usage: PCWord.Benchmark [--iterations 100..100000]");
    Environment.ExitCode = 2;
    return 0;
}

static InputContextSnapshot CreateSyntheticSnapshot(int version, string prefix, CompletionMetrics metrics)
{
    long startedAt = Stopwatch.GetTimestamp();
    string preceding = "synthetic " + prefix;
    var snapshot = new InputContextSnapshot(version, "benchmark-field", preceding, "", 0,
        HasFocus: true, InputFieldSensitivity.KnownSafe,
        CaptureStartedTimestamp: startedAt);
    metrics.Record(CompletionMetricStage.ContextAcquisition, Stopwatch.GetElapsedTime(startedAt));
    return snapshot;
}

sealed class BenchmarkInputAdapter : IInputAdapter
{
    private InputContextSnapshot? _snapshot;
    private long _candidateVersion = -1;
    private string? _candidateField;

    public event EventHandler<InputContextChangedEventArgs>? ContextChanged;

    public void Publish(InputContextSnapshot snapshot)
    {
        _snapshot = snapshot;
        ContextChanged?.Invoke(this, new InputContextChangedEventArgs(snapshot));
    }

    public void SetEnabled(bool enabled) { }

    public void SetCandidateSet(long contextVersion, string? fieldId, IReadOnlyList<string> candidates)
    {
        if (_snapshot?.Version != contextVersion || !StringComparer.Ordinal.Equals(_snapshot.FieldId, fieldId))
        {
            _candidateVersion = -1;
            _candidateField = null;
            return;
        }
        _candidateVersion = contextVersion;
        _candidateField = fieldId;
        _ = candidates.Count;
    }

    public ValueTask<TextEditResult> ApplyEditAsync(TextEditRequest request, CancellationToken cancellationToken = default)
    {
        bool matches = _snapshot?.Version == request.ContextVersion &&
            _candidateVersion == request.ContextVersion &&
            StringComparer.Ordinal.Equals(_candidateField, request.FieldId);
        return ValueTask.FromResult(new TextEditResult(matches ? TextEditStatus.Unsupported : TextEditStatus.StaleContext));
    }
}
