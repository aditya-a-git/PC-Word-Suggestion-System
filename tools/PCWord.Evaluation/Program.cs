using System.Diagnostics;
using System.Text.Json;
using PCWord.Core.Input;
using PCWord.Evaluation;
using PCWord.Prediction.Context;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Inference;

if (args.Length < 3 || args.Length > 5)
{
    Console.Error.WriteLine("Usage: PCWord.Evaluation <train.txt> <validation.txt> <test.txt> [model-output.json] [results.json]");
    return 2;
}

string? temporaryModel = null;
try
{
    string[] paths = args.Take(3).Select(Path.GetFullPath).ToArray();
    if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
        throw new ArgumentException("Train, validation, and test inputs must be distinct files.");
    foreach (string path in paths)
        if (!File.Exists(path)) throw new FileNotFoundException("A required split file is missing.");

    long managedBefore = GC.GetTotalMemory(true);
    long workingBefore = Environment.WorkingSet;
    TimeSpan cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

    var trainTimer = Stopwatch.StartNew();
    NGramModel model = NGramModel.Train(File.ReadLines(paths[0]));
    trainTimer.Stop();

    var validationMetrics = new Dictionary<string, SplitResult>(StringComparer.Ordinal);
    (int order, double backoff) = (new[] { 1, 2, 3 }
        .SelectMany(candidateOrder => new[] { .2, .4, .6, .8 }.Select(candidateBackoff =>
        {
            SplitResult score = Evaluate(model, paths[1], candidateBackoff, candidateOrder);
            validationMetrics[$"order-{candidateOrder}-backoff-{candidateBackoff:F1}"] = score;
            return (Order: candidateOrder, Backoff: candidateBackoff, Score: score);
        }))
        .OrderByDescending(x => x.Score.NGram.Top3)
        .ThenByDescending(x => x.Score.NGram.Top1)
        .ThenBy(x => x.Order)
        .ThenBy(x => x.Backoff)
        .Select(x => (x.Order, x.Backoff)).First());

    model.ConfigurePrediction(backoff, order);
    string modelPath = args.Length >= 4
        ? Path.GetFullPath(args[3])
        : temporaryModel = Path.Combine(Path.GetTempPath(), $"pcword-evaluation-{Guid.NewGuid():N}.json");
    Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
    model.Save(modelPath);
    var loadTimer = Stopwatch.StartNew();
    NGramModel loadedModel = NGramModel.Load(modelPath);
    loadTimer.Stop();
    long modelBytes = new FileInfo(modelPath).Length;
    string bundledDictionaryPath = Path.Combine(AppContext.BaseDirectory, "Dictionary", "sample.tsv");
    int? bundledDictionaryEntries = File.Exists(bundledDictionaryPath) ? DictionaryLoader.Load(bundledDictionaryPath).Count : null;

    var testTimer = Stopwatch.StartNew();
    SplitResult test = Evaluate(loadedModel, paths[2], backoff, order);
    testTimer.Stop();

    string resultsPath = args.Length >= 5
        ? Path.GetFullPath(args[4])
        : Path.GetFullPath(Path.Combine("artifacts", "stage-5", "evaluation-results.json"));
    Directory.CreateDirectory(Path.GetDirectoryName(resultsPath)!);

    var examples = EvaluationExamples.Read(File.ReadLines(paths[2])).Where(e => e.Context.Length + 1 <= InputContextSnapshot.MaximumTextBeforeCaret).Take(25_000).ToArray();
    var contextManager = new PredictionContextManager();
    long version = 0;
    // Warm-up uses the same context manager and model API as the coordinator path.
    foreach (EvaluationExample example in examples.Take(Math.Min(256, examples.Length)))
        _ = PredictAtBoundary(loadedModel, contextManager, example, ++version, order, backoff);
    var latencies = new double[examples.Length];
    for (int i = 0; i < examples.Length; i++)
    {
        var timer = Stopwatch.StartNew();
        _ = PredictAtBoundary(loadedModel, contextManager, examples[i], ++version, order, backoff);
        timer.Stop();
        latencies[i] = timer.Elapsed.TotalMilliseconds;
    }
    Array.Sort(latencies);
    double? median = Percentile(latencies, .50);
    double? p95 = Percentile(latencies, .95);

    GC.Collect(); GC.WaitForPendingFinalizers();
    var report = new
    {
        schemaVersion = 1,
        dataset = new
        {
            name = "Salesforce WikiText-2 raw",
            config = "wikitext-2-raw-v1",
            revision = "b08601e04326c79dfdd32d625aee71d232d685c3",
            license = "Dataset card reports CC BY-SA 3.0 and GFDL metadata and also CC BY-SA 4.0 text; distribution rights remain unresolved.",
            preprocessing = "UTF-8 Parquet text rows exported one row per line; Unicode letters/digits/combining marks lowercased invariantly; all punctuation separates tokens; period/exclamation/question/newline reset history; line/row boundaries reset history.",
            checksums = new
            {
                train = "e83889baabc497075506f91975be5fac0d45c5290b6b20582c8cd1e853d0c9f7",
                validation = "204929b7ff9d6184953f867dedb860e40aa69c078fc1e54b3baaa8fb28511c4c",
                test = "5f1bea067869d04849c0f975a2b29c4ff47d867f484f5010ea5e861eab246d91"
            },
            localFiles = paths
        },
        model = new { algorithm = "bounded word n-gram backoff", maximumOrder = order, backoff, vocabularySize = loadedModel.VocabularySize, contextCount = loadedModel.ContextCount, modelBytes },
        completionDictionaryFixture = new { entries = bundledDictionaryEntries, usedForQualityMetrics = false, note = "The bundled sample dictionary is a synthetic demo fixture and is not used by next-word evaluation." },
        training = new { elapsedMilliseconds = trainTimer.Elapsed.TotalMilliseconds, loadingMilliseconds = loadTimer.Elapsed.TotalMilliseconds },
        validation = new { selectedBy = "top-3 accuracy, then top-1; ties prefer smaller order then smaller backoff", selectedConfiguration = new { order, backoff }, metrics = ToReport(validationMetrics[$"order-{order}-backoff-{backoff:F1}"]) },
        test = ToReport(test),
        benchmark = new
        {
            warmupExamples = Math.Min(256, examples.Length),
            measuredPredictionExamples = latencies.Length,
            path = "PredictionContextManager eligibility plus NGramModel.Predict; excludes file I/O and WinForms painting",
            medianMilliseconds = median,
            p95Milliseconds = p95,
            cpuMilliseconds = (Process.GetCurrentProcess().TotalProcessorTime - cpuBefore).TotalMilliseconds,
            managedMemoryDeltaBytes = GC.GetTotalMemory(false) - managedBefore,
            workingSetDeltaBytes = Environment.WorkingSet - workingBefore,
            fullTestEvaluationMilliseconds = testTimer.Elapsed.TotalMilliseconds
        }
    };
    File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Evaluation complete. Test examples={test.NGram.Examples}; OOV={test.NGram.OutOfVocabulary} ({test.NGram.OovRate:F2}%); coverage={test.NGram.Coverage:F2}%.");
    Console.WriteLine($"N-gram top-1/top-3/top-5={test.NGram.Accuracy(test.NGram.Top1):F2}%/{test.NGram.Accuracy(test.NGram.Top3):F2}%/{test.NGram.Accuracy(test.NGram.Top5):F2}% (all eligible).");
    Console.WriteLine($"Unigram top-1/top-3/top-5={test.Unigram.Accuracy(test.Unigram.Top1):F2}%/{test.Unigram.Accuracy(test.Unigram.Top3):F2}%/{test.Unigram.Accuracy(test.Unigram.Top5):F2}%.");
    Console.WriteLine($"Prediction latency samples={latencies.Length}; median={median:F5} ms; p95={p95:F5} ms; training={trainTimer.Elapsed.TotalMilliseconds:F2} ms; loading={loadTimer.Elapsed.TotalMilliseconds:F2} ms; model={modelBytes} bytes.");
    Console.WriteLine($"Skipped first tokens without left context={test.SkippedBeginningTokens}; results={resultsPath}");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException)
{
    Console.Error.WriteLine($"Evaluation failed ({ex.GetType().Name}); split contents are not printed.");
    return 1;
}
finally
{
    if (temporaryModel is not null && File.Exists(temporaryModel)) File.Delete(temporaryModel);
}

