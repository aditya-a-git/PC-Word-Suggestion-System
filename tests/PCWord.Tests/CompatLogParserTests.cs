using PCWord.CompatReport;
using Xunit;

namespace PCWord.Tests;

public sealed class CompatLogParserTests
{
    [Fact]
    public void ParsesOnlyProbeRecordsAndAggregatesByProcess()
    {
        string[] lines =
        [
            "1\t00:00:00.1\t900\tdevice-name=secret-network",
            "2\t00:00:00.2\t101\t[PCWord.TSF] ",
            "3\t00:00:00.3\t101\tText service activated.",
            "4\t00:00:00.4\t102\tpassword=do-not-print",
            "5\t00:00:00.5\t101\t[PCWord.TSF] ",
            "6\t00:00:00.6\t101\tTSF document focus changed.",
            "7\t00:00:00.7\t101\t[PCWord.TSF] ",
            "8\t00:00:00.8\t101\tedit-event selection-collapsed=yes caret-rect=yes before-count=12 after-count=0 inputscope-stage=get-value inputscope-hr=0x80004005 classification=unknown decision=allowed-by-host-policy allowed=yes",
            "9\t00:00:00.9\t102\t[PCWord.TSF] ",
            "10\t00:00:01.0\t102\tedit-event selection-collapsed=no caret-rect=no before-count=-1 after-count=-1 inputscope-stage=not-read inputscope-hr=0x80004005 classification=unknown decision=paused allowed=no",
            "11\t00:00:01.1\t101\tunrelated output must be ignored"
        ];

        IReadOnlyList<CompatProcessSummary> result = CompatLogParser.Parse(lines);
        Assert.Equal(new[] { 101, 102 }, result.Select(item => item.ProcessId));
        CompatProcessSummary first = result[0];
        Assert.True(first.ActivationSeen);
        Assert.Equal(1, first.FocusChangeCount);
        Assert.Equal(1, first.EditEventCount);
        Assert.Equal(1, first.CollapsedYes);
        Assert.Equal(1, first.CaretRectYes);
        Assert.True(first.BeforeCountAvailable);
        Assert.True(first.AfterCountAvailable);
        Assert.Equal(1, first.InputScopeHResults["0x80004005"]);
        Assert.Equal(1, first.Classifications["unknown"]);
        Assert.Equal(1, first.Decisions["allowed-by-host-policy"]);

        CompatProcessSummary second = result[1];
        Assert.Equal(1, second.EditEventCount);
        Assert.Equal(0, second.CollapsedYes);
        Assert.False(second.BeforeCountAvailable);
        Assert.False(second.AfterCountAvailable);
    }

    [Fact]
    public void MatrixOutputDoesNotContainUntrustedLogTextAndEscapesArguments()
    {
        CompatProcessSummary summary = CompatLogParser.Parse(
        [
            "1\t0\t77\t[PCWord.TSF] ",
            "2\t0\t77\tedit-event selection-collapsed=unknown caret-rect=unknown before-count=-1 after-count=-1 inputscope-stage=not-read inputscope-hr=0x80004005 classification=unknown decision=paused allowed=no"
        ])[0];

        string row = CompatLogParser.FormatMatrixRows([summary], "App|Name", "v`1", "text|input", "2026-10-28");
        Assert.Contains("App\\|Name", row);
        Assert.Contains("v\\`1", row);
        Assert.DoesNotContain("password=do-not-print", row);
        Assert.Contains("Untested (probe does not insert text)", row);
    }

    [Theory]
    [InlineData("edit-event selection-collapsed=maybe caret-rect=yes before-count=-1 after-count=0 inputscope-stage=not-read inputscope-hr=0x80004005 classification=unknown decision=paused allowed=no")]
    [InlineData("edit-event selection-collapsed=yes caret-rect=yes before-count=99999999999 after-count=0 inputscope-stage=not-read inputscope-hr=0x80004005 classification=unknown decision=paused allowed=no")]
    public void IgnoresMalformedProbeEvent(string malformed)
    {
        string[] lines = ["1\t0\t77\t[PCWord.TSF] ", $"2\t0\t77\t{malformed}"];
        IReadOnlyList<CompatProcessSummary> result = CompatLogParser.Parse(lines);
        Assert.Single(result);
        Assert.Equal(0, result[0].EditEventCount);
    }
}
