# MicSentry

A tiny tray app that mutes your microphone after you've stepped away from your PC, and unmutes itself the instant you're back. No clicking, no forgetting. Windows is the primary, fully-tested target; a Linux build also exists (see below).

<img src="Assets/state-active.png" width="64" alt="Watching"> <img src="Assets/state-muted.png" width="64" alt="Muted"> <img src="Assets/state-disabled.png" width="64" alt="Disabled">

*Green = watching, Red = muted (idle), Gray = disabled.*

## Why

If you're in a voice call and step away from your desk for a few minutes without muting, whatever's happening near your mic keeps broadcasting. MicSentry watches for real inactivity and mutes for you, then quietly unmutes when you're back — no need to remember either half.

## How it works

(This section describes the Windows build. The Linux build uses equivalent OS-level mechanisms — see [linux/README.md](linux/README.md) for specifics.)

- **Idle detection** uses Windows' own `GetLastInputInfo` — the same signal that drives your screensaver/lock timer. It only reacts to real keyboard/mouse/touch input, never screen content, so it can't be fooled by a video playing, a game character auto-moving, or anything else happening on screen while you're actually away.
- **Muting** happens at the OS audio-device level (Windows Core Audio), for *every* active microphone input — not just the system default, and not tied to any single app like Discord. If you run something like SteelSeries Sonar or another virtual audio layer on top of a physical mic, both get muted, and both get correctly restored.
- **Unmuting** is automatic on the next real input, and MicSentry only ever restores a device it muted itself — it won't touch a mic you muted manually for an unrelated reason.

## Privacy

This was built specifically to be trustworthy for exactly the situation where trust matters: private conversations you don't want overheard.

- **Zero network calls.** MicSentry makes no outbound connections of any kind — nothing to leak, nothing to phone home to. You can verify this yourself in Task Manager's network column or a firewall log.
- **No keylogging.** It only ever reads *when* the last input happened, never *what* was pressed.
- **Open source.** The entire codebase is small enough to read in one sitting — see for yourself.
- **Visible confirmation.** Every auto-mute and auto-unmute shows a tray notification, so you always know it's actually working instead of trusting a silent background process.

## Install

**Option 1 — Installer (recommended):** grab the latest `MicSentrySetup.exe` from [Releases](../../releases), run it, done. Installs per-user (no admin rights needed), adds a Start Menu entry and uninstaller. The download is ~110MB because it bundles the .NET runtime so it works with zero prerequisites — that's disk/download size only, not memory use, which stays around 40MB while running either way.

**Option 2 — Build from source:**

```bash
git clone https://github.com/DarkySpear5/MicSentry.git
cd MicSentry
dotnet build -c Release
```

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download). The built exe will be in `bin\Release\net9.0-windows\`.

**Linux: ⚠️ experimental, not yet verified on a real machine — contributions and testers welcome.** The idle-detection and mute logic have automated tests and pass, but the actual GTK tray icon / D-Bus / pactl integration has never been run on real hardware. See [linux/README.md](linux/README.md) for install steps, dependencies, and known gaps (notably: no tray icon on stock GNOME without an extra extension, and no idle detection yet on non-GNOME Wayland compositors). It's designed to be safe to try either way — no root required anywhere, nothing touches system paths.

## Usage

Right-click the tray icon for:

- **Enabled** — toggle protection on/off
- **Settings...** — idle timeout (minutes) and start-with-Windows
- **Exit**

## License

MIT — see [LICENSE](LICENSE). Free to use, modify, and share.
