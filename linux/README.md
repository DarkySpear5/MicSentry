# MicSentry for Linux

⚠️ **Experimental and untested.** This was built and syntax/logic-checked from a Windows machine — the settings persistence and the mic-mute state logic have automated tests and pass, but the actual GTK tray icon, D-Bus idle detection, and pactl integration have never been run on a real Linux desktop. It should be safe to try (see "Safety" below), but don't be surprised if something doesn't work first try. [Open an issue](../../issues) if it doesn't — that's how this gets fixed.

Same idea as the Windows version: mutes your mic after real inactivity, unmutes itself when you're back, mutes every real mic input (not just one).

## How it works here

- **Idle detection** tries, in order: GNOME's Mutter D-Bus idle monitor (covers GNOME on X11 and Wayland), then the X11 MIT-SCREEN-SAVER extension (covers KDE Plasma X11, XFCE, and most other X11 window managers). If neither is available — most notably **non-GNOME Wayland compositors** like Sway or KDE Plasma Wayland — idle detection is unavailable and **the app will never auto-mute** rather than guess. The tray icon and menu will say "Idle detection unavailable" in that case.
- **Muting** uses `pactl` (works with PulseAudio and PipeWire's pulse-compatibility layer, which covers the large majority of Linux desktops) against every real input source, skipping `.monitor` sources (those are output loopbacks, not microphones).
- **Tray icon** uses AppIndicator (Ayatana or the older AppIndicator3, whichever is present). **Stock GNOME Shell does not show tray icons at all** unless you install the "AppIndicator and KStatusNotifierItem Support" extension — see below.

## Install

```bash
git clone https://github.com/DarkySpear5/MicSentry.git
cd MicSentry/linux
./install.sh
```

The script checks for missing dependencies and tells you the exact package names for your distro rather than failing silently. Everything installs under `~/.local` and `~/.config` — no `sudo`, nothing touches system paths.

Dependencies: `python3`, `python3-gi` (PyGObject) + GTK3, an AppIndicator GObject-introspection typelib, `python3-xlib`, and `pactl`.

```bash
# Debian/Ubuntu
sudo apt install python3-gi gir1.2-ayatanaappindicator3-0.1 gir1.2-notify-0.7 python3-xlib pulseaudio-utils

# Fedora
sudo dnf install python3-gobject libappindicator-gtk3 python3-xlib pulseaudio-utils

# Arch
sudo pacman -S python-gobject libayatana-appindicator python-xlib libpulse libnotify
```

**GNOME users:** install [AppIndicator and KStatusNotifierItem Support](https://extensions.gnome.org/extension/615/appindicator-support/) or you won't see the tray icon at all — this isn't a MicSentry bug, GNOME removed tray icon support by default.

Uninstall any time with `./uninstall.sh` from this same folder.

## Safety

Nothing here needs or asks for root. The installer only writes to `~/.local/share/micsentry`, `~/.local/bin`, `~/.local/share/applications`, and `~/.config/micsentry` — normal user-owned paths, same as any other per-user app. It never touches system config, never installs a systemd system service, and autostart is a standard per-user `~/.config/autostart` entry you can delete by hand at any time. The mute logic only ever flips a source's mute flag — never volume, never default-device assignment, never anything requiring elevated privileges.

## Known gaps

- No idle detection on non-GNOME Wayland compositors (Sway, KDE Plasma Wayland, etc.) — auto-mute simply won't activate there yet.
- Only tested against `pactl`; systems running bare ALSA with no sound server aren't supported.
- No `.deb`/`.rpm`/AppImage packaging — just a script you run from a cloned checkout.

Contributions and bug reports welcome, especially "I tried it on X and here's what happened."
