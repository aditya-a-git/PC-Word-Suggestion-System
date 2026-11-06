using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PCWord.WindowsUI.Logic;

namespace PCWord.WindowsUI;

public sealed class SuggestionPopupWindow : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const int SwShownoactivate = 4;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint MonitorDefaultToNearest = 2;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);

    private readonly Label _kindLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Padding = new Padding(8, 2, 4, 2),
        TextAlign = ContentAlignment.MiddleLeft,
        TabStop = false
    };
    private readonly Button[] _candidates = Enumerable.Range(0, SuggestionPopupState.MaximumSuggestions)
        .Select(_ => new Button
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            TabStop = false,
            UseVisualStyleBackColor = false
        }).ToArray();

    public event Action<int>? CandidateClicked;

    public SuggestionPopupWindow()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(240, 0);
        MaximumSize = new Size(480, 0);
        Size = new Size(300, 132);
        Font = new Font("Segoe UI", 9f);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.Controls.Add(_kindLabel, 0, 0);
        for (int i = 0; i < _candidates.Length; i++)
        {
            int index = i;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _candidates[i].Click += (_, _) => CandidateClicked?.Invoke(index);
            _candidates[i].AccessibleRole = AccessibleRole.ListItem;
            _candidates[i].AccessibleName = $"Suggestion {i + 1}";
            layout.Controls.Add(_candidates[i], 0, i + 1);
        }
        Controls.Add(layout);
        ApplySystemColors();
        Visible = false;
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

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmMouseActivate)
        {
            message.Result = (IntPtr)MaNoActivate;
            return;
        }
        base.WndProc(ref message);
    }

    public void UpdateSuggestions(IReadOnlyList<string> candidates, int selectedIndex, bool nextWord)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count > SuggestionPopupState.MaximumSuggestions)
            throw new ArgumentOutOfRangeException(nameof(candidates));

        _kindLabel.Text = nextWord ? "Next word" : "Complete word";
        _kindLabel.AccessibleName = _kindLabel.Text;
        for (int i = 0; i < _candidates.Length; i++)
        {
            Button button = _candidates[i];
            bool available = i < candidates.Count;
            button.Visible = available;
            if (!available) continue;

            button.Text = $"{(i == selectedIndex ? "›" : " ")}  {i + 1}.  {candidates[i]}";
            button.AccessibleName = $"Suggestion {i + 1}: {candidates[i]}";
            button.AccessibleDescription = i == selectedIndex ? "Selected" : "Not selected";
            button.BackColor = i == selectedIndex ? SystemColors.Highlight : SystemColors.Info;
            button.ForeColor = i == selectedIndex ? SystemColors.HighlightText : SystemColors.InfoText;
        }
        Height = 22 + candidates.Count * 34 + 2;
    }

    /// <summary>Positions using physical screen pixels from TSF and native monitor metrics.</summary>
    public void ShowAtCaret(Rectangle caretScreenBounds, bool rightToLeft = false)
    {
        if (caretScreenBounds.Width < 0 || caretScreenBounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(caretScreenBounds));

        ApplySystemColors();
        Rectangle workArea = GetMonitorWorkArea(caretScreenBounds);
        Size physicalSize = GetPhysicalWindowSize();
        Rectangle placement = PopupPlacement.Calculate(caretScreenBounds, physicalSize, workArea, rightToLeft);
        if (!Visible)
            ShowWindow(Handle, SwShownoactivate);

        SetWindowPos(Handle, HwndTopmost, placement.Left, placement.Top,
            placement.Width, placement.Height, SwpNoActivate | SwpNoOwnerZOrder | SwpShowWindow);
    }

    public new void Hide()
    {
        if (IsHandleCreated)
        {
            base.Hide();
            SetWindowPos(Handle, HwndNotTopmost, 0, 0, 0, 0,
                SwpNoActivate | SwpNoMove | SwpNoSize | SwpNoOwnerZOrder);
        }
        else
        {
            base.Hide();
        }
    }

    private Size GetPhysicalWindowSize()
    {
        if (!IsHandleCreated) _ = Handle;
        if (!GetWindowRect(Handle, out NativeRect bounds))
            return new Size(Math.Max(1, Width), Math.Max(1, Height));
        return new Size(Math.Max(1, bounds.Right - bounds.Left),
            Math.Max(1, bounds.Bottom - bounds.Top));
    }

    private Rectangle GetMonitorWorkArea(Rectangle target)
    {
        var nativeRect = new NativeRect(target.Left, target.Top, target.Right, target.Bottom);
        IntPtr monitor = MonitorFromRect(ref nativeRect, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
            return Rectangle.FromLTRB(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
        return Screen.FromRectangle(target).WorkingArea;
    }

    private void ApplySystemColors()
    {
        bool highContrast = SystemInformation.HighContrast;
        BackColor = highContrast ? SystemColors.Control : SystemColors.Info;
        ForeColor = highContrast ? SystemColors.ControlText : SystemColors.InfoText;
        _kindLabel.BackColor = BackColor;
        _kindLabel.ForeColor = ForeColor;
        foreach (Button button in _candidates)
            button.FlatAppearance.BorderSize = highContrast ? 1 : 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect(int left, int top, int right, int bottom)
    {
        public int Left = left;
        public int Top = top;
        public int Right = right;
        public int Bottom = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
        int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
