namespace MicSentry;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IdleMonitor _idleMonitor;
    private readonly MicMuteController _muteController = new();
    private readonly AppSettings _settings;

    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly ToolStripMenuItem _devicesMenuItem;

    private Icon? _previousIcon;

    private static string AppVersion
    {
        get
        {
            var v = Application.ProductVersion;
            int plus = v.IndexOf('+'); // strip the source-revision suffix .NET appends
            return plus >= 0 ? v[..plus] : v;
        }
    }

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _muteController.ExcludedDeviceIds = new HashSet<string>(_settings.ExcludedDeviceIds);

        _idleMonitor = new IdleMonitor(TimeSpan.FromMinutes(_settings.IdleMinutes), TimeSpan.FromSeconds(1));
        _idleMonitor.IdleThresholdReached += OnIdleThresholdReached;
        _idleMonitor.ActivityResumed += OnActivityResumed;

        _statusMenuItem = new ToolStripMenuItem("Status: —") { Enabled = false };
        // Shown so you can confirm at a glance which version is actually running.
        var versionMenuItem = new ToolStripMenuItem($"Version {AppVersion}") { Enabled = false };
        _enabledMenuItem = new ToolStripMenuItem("Enabled", null, OnToggleEnabledClicked) { Checked = _settings.Enabled };
        _devicesMenuItem = new ToolStripMenuItem("Devices to Mute");
        var settingsMenuItem = new ToolStripMenuItem("Settings...", null, OnSettingsClicked);
        var exitMenuItem = new ToolStripMenuItem("Exit", null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(versionMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(_devicesMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenuItem);
        menu.Opening += (_, _) => RebuildDevicesSubmenu();

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true
        };

        if (_settings.Enabled)
            _idleMonitor.Start();

        UpdateVisualState();
    }

    private void RebuildDevicesSubmenu()
    {
        _devicesMenuItem.DropDownItems.Clear();

        List<(string Id, string Name)> devices;
        try
        {
            devices = MicMuteController.GetAvailableDevices();
        }
        catch
        {
            devices = new List<(string, string)>();
        }

        if (devices.Count == 0)
        {
            _devicesMenuItem.DropDownItems.Add(new ToolStripMenuItem("(no active mic inputs found)") { Enabled = false });
            return;
        }

        foreach (var (id, name) in devices)
        {
            var item = new ToolStripMenuItem(name)
            {
                Checked = !_muteController.ExcludedDeviceIds.Contains(id),
                CheckOnClick = true,
            };
            item.Click += (_, _) => OnDeviceToggled(id, item.Checked);
            _devicesMenuItem.DropDownItems.Add(item);
        }
    }

    private void OnDeviceToggled(string deviceId, bool shouldMute)
    {
        if (shouldMute)
            _muteController.ExcludedDeviceIds.Remove(deviceId);
        else
            _muteController.ExcludedDeviceIds.Add(deviceId);

        _settings.ExcludedDeviceIds = _muteController.ExcludedDeviceIds.OrderBy(x => x).ToList();
        _settings.Save();
    }

    private void OnIdleThresholdReached(object? sender, EventArgs e)
    {
        try
        {
            _muteController.MuteAll();
            UpdateVisualState();
            _notifyIcon.ShowBalloonTip(3000, "MicSentry",
                $"Mic muted — idle for {_settings.IdleMinutes} min.", ToolTipIcon.Info);
        }
        catch (Exception)
        {
            _notifyIcon.ShowBalloonTip(4000, "MicSentry",
                "Couldn't mute your mic — check Windows Sound settings.", ToolTipIcon.Warning);
        }
    }

    private void OnActivityResumed(object? sender, EventArgs e)
    {
        if (!_muteController.IsAppMuted) return;

        try
        {
            _muteController.UnmuteAll();
            UpdateVisualState();
            _notifyIcon.ShowBalloonTip(3000, "MicSentry", "Mic unmuted — welcome back.", ToolTipIcon.Info);
        }
        catch (Exception)
        {
            _notifyIcon.ShowBalloonTip(4000, "MicSentry",
                "Couldn't unmute your mic — check Windows Sound settings.", ToolTipIcon.Warning);
        }
    }

    private void OnToggleEnabledClicked(object? sender, EventArgs e)
    {
        _settings.Enabled = !_settings.Enabled;
        _settings.Save();
        _enabledMenuItem.Checked = _settings.Enabled;

        if (_settings.Enabled)
        {
            _idleMonitor.Start();
        }
        else
        {
            _idleMonitor.Stop();
            if (_muteController.IsAppMuted)
                _muteController.UnmuteAll();
        }

        UpdateVisualState();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK) return;

        _settings.IdleMinutes = form.IdleMinutes;
        _settings.StartWithWindows = form.StartWithWindows;
        _settings.Save();

        _idleMonitor.IdleThreshold = TimeSpan.FromMinutes(_settings.IdleMinutes);
        StartupRegistration.SetEnabled(_settings.StartWithWindows);

        UpdateVisualState();
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (_muteController.IsAppMuted)
        {
            try { _muteController.UnmuteAll(); } catch { /* best effort on the way out */ }
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private void UpdateVisualState()
    {
        Color color;
        string statusText;

        if (!_settings.Enabled)
        {
            color = TrayIconFactory.DisabledColor;
            statusText = "Disabled";
        }
        else if (_muteController.IsAppMuted)
        {
            color = TrayIconFactory.MutedColor;
            statusText = "Muted (idle)";
        }
        else
        {
            color = TrayIconFactory.ActiveColor;
            statusText = $"Watching (mutes after {_settings.IdleMinutes} min idle)";
        }

        var newIcon = TrayIconFactory.CreateStarIcon(color);
        _notifyIcon.Icon = newIcon;
        _previousIcon?.Dispose();
        _previousIcon = newIcon;

        _statusMenuItem.Text = "Status: " + statusText;

        string tip = "MicSentry — " + statusText;
        _notifyIcon.Text = tip.Length > 63 ? tip[..63] : tip;
    }
}
