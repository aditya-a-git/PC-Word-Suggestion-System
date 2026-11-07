using PCWord.Core;

namespace PCWord.Stage0;

internal sealed class SettingsForm : Form
{
    private readonly UserSettingsService _settings;
    private readonly CheckBox _enabled = new() { Text = "Enable PCWord suggestions", AutoSize = true, AccessibleName = "Enable PCWord suggestions" };
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(440, 0), Padding = new Padding(0, 10, 0, 0) };

    public SettingsForm(UserSettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Text = "PCWord Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont;

        var tabs = new TabControl { Width = 500, Height = 270, AccessibleName = "Settings categories" };
        var general = new TabPage("General") { Padding = new Padding(14) };
        var generalLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        _enabled.Checked = _settings.Current.Enabled;
        generalLayout.Controls.Add(_enabled);
        generalLayout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Text = "Turning PCWord off stops suggestions in the controlled editor and invalidates its pending suggestion state."
        });
        if (_settings.LoadStatus is not null)
            generalLayout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(440, 0), Text = _settings.LoadStatus });
        general.Controls.Add(generalLayout);

        var privacy = new TabPage("Privacy") { Padding = new Padding(14) };
        privacy.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = "PCWord processes bounded text locally in its own editor. Sensitive fields are rejected by the input context policy. Typed text and candidates are not written to settings or diagnostic logs.\r\n\r\nThe TSF probe is diagnostic only; PCWord does not currently provide production external-app context reading or insertion. No setting in this window can enable password-field access. Personalization and metrics controls are unavailable because those behaviors are not implemented as user-configurable runtime features."
        });
        tabs.TabPages.Add(general);
        tabs.TabPages.Add(new TabPage("Typing & Suggestions")
        {
            Padding = new Padding(14),
            Controls =
            {
                new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "Suggestions are produced only by the capabilities present in this build. Prediction-mode selection, suggestion count, and configurable keyboard shortcuts are not currently exposed because the runtime does not yet apply those settings."
                }
            }
        });
        tabs.TabPages.Add(new TabPage("Applications")
        {
            Padding = new Padding(14),
            Controls =
            {
                new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "System-wide application allowlisting is unavailable. The current TSF probe is diagnostic-only and is not a production input adapter."
                }
            }
        });
        tabs.TabPages.Add(privacy);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(8) };
        _message.Dock = DockStyle.Top;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var defaults = new Button { Text = "Restore defaults", AutoSize = true };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(defaults);
        footer.Controls.Add(buttons);
        footer.Controls.Add(_message);
        Controls.Add(tabs);
        Controls.Add(footer);
        AcceptButton = save;
        CancelButton = cancel;

        save.Click += (_, _) =>
        {
            if (!_settings.TryUpdate(_settings.Current with { Enabled = _enabled.Checked }, out var error))
            {
                _message.Text = error ?? "Settings could not be applied.";
                AccessibleDescription = _message.Text;
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        defaults.Click += (_, _) => _enabled.Checked = UserSettings.Defaults.Enabled;
    }
}