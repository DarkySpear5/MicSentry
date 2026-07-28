namespace MicSentry;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _idleMinutesInput;
    private readonly CheckBox _startWithWindowsCheckbox;

    public int IdleMinutes => (int)_idleMinutesInput.Value;
    public bool StartWithWindows => _startWithWindowsCheckbox.Checked;

    public SettingsForm(AppSettings settings)
    {
        Text = "MicSentry Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(320, 160);
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
            Location = new Point(220, 18),
            Width = 70
        };

        _startWithWindowsCheckbox = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            Checked = settings.StartWithWindows,
            Location = new Point(15, 60)
        };

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(125, 115),
            Width = 85
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(220, 115),
            Width = 85
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(idleLabel);
        Controls.Add(_idleMinutesInput);
        Controls.Add(_startWithWindowsCheckbox);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }
}
