using System.Reflection;
using PCWord.Prediction.Diagnostics;
using Xunit;

namespace PCWord.Tests;

public sealed class CompletionMetricsTests
{
    [Fact]
    public void AggregatesPercentilesAndBoundsRetainedSamples()
    {
        var metrics = new CompletionMetrics();
        for (int index = 1; index <= CompletionMetrics.MaximumSamplesPerStage + 100; index++)
            metrics.Record(CompletionMetricStage.LookupOnly, TimeSpan.FromMilliseconds(index));

        CompletionMetricSummary summary = Assert.Single(metrics.GetSnapshot(), item => item.Stage == CompletionMetricStage.LookupOnly);
        Assert.Equal(CompletionMetrics.MaximumSamplesPerStage + 100, summary.SampleCount);
        Assert.Equal(CompletionMetrics.MaximumSamplesPerStage, summary.WindowSampleCount);
        Assert.Equal(CompletionMetrics.MaximumSamplesPerStage / 2d + 100.5, summary.MedianMilliseconds);
        Assert.True(summary.P95Milliseconds > summary.MedianMilliseconds);
    }

    [Fact]
    public void InstrumentationSurfaceAcceptsAndExposesNoText()
    {
        Type metricsType = typeof(CompletionMetrics);
        Assert.DoesNotContain(metricsType.GetMethods(BindingFlags.Instance | BindingFlags.Public), method =>
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)));
        Assert.DoesNotContain(metricsType.GetProperties(BindingFlags.Instance | BindingFlags.Public), property =>
            property.PropertyType == typeof(string));
        Assert.DoesNotContain(metricsType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic), field =>
            field.FieldType == typeof(string));

        var metrics = new CompletionMetrics();
        metrics.Record(CompletionMetricStage.ContextAcquisition, TimeSpan.FromMilliseconds(0.25));
        Assert.All(metrics.GetSnapshot(), summary =>
            Assert.DoesNotContain(typeof(string), summary.GetType().GetProperties()
                .Select(property => property.PropertyType)));
    }
}
