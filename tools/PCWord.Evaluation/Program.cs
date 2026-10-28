using System.Diagnostics;
using PCWord.Prediction.Inference;
using PCWord.Prediction.Context;
using PCWord.Core.Input;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: PCWord.Evaluation <train.txt> <validation.txt> <test.txt> [model-output.json]");
    return 2;
}

try
{
    if (new[] { args[0], args[1], args[2] }.Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
        throw new ArgumentException("Train, validation, and test inputs must be distinct files.");
    long allocatedBefore = GC.GetTotalMemory(true);
    long workingBefore = Environment.WorkingSet;
    TimeSpan cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
    var build = Stopwatch.StartNew();
    NGramModel model = NGramModel.Train(File.ReadLines(args[0]));
    build.Stop();

    (int order, double alpha, Metrics _) = new[] { 1, 2, 3 }.SelectMany(order => new[] { .2, .4, .6, .8 }
        .Select(alpha => (order, alpha, Evaluate(model, args[1], alpha, order))))
        .OrderByDescending(x => x.Item3.Top3).ThenByDescending(x => x.Item3.Top1)
        .ThenBy(x => x.order).ThenBy(x => x.alpha).First();

    model.ConfigurePrediction(alpha, order);
    string? modelOutput = args.Length > 3 ? args[3] : null;
    var testTimer = Stopwatch.StartNew();
    Metrics test = Evaluate(model, args[2], alpha, order);
    testTimer.Stop();
    var lookupTimes = new double[Math.Min(2048, Math.Max(1, test.Examples))];
    var requestTimes = new double[lookupTimes.Length];
    var contextManager = new PredictionContextManager();
    string[] testLines = File.ReadLines(args[2]).ToArray();
    int sample = 0;
    foreach (string line in testLines)
    {
        foreach (string[] words in WordTokenizer.TokenizeSegments(line))
        for (int i = 1; i < words.Length && sample < lookupTimes.Length; i++)
        {
            var timer = Stopwatch.StartNew(); _ = model.Predict(string.Join(' ', words.Take(i)), 3, alpha, order);
            timer.Stop(); lookupTimes[sample++] = timer.Elapsed.TotalMilliseconds;
            if (sample <= requestTimes.Length)
            {
                string before = string.Join(' ', words.Take(i)) + " ";
                var requestTimer = Stopwatch.StartNew();
                var snapshot = new InputContextSnapshot(sample, "evaluation-field", before, string.Empty,
                    0, true, InputFieldSensitivity.KnownSafe, false, true);
                if (contextManager.TryUpdate(snapshot) && contextManager.Current.CanPredictNextWord)
                    _ = model.Predict(contextManager.Current.PrecedingText, 3, alpha, order);
                requestTimer.Stop(); requestTimes[sample - 1] = requestTimer.Elapsed.TotalMilliseconds;
            }
        }
    }
    double? loadMilliseconds = null;
    long? modelBytes = null;
    if (modelOutput is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(modelOutput))!);
        model.Save(modelOutput);
        var loadTimer = Stopwatch.StartNew(); _ = NGramModel.Load(modelOutput); loadTimer.Stop();
        loadMilliseconds = loadTimer.Elapsed.TotalMilliseconds;
        modelBytes = new FileInfo(modelOutput).Length;
    }
    Array.Sort(lookupTimes, 0, sample);
    double median = sample == 0 ? double.NaN : lookupTimes[(sample - 1) / 2];
    double p95 = sample == 0 ? double.NaN : lookupTimes[Math.Max(0, (int)Math.Ceiling(sample * .95) - 1)];
    Array.Sort(requestTimes, 0, sample);
    double requestMedian = sample == 0 ? double.NaN : requestTimes[(sample - 1) / 2];
    double requestP95 = sample == 0 ? double.NaN : requestTimes[Math.Max(0, (int)Math.Ceiling(sample * .95) - 1)];
    GC.Collect(); GC.WaitForPendingFinalizers();
    Console.WriteLine("PCWord local n-gram evaluation (aggregate metrics only)");
    Console.WriteLine($"Tokenizer: Unicode letters/digits/marks; invariant lowercase; punctuation separators; sentence/line reset");
    Console.WriteLine($"Selected on validation: order={order}, backoff={alpha:F1}; vocab={model.VocabularySize}; contexts={model.ContextCount}; model bytes={(modelBytes?.ToString() ?? "unavailable")}");
    Console.WriteLine($"Test examples={test.Examples}; OOV={test.Oov} ({Pct(test.Oov, test.Examples):F2}%); target coverage={Pct(test.Covered, test.Examples):F2}%; top1={Pct(test.Top1, test.Examples):F2}%; top3={Pct(test.Top3, test.Examples):F2}%");
    Console.WriteLine($"Unigram baseline: top1={Pct(test.UniTop1, test.Examples):F2}%; top3={Pct(test.UniTop3, test.Examples):F2}%");
    Console.WriteLine($"Model build ms={build.Elapsed.TotalMilliseconds:F2}; model load ms={(loadMilliseconds?.ToString("F2") ?? "unavailable")}; lookup samples={sample}; median ms={median:F5}; p95 ms={p95:F5}");
    Console.WriteLine($"Synthetic controlled-context request median ms={requestMedian:F5}; p95 ms={requestP95:F5}; complete test evaluation ms={testTimer.Elapsed.TotalMilliseconds:F2}");
    Console.WriteLine($"CPU ms={(Process.GetCurrentProcess().TotalProcessorTime-cpuBefore).TotalMilliseconds:F2}; managed delta bytes={GC.GetTotalMemory(false)-allocatedBefore}; working-set delta bytes={Environment.WorkingSet-workingBefore}");
    Console.WriteLine($"Model output: {(modelOutput is null ? "not requested" : Path.GetFullPath(modelOutput))}");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
{
    Console.Error.WriteLine($"Evaluation failed ({ex.GetType().Name}); input content is not printed."); return 1;
}

static Metrics Evaluate(NGramModel model, string path, double alpha, int order)
{
    var result = new Metrics();
    foreach (string line in File.ReadLines(path))
    {
        foreach (string[] words in WordTokenizer.TokenizeSegments(line))
        for (int i = 1; i < words.Length; i++)
        {
            result.Examples++;
            bool knownTarget = model.ContainsWord(words[i]);
            if (!knownTarget) result.Oov++;
            var context = model.Predict(string.Join(' ', words.Take(i)), 3, alpha, order);
            if (knownTarget && context.Count > 0) result.Covered++;
            if (context.Count > 0 && context[0] == words[i]) result.Top1++;
            if (context.Contains(words[i], StringComparer.Ordinal)) result.Top3++;
            var uni = model.PredictUnigram(3);
            if (uni.Count > 0 && uni[0] == words[i]) result.UniTop1++;
            if (uni.Contains(words[i], StringComparer.Ordinal)) result.UniTop3++;
        }
    }
    return result;
}
static double Pct(long value, long total) => total == 0 ? 0 : value * 100.0 / total;
sealed class Metrics { public int Examples, Oov, Covered, Top1, Top3, UniTop1, UniTop3; }