static IReadOnlyList<string> PredictAtBoundary(NGramModel model, PredictionContextManager manager,
    EvaluationExample example, long version, int order, double backoff)
{
    var snapshot = new InputContextSnapshot(version, "evaluation-field", example.Context + " ", string.Empty,
        0, true, InputFieldSensitivity.KnownSafe, false, true);
    if (!manager.TryUpdate(snapshot) || !manager.Current.CanPredictNextWord) return Array.Empty<string>();
    return model.Predict(manager.Current.PrecedingText, 5, backoff, order);
}

static SplitResult Evaluate(NGramModel model, string path, double backoff, int order)
{
    var result = new SplitResult();
    foreach (EvaluationExample example in EvaluationExamples.Read(File.ReadLines(path), () => result.SkippedBeginningTokens++))
    {
        if (example.Context.Length + 1 > InputContextSnapshot.MaximumTextBeforeCaret)
        {
            result.SkippedOverlongContexts++;
            continue;
        }
        // The shared candidate API receives only the left context; target is consulted only for scoring.
        var snapshot = new InputContextSnapshot(result.NGram.Examples + 1, "evaluation-field", example.Context + " ",
            string.Empty, 0, true, InputFieldSensitivity.KnownSafe, false, true);
        var manager = new PredictionContextManager();
        IReadOnlyList<string> candidates = manager.TryUpdate(snapshot) && manager.Current.CanPredictNextWord
            ? model.Predict(manager.Current.PrecedingText, 5, backoff, order)
            : Array.Empty<string>();
        IReadOnlyList<string> unigram = model.PredictUnigram(5);
        bool known = model.ContainsWord(example.Target);
        result.NGram.Add(example.Target, candidates, known);
        result.Unigram.Add(example.Target, unigram, known);
        result.ByContextOrder.GetOrAdd(example.ContextOrder).Add(example.Target, candidates, known);
        result.UnigramByContextOrder.GetOrAdd(example.ContextOrder).Add(example.Target, unigram, known);
    }
    return result;
}

