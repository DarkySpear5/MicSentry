namespace MicSentry;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _idleMinutesInput;
    private readonly CheckBox _startWithWindowsCheckbox;
    private readonly CheckBox _checkForUpdatesCheckbox;

    public int IdleMinutes => (int)_idleMinutesInput.Value;
    public bool StartWithWindows => _startWithWindowsCheckbox.Checked;
    public bool CheckForUpdates => _checkForUpdatesCheckbox.Checked;

    public SettingsForm(AppSettings settings)
    {
        Text = "MicSentry Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 195);
        Font = new Font("Segoe UI", 9f);
        Icon = TrayIconFactory.CreateStarIcon(TrayIconFactory.ActiveColor);

        var idleLabel = new Label
        {
            Text = "Mute after idle (minutes):",
            AutoSize = true,
            Location = new Point(15, 22)
        };

        _idleMinutesInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 60,
            Value = Math.Clamp(settings.IdleMinutes, 1, 60),
            Location = new Point(235, 18),
            Width = 70
        };

        _startWithWindowsCheckbox = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            Checked = settings.StartWithWindows,
            Location = new Point(15, 60)
        };

        _checkForUpdatesCheckbox = new CheckBox
        {
            Text = "Check for updates on launch (contacts GitHub)",
            AutoSize = true,
            Checked = settings.CheckForUpdates,
            Location = new Point(15, 90)
        };

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(145, 150),
            Width = 85
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(240, 150),
            Width = 85
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(idleLabel);
        Controls.Add(_idleMinutesInput);
        Controls.Add(_startWithWindowsCheckbox);
        Controls.Add(_checkForUpdatesCheckbox);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }
}
