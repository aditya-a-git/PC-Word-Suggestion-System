using PCWord.Core;

namespace PCWord.Stage0;

internal sealed class ControlledEditorForm : Form
{
    private readonly TextBox editor = new() { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 12) };
    private readonly CheckBox sensitive = new() { Text = "Treat this field as sensitive (context disabled)", AutoSize = true };
    private readonly Label report = new() { AutoSize = true, MaximumSize = new Size(760, 0), Padding = new Padding(0, 8, 0, 8) };
    private readonly TextBox replacement = new() { Width = 180, PlaceholderText = "Explicit replacement" };
    private readonly Button apply = new() { Text = "Apply explicit edit", AutoSize = true };
    private readonly Button sample = new() { Text = "Load sample text", AutoSize = true };
    private bool updating;

    public ControlledEditorForm()
    {
        Text = "PCWord Stage 0 — Controlled Editor";
        MinimumSize = new Size(760, 520);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        toolbar.Controls.Add(sample); toolbar.Controls.Add(sensitive); toolbar.Controls.Add(replacement); toolbar.Controls.Add(apply);
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8) }; bottom.Controls.Add(report);
        Controls.Add(editor); Controls.Add(bottom); Controls.Add(toolbar);
        editor.TextChanged += (_, _) => UpdateReport();
        editor.KeyUp += (_, _) => UpdateReport();
        editor.MouseUp += (_, _) => UpdateReport();
        editor.Enter += (_, _) => UpdateReport();
        editor.Leave += (_, _) => UpdateReport();
        sensitive.CheckedChanged += (_, _) => UpdateReport(); apply.Click += (_, _) => ApplyEdit();
        sample.Click += (_, _) =>
        {
            editor.Text = "Stage 0 sample: controlled editor context.";
            editor.SelectionStart = editor.Text.IndexOf("controlled", StringComparison.Ordinal); editor.SelectionLength = 0; editor.Focus();
        };
        UpdateReport();
    }

    private void UpdateReport()
    {
        if (updating || IsDisposed) return;
        updating = true;
        try
        {
            var context = TextContext.Capture(editor.Text, editor.SelectionStart, editor.SelectionLength, editor.Focused, sensitive.Checked);
            string selected = context.HasSelection ? $"{context.SelectionLength} UTF-16 units selected" : "no selection";
            report.Text = $"Focus: {(context.HasFocus ? "editor" : "none")} | Caret: {context.Caret} | {selected} | Eligible: {context.IsEligible}\nCurrent word: {(context.IsSensitive ? "[disabled for sensitive field]" : context.CurrentWord)} | Previous context: {(context.IsSensitive ? "[disabled]" : context.PrecedingContext())}";
        }
        finally { updating = false; }
    }

    private void ApplyEdit()
    {
        try
        {
            var context = TextContext.Capture(editor.Text, editor.SelectionStart, editor.SelectionLength, editor.Focused, sensitive.Checked);
            var result = context.ApplyExplicitEdit(replacement.Text);
            editor.Text = result.Text; editor.SelectionStart = result.Caret; editor.SelectionLength = 0; editor.Focus();
        }
        catch (InvalidOperationException) { MessageBox.Show(this, "Focus the editor and clear any sensitive-field flag before applying an edit.", "Edit unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