static object ToReport(SplitResult value) => new
{
    skippedBeginningTokensWithoutLeftContext = value.SkippedBeginningTokens,
    skippedOverlongContexts = value.SkippedOverlongContexts,
    ngram = MetricReport(value.NGram),
    unigramBaseline = MetricReport(value.Unigram),
    byAvailableContextOrder = value.ByContextOrder.OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => MetricReport(x.Value)),
    unigramByAvailableContextOrder = value.UnigramByContextOrder.OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => MetricReport(x.Value))
};
static object MetricReport(TopKMetrics m) => new
{
    eligibleExamples = m.Examples, outOfVocabularyTargets = m.OutOfVocabulary,
    vocabularyCoveragePercent = m.Coverage, oovRatePercent = m.OovRate,
    top1AccuracyPercentAllEligible = m.Accuracy(m.Top1), top3AccuracyPercentAllEligible = m.Accuracy(m.Top3), top5AccuracyPercentAllEligible = m.Accuracy(m.Top5),
    top1AccuracyPercentInVocabulary = m.Accuracy(m.Top1, true), top3AccuracyPercentInVocabulary = m.Accuracy(m.Top3, true), top5AccuracyPercentInVocabulary = m.Accuracy(m.Top5, true)
};
static double? Percentile(double[] sorted, double p) => sorted.Length == 0 ? null : sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * p) - 1)];

sealed class SplitResult
{
    public TopKMetrics NGram { get; } = new();
    public TopKMetrics Unigram { get; } = new();
    public long SkippedBeginningTokens { get; set; }
    public long SkippedOverlongContexts { get; set; }
    public Dictionary<int, TopKMetrics> ByContextOrder { get; } = new();
    public Dictionary<int, TopKMetrics> UnigramByContextOrder { get; } = new();
}
static class MetricDictionaryExtensions
{
    public static TopKMetrics GetOrAdd(this Dictionary<int, TopKMetrics> source, int key)
    {
        if (!source.TryGetValue(key, out TopKMetrics? value)) source[key] = value = new TopKMetrics();
        return value;
    }
}
