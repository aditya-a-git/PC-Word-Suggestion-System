using System.Diagnostics;
using PCWord.Core;
using PCWord.Core.Input;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Context;
using PCWord.Prediction.Ranking;
using PCWord.Prediction.Diagnostics;

namespace PCWord.Stage0;

internal sealed class ControlledEditorForm : Form
{
    private readonly RichTextBox editor = new() { Multiline = true, ScrollBars = RichTextBoxScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 12) };
    private readonly CheckBox sensitive = new() { Text = "Treat this field as sensitive (context disabled)", AutoSize = true };
    private readonly CheckBox enabled = new() { Text = "Suggestions enabled", AutoSize = true, Checked = true };
    private readonly Label report = new() { AutoSize = true, MaximumSize = new Size(760, 0), Padding = new Padding(0, 8, 0, 8) };
    private readonly TextBox replacement = new() { Width = 180, PlaceholderText = "Explicit replacement" };
    private readonly Button apply = new() { Text = "Apply explicit edit", AutoSize = true };
    private readonly Button sample = new() { Text = "Load sample text", AutoSize = true };
    private readonly Button performance = new() { Text = "Performance stats", AutoSize = true };
    private readonly SuggestionPopupControl popup = new();
    private readonly CompletionMetrics metrics = new();
    private readonly ControlledEditorInputAdapter inputAdapter;
    private readonly DictionaryCompletionCoordinator completionCoordinator;
    private IReadOnlyList<string> currentCandidates = Array.Empty<string>();
    private bool updating;

    public ControlledEditorForm()
    {
        CandidateRanker? loadedRanker = CreateRanker(metrics);
        CandidateRanker ranker = loadedRanker ?? new CandidateRanker(new PrefixIndex(Array.Empty<DictionaryEntry>()));
        inputAdapter = new ControlledEditorInputAdapter(editor, () => sensitive.Checked, metrics);
        completionCoordinator = new DictionaryCompletionCoordinator(inputAdapter, ranker, metrics: metrics);
        completionCoordinator.SuggestionsChanged += CompletionCoordinatorOnSuggestionsChanged;

        Text = "PCWord — Controlled Editor";
        MinimumSize = new Size(760, 520);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        toolbar.Controls.Add(sample); toolbar.Controls.Add(performance); toolbar.Controls.Add(enabled); toolbar.Controls.Add(sensitive); toolbar.Controls.Add(replacement); toolbar.Controls.Add(apply);
        var editorHost = new Panel { Dock = DockStyle.Fill };
        editor.Dock = DockStyle.Fill;
        editorHost.Controls.Add(editor);
        editorHost.Controls.Add(popup);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 82, Padding = new Padding(8) }; bottom.Controls.Add(report);
        Controls.Add(editorHost); Controls.Add(bottom); Controls.Add(toolbar);

        editor.TextChanged += (_, _) => UpdateReport();
        editor.SelectionChanged += (_, _) => UpdateReport();
        editor.Enter += (_, _) => UpdateReport();
        editor.Leave += (_, _) => { popup.Hide(); completionCoordinator.Dismiss(); UpdateReport(); };
        editor.KeyDown += Editor_KeyDown;
        sensitive.CheckedChanged += (_, _) => { inputAdapter.Refresh(); UpdateReport(); };
        enabled.CheckedChanged += (_, _) => { completionCoordinator.SetEnabled(enabled.Checked); UpdateReport(); };
        apply.Click += (_, _) => ApplyEdit();
        performance.Click += (_, _) => ShowPerformanceSummary();
        sample.Click += (_, _) =>
        {
            editor.Text = "Try typing 'pro' or 'pred' to see dictionary suggestions. This is a controlled-editor demo.";
            editor.SelectionStart = editor.Text.IndexOf("pro", StringComparison.Ordinal);
            editor.SelectionLength = 0;
            editor.Focus();
            UpdateReport();
        };
        Deactivate += (_, _) => popup.Hide();
        FormClosed += (_, _) => { completionCoordinator.Dispose(); inputAdapter.Dispose(); };
        UpdateReport();
        inputAdapter.Refresh();
    }

    private async void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            completionCoordinator.Dismiss();
            popup.Hide();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        int index = e.Control && e.KeyCode == Keys.Space ? 0 :
            e.Control && e.KeyCode is Keys.D1 or Keys.NumPad1 ? 0 :
            e.Control && e.KeyCode is Keys.D2 or Keys.NumPad2 ? 1 :
            e.Control && e.KeyCode is Keys.D3 or Keys.NumPad3 ? 2 : -1;
        if (index < 0 || index >= currentCandidates.Count)
            return;

        e.Handled = true;
        e.SuppressKeyPress = true;
        await AcceptSuggestionAsync(index);
    }

    private static CandidateRanker? CreateRanker(CompletionMetrics metrics)
    {
        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            string dictionaryPath = Path.Combine(AppContext.BaseDirectory, "Dictionary", "sample.tsv");
            return new CandidateRanker(new PrefixIndex(DictionaryLoader.Load(dictionaryPath)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
        finally
        {
            metrics.Record(CompletionMetricStage.DictionaryStartup, Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private void CompletionCoordinatorOnSuggestionsChanged(object? sender, CompletionUpdatedEventArgs args)
    {
        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            if (!completionCoordinator.IsCurrent(args.ContextVersion, args.FieldId))
                return;

            currentCandidates = args.Candidates;
            if (!completionCoordinator.IsEnabled || args.Candidates.Count == 0 ||
                !editor.Focused || args.CaretBounds is null)
            {
                popup.Hide();
                return;
            }

            Control? host = editor.Parent;
            if (host is null)
            {
                popup.Hide();
                return;
            }

            popup.Height = args.Candidates.Count * 25 + 6;
            CaretScreenBounds caretBounds = args.CaretBounds.Value;
            Point hostAnchor = host.PointToClient(new Point(caretBounds.Left, caretBounds.Bottom + 4));
            int x = Math.Clamp(hostAnchor.X, 0, Math.Max(0, host.ClientSize.Width - popup.Width));
            int y = hostAnchor.Y;
            if (y + popup.Height > host.ClientSize.Height)
            {
                Point aboveCaret = host.PointToClient(new Point(caretBounds.Left, caretBounds.Top - popup.Height - 4));
                y = aboveCaret.Y;
            }
            y = Math.Clamp(y, 0, Math.Max(0, host.ClientSize.Height - popup.Height));
            popup.ShowSuggestions(args.Candidates, new Point(x, y));
        }
        finally
        {
            metrics.Record(CompletionMetricStage.UiUpdate, Stopwatch.GetElapsedTime(startedAt));
            UpdateReport();
        }
    }

    private async Task AcceptSuggestionAsync(int index)
    {
        TextEditResult result = await completionCoordinator.AcceptAsync(index);
        popup.Hide();
        if (result.WasApplied)
        {
            completionCoordinator.Dismiss();
            editor.Focus();
            UpdateReport();
            return;
        }

        currentCandidates = Array.Empty<string>();
        string message = result.Status switch
        {
            TextEditStatus.RefusedSensitiveField => "The current field is marked sensitive.",
            TextEditStatus.StaleContext => "The text changed before the suggestion could be applied.",
            TextEditStatus.Cancelled => "The edit was cancelled.",
            TextEditStatus.Unsupported => "The current editor context is unavailable.",
            _ => "The suggestion could not be verified and was not applied."
        };
        report.Text = message;
    }

    private void UpdateReport()
    {
        if (updating || IsDisposed) return;
        updating = true;
        try
        {
            InputContextSnapshot? snapshot = inputAdapter.LatestSnapshot;
            string focus = snapshot?.HasFocus == true ? "editor" : "none";
            string caret = snapshot?.CaretOffset?.ToString() ?? "unavailable";
            string selected = snapshot?.SelectionLength > 0
                ? $"{snapshot.SelectionLength} UTF-16 units selected"
                : "no selection";
            bool eligible = snapshot is { ContextAvailable: true, HasFocus: true,
                Sensitivity: InputFieldSensitivity.KnownSafe, SelectionLength: 0, IsComposing: false };
report.Text = $"Focus: {focus} | Caret: {caret} | {selected} | Eligible: {eligible} | Context: {(snapshot?.ContextAvailable == true ? "bounded" : "unavailable")}\nVersion: {snapshot?.Version.ToString() ?? "n/a"} | Suggestions enabled: {enabled.Checked} | Sensitive: {sensitive.Checked} | Composition: {snapshot?.IsComposing == true}\nCtrl+Space / Ctrl+1–3 accepts; Esc dismisses. Owned editor only; external application integration is disabled.";
        }
        finally { updating = false; }
    }

    private void ShowPerformanceSummary()
    {
        string details = string.Join(Environment.NewLine, metrics.GetSnapshot().Select(summary =>
        {
            string percentile = summary.MedianMilliseconds is double median && summary.P95Milliseconds is double p95
                ? $"median {median:F3} ms, p95 {p95:F3} ms"
                : "unavailable";
            return $"{summary.Stage}: {percentile}; samples {summary.SampleCount} (window {summary.WindowSampleCount})";
        }));
        MessageBox.Show(this, details, "Aggregate performance timings", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ApplyEdit()
    {
        try
        {
            var context = TextContext.Capture(editor.Text, editor.SelectionStart, editor.SelectionLength, editor.Focused, sensitive.Checked);
            var result = context.ApplyExplicitEdit(replacement.Text);
            popup.Hide();
            editor.Text = result.Text; editor.SelectionStart = result.Caret; editor.SelectionLength = 0; editor.Focus();
            UpdateReport();
        }
        catch (InvalidOperationException) { MessageBox.Show(this, "Focus the editor and clear any sensitive-field flag before applying an edit.", "Edit unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
