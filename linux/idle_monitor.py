"""Best-effort idle-time detection across Linux desktops.

There's no single universal equivalent of Windows' GetLastInputInfo here, so
this tries a couple of standard backends in order:

  1. GNOME's Mutter IdleMonitor over D-Bus — covers GNOME on both X11 and
     Wayland, which is the default on Ubuntu, Fedora Workstation, and others.
  2. The X11 MIT-SCREEN-SAVER extension — covers X11 sessions generally
     (KDE Plasma X11, XFCE, most X11 window managers).

If neither is available (e.g. a non-GNOME Wayland compositor like Sway or
KDE Plasma Wayland), `get_idle_seconds()` returns None. Callers MUST treat
None as "cannot verify idle state" and must never auto-mute in that case —
it's better to do nothing than to silently fail at the one thing this app
is supposed to be trusted for.
"""


class IdleMonitor:
    def __init__(self):
        self.backend = self._detect_backend()

    @property
    def available(self):
        return self.backend is not None

    def get_idle_seconds(self):
        if self.backend == "gnome":
            return self._gnome_idle_seconds()
        if self.backend == "xscreensaver":
            return self._xscreensaver_idle_seconds()
        return None

    def _detect_backend(self):
        if self._gnome_idle_seconds() is not None:
            return "gnome"
        if self._xscreensaver_idle_seconds() is not None:
            return "xscreensaver"
        return None

    def _gnome_idle_seconds(self):
        try:
            import gi

            gi.require_version("Gio", "2.0")
            from gi.repository import Gio, GLib

            bus = Gio.bus_get_sync(Gio.BusType.SESSION, None)
            result = bus.call_sync(
                "org.gnome.Mutter.IdleMonitor",
                "/org/gnome/Mutter/IdleMonitor/Core",
                "org.gnome.Mutter.IdleMonitor",
                "GetIdletime",
                None,
                GLib.VariantType.new("(t)"),
                Gio.DBusCallFlags.NONE,
                500,
                None,
            )
            (idle_ms,) = result.unpack()
            return idle_ms / 1000.0
        except Exception:
            return None

    def _xscreensaver_idle_seconds(self):
        try:
            from Xlib import display

            d = display.Display()
            info = d.screen().root.screensaver_query_info()
            return info.idle / 1000.0
        except Exception:
            return None
