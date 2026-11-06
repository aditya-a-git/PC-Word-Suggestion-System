using System.Drawing;
using System.Windows.Forms;
using PCWord.WindowsUI;

ApplicationConfiguration.Initialize();
Application.Run(new PopupHarnessForm());

internal sealed class PopupHarnessForm : Form
{
    private readonly SuggestionPopupWindow _popup = new();
    private readonly Label _status = new()
    {
        AutoSize = true,
        MaximumSize = new Size(480, 0),
        Text = "Synthetic UI harness only. No external text is read or edited."
    };

    public PopupHarnessForm()
    {
        Text = "PCWord Suggestion Popup Harness";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 150);
        var show = new Button
        {
            AutoSize = true,
            Text = "Show sample popup at mouse pointer",
            Dock = DockStyle.Top
        };
        show.Click += (_, _) =>
        {
            _popup.UpdateSuggestions(
                ["completion", "complete", "completing"],
                selectedIndex: 0,
                nextWord: false);
            Point pointer = Cursor.Position;
            _popup.ShowAtCaret(new Rectangle(pointer.X, pointer.Y, 1, 16));
            _status.Text = "Popup shown at pointer coordinates. The harness form should remain active; this is not caret integration.";
        };
        _popup.CandidateClicked += index =>
            _status.Text = $"Clicked synthetic candidate {index + 1}; no document edit was attempted.";
        FormClosed += (_, _) => _popup.Dispose();
        Controls.Add(_status);
        Controls.Add(show);
    }
}
