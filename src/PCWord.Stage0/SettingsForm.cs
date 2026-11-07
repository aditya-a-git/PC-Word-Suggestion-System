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
            Text = "The editor processes only bounded context locally. It does not send text over the network or save documents or prediction context in settings. The sensitive-field control disables context collection.\r\n\r\nNo raw keystrokes are logged. Settings contain configuration only. Prediction stays available offline."
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
                    Text = "Partial-word completion uses the installed local dictionary. Next-word prediction is available when a local model is present. Up to three suggestions are shown; Ctrl+Space accepts the first and Ctrl+1–3 accepts a numbered suggestion. Mode and count selection are not currently configurable."
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