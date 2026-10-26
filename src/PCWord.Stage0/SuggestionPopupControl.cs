using System.Drawing;

namespace PCWord.Stage0;

/// <summary>
/// Compact in-window suggestion surface. Because this is a non-selectable child control
/// rather than a top-level Form, showing it does not activate another window.
/// </summary>
internal sealed class SuggestionPopupControl : Panel
{
    private readonly Label[] _candidateLabels = Enumerable.Range(0, 3)
        .Select(index => new Label
        {
            AutoSize = false,
            Left = 6,
            Top = 3 + index * 25,
            Width = 224,
            Height = 24,
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleLeft,
            TabStop = false,
            BackColor = SystemColors.Info,
            ForeColor = SystemColors.InfoText
        })
        .ToArray();

    public SuggestionPopupControl()
    {
        TabStop = false;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = SystemColors.Info;
        ForeColor = SystemColors.InfoText;
        Size = new Size(238, 3 * 25 + 6);
        Visible = false;
        Controls.AddRange(_candidateLabels);
    }

    public void ShowSuggestions(IReadOnlyList<string> suggestions, Point anchor)
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
            _candidateLabels[i].Text = available ? $"{i + 1}.  {suggestions[i]}   (Ctrl+{i + 1})" : string.Empty;
        }

        Height = suggestions.Count * 25 + 6;
        Location = anchor;
        BringToFront();
        Visible = true;
    }
}
