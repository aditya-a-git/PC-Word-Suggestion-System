using System.Drawing;

namespace PCWord.Stage0;

/// <summary>
/// A non-activating demonstration popup for the controlled editor only. It deliberately
/// does not claim system-wide caret positioning or compatibility.
/// </summary>
internal sealed class SuggestionPopupForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;

    private readonly Label _heading = new()
    {
        AutoSize = false,
        Text = "PCWord suggestions  ·  Ctrl+Space accepts #1  ·  Esc hides",
        Left = 8,
        Top = 6,
        Width = 300,
        Height = 22,
        Font = new Font("Segoe UI", 8, FontStyle.Bold)
    };

    private readonly Label[] _candidateLabels = Enumerable.Range(0, 3)
        .Select(index => new Label
        {
            AutoSize = false,
            Left = 8,
            Top = 30 + index * 25,
            Width = 300,
            Height = 23,
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleLeft
        })
        .ToArray();

    public SuggestionPopupForm()
    {
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        ShowIcon = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(320, 30 + 3 * 25);
        BackColor = SystemColors.Info;
        ForeColor = SystemColors.InfoText;
        Controls.Add(_heading);
        Controls.AddRange(_candidateLabels);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    public void ShowSuggestions(Form owner, IReadOnlyList<string> suggestions, Point anchor)
    {
        if (suggestions.Count == 0)
        {
            Hide();
            return;
        }

        for (int i = 0; i < _candidateLabels.Length; i++)
        {
            bool available = i < suggestions.Count;
            _candidateLabels[i].Visible = available;
            _candidateLabels[i].Text = available ? $"{i + 1}. {suggestions[i]}   (Ctrl+{i + 1})" : string.Empty;
        }

        ClientSize = new Size(320, 30 + suggestions.Count * 25);
        Rectangle workingArea = Screen.FromPoint(anchor).WorkingArea;
        int x = Math.Clamp(anchor.X, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - Width));
        int y = anchor.Y;
        if (y + Height > workingArea.Bottom)
            y = Math.Max(workingArea.Top, anchor.Y - Height - 8);
        Location = new Point(x, y);

        if (!Visible)
            Show(owner);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaNoActivate;
            return;
        }

        base.WndProc(ref m);
    }
}
