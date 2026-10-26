using PCWord.Core;
using PCWord.Core.Suggestions;

namespace PCWord.Stage0;

internal sealed class ControlledEditorForm : Form
{
    private static readonly string[] DemoWords =
    {
        "program", "programmer", "programming", "progress", "project", "projection",
        "process", "processor", "professional", "provide", "prediction", "predictive",
        "hello", "help", "helpful", "world", "wonderful", "there", "their", "these",
        "computer", "context", "complete", "completion", "suggestion", "system"
    };

    private const string FieldId = "controlled-editor";
    private readonly TextBox editor = new() { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 12) };
    private readonly CheckBox sensitive = new() { Text = "Treat this field as sensitive (context disabled)", AutoSize = true };
    private readonly Label report = new() { AutoSize = true, MaximumSize = new Size(760, 0), Padding = new Padding(0, 8, 0, 8) };
    private readonly TextBox replacement = new() { Width = 180, PlaceholderText = "Explicit replacement" };
    private readonly Button apply = new() { Text = "Apply explicit edit", AutoSize = true };
    private readonly Button sample = new() { Text = "Load sample text", AutoSize = true };
    private readonly SuggestionPopupForm popup = new();
    private readonly SuggestionState suggestionState = new();
    private long contextVersion;
    private long suggestionVersion;
    private string? suggestionText;
    private int suggestionSelectionStart;
    private int suggestionSelectionLength;
    private string? dismissedContextKey;
    private bool updating;
    private bool applyingSuggestion;

    public ControlledEditorForm()
    {
        Text = "PCWord Stage 3 — Controlled Editor";
        MinimumSize = new Size(760, 520);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        toolbar.Controls.Add(sample); toolbar.Controls.Add(sensitive); toolbar.Controls.Add(replacement); toolbar.Controls.Add(apply);
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(8) }; bottom.Controls.Add(report);
        Controls.Add(editor); Controls.Add(bottom); Controls.Add(toolbar);

        editor.TextChanged += (_, _) => { dismissedContextKey = null; UpdateReport(); RefreshSuggestions(); };
        editor.KeyUp += (_, _) => { UpdateReport(); RefreshSuggestions(); };
        editor.MouseUp += (_, _) => { dismissedContextKey = null; UpdateReport(); RefreshSuggestions(); };
        editor.Enter += (_, _) => { dismissedContextKey = null; UpdateReport(); RefreshSuggestions(); };
        editor.Leave += (_, _) => { popup.Hide(); DismissSuggestions(); UpdateReport(); };
        editor.KeyDown += Editor_KeyDown;
        sensitive.CheckedChanged += (_, _) => { dismissedContextKey = null; UpdateReport(); RefreshSuggestions(); };
        apply.Click += (_, _) => ApplyEdit();
        sample.Click += (_, _) =>
        {
            editor.Text = "Try typing 'progr' or 'comp' to see the Stage 3 popup. This is a controlled-editor demo.";
            editor.SelectionStart = editor.Text.IndexOf("progr", StringComparison.Ordinal);
            editor.SelectionLength = 0;
            editor.Focus();
            UpdateReport();
            RefreshSuggestions();
        };
        Deactivate += (_, _) => popup.Hide();
        FormClosed += (_, _) => popup.Dispose();
        UpdateReport();
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            dismissedContextKey = CurrentContextKey();
            DismissSuggestions();
            popup.Hide();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.Space)
        {
            AcceptSuggestion(0);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode is Keys.D1 or Keys.NumPad1)
        {
            AcceptSuggestion(0);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode is Keys.D2 or Keys.NumPad2)
        {
            AcceptSuggestion(1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode is Keys.D3 or Keys.NumPad3)
        {
            AcceptSuggestion(2);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private string CurrentContextKey() =>
        $"{editor.Text}\u001f{editor.SelectionStart}\u001f{editor.SelectionLength}\u001f{sensitive.Checked}";

    private void RefreshSuggestions()
    {
        if (updating || applyingSuggestion || IsDisposed)
            return;

        var context = TextContext.Capture(
            editor.Text, editor.SelectionStart, editor.SelectionLength,
            editor.Focused, sensitive.Checked);

        long version = ++contextVersion;
        string prefix = context.TypedPrefix;
        string[] candidates = context.IsEligible && prefix.Length > 0 &&
                              !StringComparer.Ordinal.Equals(CurrentContextKey(), dismissedContextKey)
            ? DemoWords.Where(word => word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(SuggestionState.MaximumSuggestions)
                .ToArray()
            : Array.Empty<string>();

        suggestionState.TryUpdate(version, context.IsEligible ? FieldId : null, candidates);
        suggestionVersion = suggestionState.ContextVersion;
        suggestionText = editor.Text;
        suggestionSelectionStart = editor.SelectionStart;
        suggestionSelectionLength = editor.SelectionLength;

        if (!context.IsEligible || candidates.Length == 0 || !editor.Focused)
        {
            popup.Hide();
            return;
        }

        // Coordinates are available only because this demo owns the RichTextBox.
        // A global adapter must provide and verify its own caret geometry.
        Point caret = editor.GetPositionFromCharIndex(editor.SelectionStart);
        Point screenAnchor = editor.PointToScreen(new Point(caret.X, caret.Y + editor.Font.Height + 4));
        popup.ShowSuggestions(this, candidates, screenAnchor);
    }

    private void AcceptSuggestion(int index)
    {
        if (!editor.Focused || sensitive.Checked ||
            !StringComparer.Ordinal.Equals(editor.Text, suggestionText) ||
            editor.SelectionStart != suggestionSelectionStart ||
            editor.SelectionLength != suggestionSelectionLength ||
            !suggestionState.TryAccept(suggestionVersion, FieldId, index, out string candidate))
        {
            popup.Hide();
            DismissSuggestions();
            return;
        }

        try
        {
            var context = TextContext.Capture(
                editor.Text, editor.SelectionStart, editor.SelectionLength,
                editor.Focused, sensitive.Checked);
            var result = context.ApplyExplicitEdit(candidate);

            applyingSuggestion = true;
            popup.Hide();
            editor.Text = result.Text;
            editor.SelectionStart = result.Caret;
            editor.SelectionLength = 0;
            dismissedContextKey = CurrentContextKey();
            editor.Focus();
        }
        catch (InvalidOperationException)
        {
            popup.Hide();
            MessageBox.Show(this, "The current field is not eligible for an edit.", "Suggestion not applied",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            applyingSuggestion = false;
            UpdateReport();
            RefreshSuggestions();
        }
    }

    private void DismissSuggestions()
    {
        suggestionState.Dismiss(++contextVersion);
        suggestionVersion = suggestionState.ContextVersion;
        suggestionText = editor.Text;
        suggestionSelectionStart = editor.SelectionStart;
        suggestionSelectionLength = editor.SelectionLength;
    }

    private void UpdateReport()
    {
        if (updating || IsDisposed) return;
        updating = true;
        try
        {
            var context = TextContext.Capture(editor.Text, editor.SelectionStart, editor.SelectionLength, editor.Focused, sensitive.Checked);
            string selected = context.HasSelection ? $"{context.SelectionLength} UTF-16 units selected" : "no selection";
            report.Text = $"Focus: {(context.HasFocus ? "editor" : "none")} | Caret: {context.Caret} | {selected} | Eligible: {context.IsEligible}\nCurrent word: {(context.IsSensitive ? "[disabled for sensitive field]" : context.CurrentWord)} | Previous context: {(context.IsSensitive ? "[disabled]" : context.PrecedingContext())}\nStage 3 demo: Ctrl+Space / Ctrl+1–3 accepts; Esc dismisses. Suggestions are sample-only until Stage 4.";
        }
        finally { updating = false; }
    }

    private void ApplyEdit()
    {
        try
        {
            var context = TextContext.Capture(editor.Text, editor.SelectionStart, editor.SelectionLength, editor.Focused, sensitive.Checked);
            var result = context.ApplyExplicitEdit(replacement.Text);
            popup.Hide();
            editor.Text = result.Text; editor.SelectionStart = result.Caret; editor.SelectionLength = 0; editor.Focus();
            dismissedContextKey = CurrentContextKey();
            UpdateReport();
            RefreshSuggestions();
        }
        catch (InvalidOperationException) { MessageBox.Show(this, "Focus the editor and clear any sensitive-field flag before applying an edit.", "Edit unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
