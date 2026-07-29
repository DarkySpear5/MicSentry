using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace MicSentry;

// Mutes every active capture (input) device at the OS level — covers both a physical
// mic and any virtual device layered on top of it (e.g. SteelSeries Sonar's virtual
// microphone), since both show up as independent capture endpoints. Remembers each
// device's own prior mute state so it never unmutes something that was already
// muted for an unrelated reason before this controller stepped in.
internal sealed class MicMuteController
{
    private readonly Dictionary<string, bool> _preMuteState = new();

    public bool IsAppMuted { get; private set; }

    // Device IDs to leave alone entirely, even though they're active capture
    // devices — empty by default, which preserves the original "mute
    // everything" behavior for anyone who never touches this setting.
    public HashSet<string> ExcludedDeviceIds { get; set; } = new();

    public void MuteAll()
    {
        if (IsAppMuted) return;

        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                if (ExcludedDeviceIds.Contains(device.ID))
                    continue;

                try
                {
                    bool wasMuted = device.AudioEndpointVolume.Mute;
                    _preMuteState[device.ID] = wasMuted;
                    if (!wasMuted)
                        device.AudioEndpointVolume.Mute = true;
                }
                catch (COMException)
                {
                    // device likely went away mid-enumeration (e.g. unplugged) — skip it
                }
            }
        }

        IsAppMuted = true;
    }

    public void UnmuteAll()
    {
        if (!IsAppMuted) return;

        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                if (_preMuteState.TryGetValue(device.ID, out bool wasMuted) && !wasMuted)
                {
                    try
                    {
                        device.AudioEndpointVolume.Mute = false;
                    }
                    catch (COMException)
                    {
                        // device likely went away — nothing to restore it to anyway
                    }
                }
            }
        }

        _preMuteState.Clear();
        IsAppMuted = false;
    }

    // For populating the "Devices to Mute" tray submenu — a fresh snapshot
    // each time, since devices can come and go (USB mic plugged/unplugged).
    public static List<(string Id, string Name)> GetAvailableDevices()
    {
        var devices = new List<(string, string)>();

        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                devices.Add((device.ID, device.FriendlyName));
            }
        }

        return devices;
    }
}
