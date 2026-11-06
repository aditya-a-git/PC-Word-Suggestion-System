namespace PCWord.Evaluation;

public sealed record EvaluationExample(string Context, string Target, int ContextOrder);

public static class EvaluationExamples
{
    /// <summary>Creates next-token examples from left context only. The first token in each segment has no eligible predecessor.</summary>
    public static IEnumerable<EvaluationExample> Read(IEnumerable<string> lines, Action? skippedBeginningOfSegment = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (string line in lines)
        {
            foreach (string[] segment in PCWord.Prediction.Inference.WordTokenizer.TokenizeSegments(line))
            {
                if (segment.Length == 0) continue;
                skippedBeginningOfSegment?.Invoke();
                for (int targetIndex = 1; targetIndex < segment.Length; targetIndex++)
                {
                    int start = Math.Max(0, targetIndex - 2);
                    yield return new EvaluationExample(
                        string.Join(' ', segment.Skip(start).Take(targetIndex - start)),
                        segment[targetIndex],
                        Math.Min(targetIndex, 3));
                }
            }
        }
    }
}

public sealed class TopKMetrics
{
    public long Examples { get; private set; }
    public long OutOfVocabulary { get; private set; }
    public long InVocabulary { get; private set; }
    public long Top1 { get; private set; }
    public long Top3 { get; private set; }
    public long Top5 { get; private set; }

    public void Add(string target, IReadOnlyList<string> ranked, bool inVocabulary)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ranked);
        Examples++;
        if (inVocabulary) InVocabulary++; else OutOfVocabulary++;
        if (ranked.Count > 0 && StringComparer.Ordinal.Equals(ranked[0], target)) Top1++;
        if (ranked.Take(3).Contains(target, StringComparer.Ordinal)) Top3++;
        if (ranked.Take(5).Contains(target, StringComparer.Ordinal)) Top5++;
    }

    public double Accuracy(long hits, bool inVocabularyOnly = false)
    {
        long denominator = inVocabularyOnly ? InVocabulary : Examples;
        return denominator == 0 ? 0 : hits * 100.0 / denominator;
    }
    public double Coverage => Examples == 0 ? 0 : InVocabulary * 100.0 / Examples;
    public double OovRate => Examples == 0 ? 0 : OutOfVocabulary * 100.0 / Examples;
}
