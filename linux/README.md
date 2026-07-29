# MicSentry for Linux

⚠️ **Experimental, lightly tested.** Confirmed working on a real Linux machine (thanks to an actual tester), but this was still built and mostly verified from a Windows machine, so treat it as less battle-tested than the Windows build. [Open an issue](../../issues) if something's off — that's how this gets fixed.

Same idea as the Windows version: mutes your mic after real inactivity, unmutes itself when you're back, mutes every real mic input by default (with an option to exclude specific ones — see below).

## How it works here

- **Idle detection** tries, in order: GNOME's Mutter D-Bus idle monitor (covers GNOME on X11 and Wayland), then the X11 MIT-SCREEN-SAVER extension (covers KDE Plasma X11, XFCE, and most other X11 window managers). If neither is available — most notably **non-GNOME Wayland compositors** like Sway or KDE Plasma Wayland — idle detection is unavailable and **the app will never auto-mute** rather than guess. The tray icon and menu will say "Idle detection unavailable" in that case.
- **Muting** uses `pactl` (works with PulseAudio and PipeWire's pulse-compatibility layer, which covers the large majority of Linux desktops) against every real input source by default, skipping `.monitor` sources (those are output loopbacks, not microphones). The tray menu's "Devices to Mute" submenu lists every detected input with a checkbox — uncheck one to exclude it from muting. Nothing's excluded by default.
- **Tray icon** uses AppIndicator (Ayatana or the older AppIndicator3, whichever is present). **Stock GNOME Shell does not show tray icons at all** unless you install the "AppIndicator and KStatusNotifierItem Support" extension — see below.
- **Hovering the tray icon** shows "MicSentry — <status>" via the indicator's title. Whether this actually renders as a mouse-hover tooltip depends on your tray host implementation — some desktop environments show it, some don't; this isn't something the app can force everywhere.
- **Process name**: the process renames itself (via `prctl`) so it shows up as `micsentry` in `ps`/`top`/`htop` and most system monitors' Name column, instead of the generic `python3` every Python script would otherwise show as — so you (or whoever's checking memory usage) can actually tell it apart from other Python processes.
- **Update check (off by default)**: Settings has an opt-in "Check for updates on launch" checkbox. When enabled, it makes exactly one request to GitHub's API at startup to see if a newer release exists, and shows a notification if so — it never auto-downloads or auto-installs anything, and it's completely silent if you're already up to date or if the request fails for any reason (no internet, rate-limited, whatever). This is the **one deliberate exception** to "zero network calls," which is exactly why it defaults to off and lives behind an explicit checkbox rather than running automatically.

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

The installer also drops a launchable icon on your Desktop (using `xdg-user-dir` to find the right folder if you have one installed, otherwise `~/Desktop`), alongside the existing applications-menu entry. Some file managers (GNOME Files/Nautilus especially) treat a brand new Desktop `.desktop` file as untrusted the first time — right-click it and choose "Allow Launching" if double-clicking doesn't do anything.

Uninstall any time with `./uninstall.sh` from this same folder.

## Safety

Nothing here needs or asks for root. The installer only writes to `~/.local/share/micsentry`, `~/.local/bin`, `~/.local/share/applications`, `~/.config/micsentry`, and your Desktop folder — normal user-owned paths, same as any other per-user app. It never touches system config, never installs a systemd system service, and autostart is a standard per-user `~/.config/autostart` entry you can delete by hand at any time. The mute logic only ever flips a source's mute flag — never volume, never default-device assignment, never anything requiring elevated privileges.

## Known gaps

- No idle detection on non-GNOME Wayland compositors (Sway, KDE Plasma Wayland, etc.) — auto-mute simply won't activate there yet.
- Only tested against `pactl`; systems running bare ALSA with no sound server aren't supported.
- No `.deb`/`.rpm`/AppImage packaging — just a script you run from a cloned checkout.

Contributions and bug reports welcome, especially "I tried it on X and here's what happened."
