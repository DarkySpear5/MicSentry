using System.Runtime.InteropServices;
using System.Text.Json;
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

    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MicSentry", "mutestate.json");

    public bool IsAppMuted { get; private set; }

    // Device IDs to leave alone entirely, even though they're active capture
    // devices. Empty by default = original "mute everything" behaviour.
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
        SaveState();
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
        ClearState();
    }

    // Called once at startup, before the idle monitor starts. If a previous
    // process instance muted devices and then died (crash, forced restart,
    // reboot) before it could unmute them, a fresh instance normally has no
    // memory of that — IsAppMuted defaults to false — so it would never
    // auto-unmute, leaving the mic silently muted with the tray icon showing
    // green "watching" the whole time. This reconciles saved state against
    // what's actually still muted right now, so a resumed process picks up
    // exactly where the dead one left off instead of losing track entirely.
    // Returns true if a pending mute was restored (caller should treat this
    // session as already idle/muted).
    public bool TryRestorePendingMute()
    {
        try
        {
            if (!File.Exists(StatePath)) return false;

            var saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(StatePath));
            if (saved is null || saved.Count == 0)
            {
                ClearState();
                return false;
            }

            using var enumerator = new MMDeviceEnumerator();
            var restored = new Dictionary<string, bool>();

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (device)
                {
                    if (!saved.TryGetValue(device.ID, out bool wasMutedBefore))
                        continue;

                    try
                    {
                        // Only take ownership of devices that are STILL muted —
                        // if something else already unmuted it while we were
                        // down, there's nothing to restore for that device.
                        if (device.AudioEndpointVolume.Mute)
                            restored[device.ID] = wasMutedBefore;
                    }
                    catch (COMException)
                    {
                        // device went away — nothing to reconcile for it
                    }
                }
            }

            if (restored.Count == 0)
            {
                ClearState();
                return false;
            }

            _preMuteState.Clear();
            foreach (var kv in restored)
                _preMuteState[kv.Key] = kv.Value;

            IsAppMuted = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(_preMuteState));
        }
        catch
        {
            // best-effort — losing this only means a future crash can't
            // self-heal, it's not fatal to muting/unmuting right now
        }
    }

    private static void ClearState()
    {
        try { File.Delete(StatePath); } catch { }
    }

    // Fresh snapshot each time it's needed — devices come and go (a USB mic
    // gets unplugged, a virtual device starts up).
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
