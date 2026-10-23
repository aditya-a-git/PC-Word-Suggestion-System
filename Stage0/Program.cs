using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PCWord.Stage0;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new FeasibilityForm());
    }
}

internal sealed class FeasibilityForm : Form
{
    private readonly RichTextBox editor = new() { Dock = DockStyle.Fill, AcceptsTab = true, Font = new System.Drawing.Font("Consolas", 12), Text = "Type here to inspect the controlled editor.\nMove the caret, select text, then click Refresh.\n" };
    private readonly TextBox snapshot = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new System.Drawing.Font("Consolas", 9) };
    private readonly Label status = new() { AutoSize = true, Text = "Ready — controlled RichTextBox only" };
    private readonly Timer debounce = new() { Interval = 120 };

    internal FeasibilityForm()
    {
        Text = "PCWord — Stage 0.1 Feasibility Editor";
        Width = 1000; Height = 720; MinimumSize = new System.Drawing.Size(760, 520);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var refresh = new Button { Text = "Refresh context" };
        var replace = new Button { Text = "Replace current word with ‘example’" };
        var insert = new Button { Text = "Insert ‘[probe]’ at caret" };
        var clear = new Button { Text = "Clear editor" };
        actions.Controls.AddRange([refresh, replace, insert, clear, status]);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 400 };
        split.Panel1.Controls.Add(editor); split.Panel2.Controls.Add(snapshot);
        Controls.Add(split); Controls.Add(actions);
        editor.TextChanged += (_, _) => { status.Text = "Text changed (controlled editor)"; debounce.Stop(); debounce.Start(); };
        editor.SelectionChanged += (_, _) => RefreshSnapshot();
        debounce.Tick += (_, _) => { debounce.Stop(); RefreshSnapshot(); };
        refresh.Click += (_, _) => RefreshSnapshot();
        replace.Click += (_, _) => ReplaceCurrentWord("example");
        insert.Click += (_, _) => { editor.SelectedText = "[probe]"; editor.Focus(); RefreshSnapshot(); };
        clear.Click += (_, _) => { editor.Clear(); editor.Focus(); };
        Activated += (_, _) => RefreshSnapshot();
        RefreshSnapshot();
    }

    private void ReplaceCurrentWord(string replacement)
    {
        var text = editor.Text;
        var caret = editor.SelectionStart;
        if (editor.SelectionLength > 0) { editor.SelectedText = replacement; status.Text = "Selected range replaced (controlled editor)"; RefreshSnapshot(); return; }
        var start = caret; var end = caret;
        while (start > 0 && IsWordChar(text[start - 1])) start--;
        while (end < text.Length && IsWordChar(text[end])) end++;
        if (start == end) { status.Text = "No word at caret; no edit made"; return; }
        editor.Select(start, end - start);
        editor.SelectedText = replacement;
        editor.SelectionStart = start + replacement.Length;
        editor.Focus();
        status.Text = "Current word replaced (controlled editor)";
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        var text = editor.Text;
        var caret = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var selectionEnd = Math.Clamp(caret + editor.SelectionLength, caret, text.Length);
        var left = caret; var right = caret;
        while (left > 0 && IsWordChar(text[left - 1])) left--;
        while (right < text.Length && IsWordChar(text[right])) right++;
        var contextStart = Math.Max(0, left - 80);
        var before = text.Substring(contextStart, left - contextStart);
        var current = text.Substring(left, right - left);
        var after = text.Substring(right, Math.Min(40, text.Length - right));
        snapshot.Text = $"Source: this process's WinForms RichTextBox (not TSF/UIA)\r\n" +
            $"Process: {Process.GetCurrentProcess().Id}  Focused control: {ActiveControl?.GetType().Name ?? "unknown"}\r\n" +
            $"Text length: {text.Length}  Caret UTF-16 offset: {caret}\r\n" +
            $"Selection: [{caret}, {selectionEnd}) length={editor.SelectionLength}\r\n" +
            $"Current word: [{left}, {right}) \"{current}\"\r\n" +
            $"Preceding context (max 80 chars): \"{Escape(before)}\"\r\n" +
            $"Following text (max 40 chars): \"{Escape(after)}\"\r\n\r\n" +
            "This view refreshes on text, selection/caret, focus activation, or Refresh. Text is not written to disk or logged.";
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '\'';
    private static string Escape(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
}
