using System.Diagnostics;

namespace PCWord.Prediction.Diagnostics;

/// <summary>Names of aggregate-only timings; no text or user content is accepted.</summary>
public enum CompletionMetricStage
{
    DictionaryStartup = 0,
    ContextAcquisition = 1,
    DictionaryLookupAndRanking = 2,
    ResultDelivery = 3,
    UiUpdate = 4,
    EndToEndPrediction = 5,
    LookupOnly = 6,
    NGramLookup = 7
}

public sealed record CompletionMetricSummary(
    CompletionMetricStage Stage,
    long SampleCount,
    int WindowSampleCount,
    double? MedianMilliseconds,
    double? P95Milliseconds);

/// <summary>
/// Keeps only fixed-size timing rings and lifetime counts in memory. It accepts numeric
/// durations and stage enums only, so input text and candidates cannot enter this store.
/// </summary>
public sealed class CompletionMetrics
{
    public const int MaximumSamplesPerStage = 2048;
    private readonly object _gate = new();
    private readonly StageSamples[] _stages = Enum.GetValues<CompletionMetricStage>()
        .Select(_ => new StageSamples())
        .ToArray();

    public void Record(CompletionMetricStage stage, TimeSpan elapsed)
    {
        int index = (int)stage;
        if ((uint)index >= (uint)_stages.Length)
            throw new ArgumentOutOfRangeException(nameof(stage));

        double milliseconds = Math.Clamp(elapsed.TotalMilliseconds, 0, 60_000);
        lock (_gate)
            _stages[index].Add(milliseconds);
    }

    public IReadOnlyList<CompletionMetricSummary> GetSnapshot()
    {
        lock (_gate)
        {
            var summaries = new CompletionMetricSummary[_stages.Length];
            for (int index = 0; index < _stages.Length; index++)
                summaries[index] = _stages[index].Summarize((CompletionMetricStage)index);
            return summaries;
        }
    }

    public void Reset()
    {
        lock (_gate)
            foreach (StageSamples samples in _stages)
                samples.Reset();
    }

    public static TimeSpan ElapsedSince(long timestamp) => Stopwatch.GetElapsedTime(timestamp);

    private sealed class StageSamples
    {
        private readonly double[] _samples = new double[MaximumSamplesPerStage];
        private int _next;
        private int _count;
        private long _totalCount;

        public void Add(double milliseconds)
        {
            _samples[_next] = milliseconds;
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length)
                _count++;
            if (_totalCount < long.MaxValue)
                _totalCount++;
        }

        public CompletionMetricSummary Summarize(CompletionMetricStage stage)
        {
            if (_count == 0)
                return new CompletionMetricSummary(stage, _totalCount, 0, null, null);

            double[] ordered = new double[_count];
            Array.Copy(_samples, ordered, _count);
            Array.Sort(ordered);
            double median = _count % 2 == 1
                ? ordered[_count / 2]
                : (ordered[(_count / 2) - 1] + ordered[_count / 2]) / 2;
            int p95Index = Math.Max(0, (int)Math.Ceiling(_count * 0.95) - 1);
            return new CompletionMetricSummary(stage, _totalCount, _count, median, ordered[p95Index]);
        }

        public void Reset()
        {
            Array.Clear(_samples);
            _next = 0;
            _count = 0;
            _totalCount = 0;
        }
    }
}
