using System.Globalization;
using System.Text.RegularExpressions;

namespace PCWord.CompatReport;

public sealed record CompatProcessSummary(
    int ProcessId,
    bool ActivationSeen,
    int FocusChangeCount,
    int EditEventCount,
    int CollapsedYes,
    int CollapsedKnown,
    int CaretRectYes,
    int CaretRectKnown,
    IReadOnlyDictionary<string, int> InputScopeHResults,
    IReadOnlyDictionary<string, int> Classifications,
    IReadOnlyDictionary<string, int> Decisions,
    bool BeforeCountAvailable,
    bool AfterCountAvailable);

public static class CompatLogParser
{
    private const string Marker = "[PCWord.TSF] ";
    private static readonly Regex EditPattern = new(
        @"^edit-event selection-collapsed=(yes|no|unknown) caret-rect=(yes|no|unknown) before-count=(-1|[0-9]{1,9}) after-count=(-1|[0-9]{1,9}) inputscope-stage=(not-read|get-app-property|get-value|query-interface|get-input-scopes|complete) inputscope-hr=(0x[0-9A-Fa-f]{8}) classification=(safe|sensitive|unknown) decision=[a-z-]+ allowed=(yes|no)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<CompatProcessSummary> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var processes = new Dictionary<int, MutableSummary>();
        int? pendingPid = null;

        foreach (string rawLine in lines)
        {
            if (!TryGetRecord(rawLine, out int pid, out string message))
            {
                pendingPid = null;
                continue;
            }

            if (pendingPid is int expectedPid)
            {
                if (pid == expectedPid)
                    Accumulate(processes, pid, message);
                pendingPid = null;
                continue;
            }

            if (!message.StartsWith(Marker, StringComparison.Ordinal))
                continue;

            string payload = message[Marker.Length..];
            if (payload.Length == 0)
                pendingPid = pid;
            else
                Accumulate(processes, pid, payload);
        }

        return processes.OrderBy(pair => pair.Key)
            .Select(pair => pair.Value.ToImmutable(pair.Key))
            .ToArray();
    }

    public static string FormatMatrixRows(
        IReadOnlyList<CompatProcessSummary> summaries,
        string application,
        string version,
        string fieldCategory,
        string testDate)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        string app = Cell(application), appVersion = Cell(version), field = Cell(fieldCategory);
        string date = Cell(testDate);
        return string.Join(Environment.NewLine, summaries.Select(summary =>
            $"| {app} {appVersion} (PID {summary.ProcessId}) | {field} | " +
            $"bounded counts non-negative: before={YesNo(summary.BeforeCountAvailable)}, after={YesNo(summary.AfterCountAvailable)} | " +
            $"caret rect available {summary.CaretRectYes}/{summary.CaretRectKnown} ({Percent(summary.CaretRectYes, summary.CaretRectKnown)}) | " +
            "Untested (probe does not insert text) | Untested (no external popup) | " +
            $"Probe-only InputScope HRESULTs: {Distribution(summary.InputScopeHResults)}; classifications: {Distribution(summary.Classifications)}; decisions: {Distribution(summary.Decisions)} (not a sensitivity guarantee) | " +
            $"Observed (probe only; {date}; PID {summary.ProcessId}); activated={YesNo(summary.ActivationSeen)}, focus={summary.FocusChangeCount}, edit={summary.EditEventCount}, " +
            $"collapsed={summary.CollapsedYes}/{summary.CollapsedKnown} ({Percent(summary.CollapsedYes, summary.CollapsedKnown)}), caret rect={summary.CaretRectYes}/{summary.CaretRectKnown} ({Percent(summary.CaretRectYes, summary.CaretRectKnown)}) |"));
    }

    private static bool TryGetRecord(string line, out int pid, out string message)
    {
        pid = 0;
        message = string.Empty;
        if (string.IsNullOrEmpty(line)) return false;
        int first = line.IndexOf('\t');
        if (first < 0) return false;
        int second = line.IndexOf('\t', first + 1);
        if (second < 0) return false;
        int third = line.IndexOf('\t', second + 1);
        if (third < 0) return false;
        if (!int.TryParse(line.AsSpan(second + 1, third - second - 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out pid) || pid < 0)
            return false;
        message = line[(third + 1)..];
        return true;
    }

    private static void Accumulate(Dictionary<int, MutableSummary> all, int pid, string message)
    {
        MutableSummary summary = all.TryGetValue(pid, out MutableSummary? value)
            ? value : (all[pid] = new MutableSummary());

        if (message == "Text service activated.")
        {
            summary.ActivationSeen = true;
            return;
        }
        if (message == "TSF document focus changed.")
        {
            summary.FocusChangeCount++;
            return;
        }
        if (!message.StartsWith("edit-event ", StringComparison.Ordinal)) return;

        Match match = EditPattern.Match(message);
        if (!match.Success) return;
        summary.EditEventCount++;
        string collapsed = match.Groups[1].Value;
        if (collapsed != "unknown")
        {
            summary.CollapsedKnown++;
            if (collapsed == "yes") summary.CollapsedYes++;
        }
        string caret = match.Groups[2].Value;
        if (caret != "unknown")
        {
            summary.CaretRectKnown++;
            if (caret == "yes") summary.CaretRectYes++;
        }
        int before = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        int after = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        summary.BeforeCountAvailable |= before >= 0;
        summary.AfterCountAvailable |= after >= 0;
        string hresult = match.Groups[6].Value;
        Add(summary.InputScopeHResults, hresult);
        Add(summary.Classifications, match.Groups[7].Value);
        Add(summary.Decisions, ExtractDecision(message));
    }

    private static string ExtractDecision(string message)
    {
        foreach (string token in message.Split(' '))
            if (token.StartsWith("decision=", StringComparison.Ordinal))
                return token["decision=".Length..];
        return "unknown";
    }

    private static void Add(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;

    private static string Distribution(IReadOnlyDictionary<string, int> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value}"));

    private static string Percent(int numerator, int denominator) =>
        denominator == 0 ? "n/a" : $"{100.0 * numerator / denominator:F1}%";

    private static string YesNo(bool value) => value ? "yes" : "no";
    private static string Cell(string value) =>
        string.Concat((value ?? string.Empty).Where(ch => !char.IsControl(ch)))
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Trim();

    private sealed class MutableSummary
    {
        public bool ActivationSeen;
        public int FocusChangeCount, EditEventCount, CollapsedYes, CollapsedKnown, CaretRectYes, CaretRectKnown;
        public bool BeforeCountAvailable, AfterCountAvailable;
        public Dictionary<string, int> InputScopeHResults { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Classifications { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Decisions { get; } = new(StringComparer.Ordinal);

        public CompatProcessSummary ToImmutable(int pid) => new(pid, ActivationSeen, FocusChangeCount,
            EditEventCount, CollapsedYes, CollapsedKnown, CaretRectYes, CaretRectKnown,
            new Dictionary<string, int>(InputScopeHResults), new Dictionary<string, int>(Classifications),
            new Dictionary<string, int>(Decisions), BeforeCountAvailable, AfterCountAvailable);
    }
}
