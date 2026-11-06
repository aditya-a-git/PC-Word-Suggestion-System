using PCWord.Evaluation;
using Xunit;

namespace PCWord.Tests;

public sealed class EvaluationScoringTests
{
    [Fact]
    public void TopKMetricsUseAllExamplesAndExposeVocabularyConditionalAccuracy()
    {
        var metrics = new TopKMetrics();
        metrics.Add("a", new[] { "a", "b", "c", "d", "e" }, true);
        metrics.Add("z", new[] { "x", "z", "y" }, false);
        metrics.Add("q", new[] { "x", "y", "z", "q", "r" }, true);

        Assert.Equal(3, metrics.Examples);
        Assert.Equal(1, metrics.OutOfVocabulary);
        Assert.Equal(2, metrics.InVocabulary);
        Assert.Equal(33.333, metrics.Accuracy(metrics.Top1), 2);
        Assert.Equal(50, metrics.Accuracy(metrics.Top1, inVocabularyOnly: true));
        Assert.Equal(66.666, metrics.Accuracy(metrics.Top3), 2);
        Assert.Equal(100, metrics.Accuracy(metrics.Top5), 2);
        Assert.Equal(66.666, metrics.Coverage, 2);
        Assert.Equal(33.333, metrics.OovRate, 2);
    }

    [Fact]
    public void ExamplesUseOnlyPastTokensAndResetAtPunctuationAndLineBoundaries()
    {
        var examples = EvaluationExamples.Read(new[] { "alpha beta gamma. delta epsilon", "zeta eta" }).ToArray();

        Assert.Equal(new EvaluationExample("alpha", "beta", 1), examples[0]);
        Assert.Equal(new EvaluationExample("alpha beta", "gamma", 2), examples[1]);
        Assert.Equal(new EvaluationExample("delta", "epsilon", 1), examples[2]);
        Assert.Equal(new EvaluationExample("zeta", "eta", 1), examples[3]);
        Assert.DoesNotContain(examples, example => example.Context.Contains(example.Target, StringComparison.Ordinal));
    }

    [Fact]
    public void SegmentFirstTokensAreCountedAsSkipped()
    {
        int skipped = 0;
        var examples = EvaluationExamples.Read(new[] { "one two. three four five" }, () => skipped++).ToArray();
        Assert.Equal(2, skipped);
        Assert.Equal(3, examples.Length);
    }
}
