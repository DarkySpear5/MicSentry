import fcntl
import os

import gi

gi.require_version("Gtk", "3.0")
from gi.repository import Gtk, GLib

try:
    gi.require_version("AyatanaAppIndicator3", "0.1")
    from gi.repository import AyatanaAppIndicator3 as AppIndicator3
except (ValueError, ImportError):
    gi.require_version("AppIndicator3", "0.1")
    from gi.repository import AppIndicator3

try:
    gi.require_version("Notify", "0.7")
    from gi.repository import Notify

    Notify.init("MicSentry")
    _NOTIFY_AVAILABLE = True
except Exception:
    _NOTIFY_AVAILABLE = False

from idle_monitor import IdleMonitor
from mic_mute import MicMuteController
from settings import AppSettings, SETTINGS_DIR
from update_checker import check_for_updates_on_launch

_APP_DIR = os.path.dirname(os.path.abspath(__file__))
_ENTRY_SCRIPT = os.path.join(_APP_DIR, "micsentry.py")
_ICON_DIR = os.path.join(_APP_DIR, "icons")
_AUTOSTART_PATH = os.path.expanduser("~/.config/autostart/micsentry.desktop")
_LOCK_PATH = os.path.join(SETTINGS_DIR, "micsentry.lock")


def _notify(title, body):
    if not _NOTIFY_AVAILABLE:
        return
    try:
        Notify.Notification.new(title, body, None).show()
    except Exception:
        pass


def _set_process_name(name):
    """Renames this process (as seen in ps/top/htop and most system monitors'
    Name column) from the generic "python3" to something identifiable, via
    the standard Linux prctl(PR_SET_NAME) syscall. Best-effort: silently does
    nothing if this isn't Linux or the call fails for any reason — it's a
    cosmetic fix, never worth crashing over. Names are truncated to 15 bytes,
    which is the kernel's actual limit for this field.
    """
    try:
        import ctypes

        libc = ctypes.CDLL("libc.so.6", use_errno=True)
        PR_SET_NAME = 15
        libc.prctl(PR_SET_NAME, name.encode("utf-8")[:15], 0, 0, 0)
    except Exception:
        pass


class TrayApp:
    def __init__(self):
        self.settings = AppSettings.load()
        self.idle_monitor = IdleMonitor()
        self.mute_controller = MicMuteController()
        self._was_idle = False

        self.indicator = AppIndicator3.Indicator.new(
            "micsentry",
            os.path.join(_ICON_DIR, "state-active.png"),
            AppIndicator3.IndicatorCategory.APPLICATION_STATUS,
        )
        self.indicator.set_status(AppIndicator3.IndicatorStatus.ACTIVE)
        try:
            self.indicator.set_title("MicSentry")
        except Exception:
            pass  # not every tray host surfaces this as a hover tooltip, but it's harmless either way

        self._build_menu()

        if not self.idle_monitor.available:
            _notify(
                "MicSentry",
                "Couldn't detect idle time on this desktop, so auto-mute is disabled. "
                "See the README for supported environments.",
            )

        self._update_visual_state()
        GLib.timeout_add_seconds(1, self._on_tick)

        if self.settings.check_for_updates:
            check_for_updates_on_launch(self._on_update_available)

    def _on_update_available(self, version, url):
        # Runs on update_checker's background thread — never touch GTK/
        # AppIndicator objects off the main thread, marshal back via idle_add.
        GLib.idle_add(self._show_update_notification, version, url)

    def _show_update_notification(self, version, url):
        _notify("MicSentry", f"Version {version} is available: {url}")
        return False  # one-shot; GLib.idle_add would otherwise repeat this

    def _build_menu(self):
        menu = Gtk.Menu()

        self.status_item = Gtk.MenuItem(label="Status: —")
        self.status_item.set_sensitive(False)
        menu.append(self.status_item)
        menu.append(Gtk.SeparatorMenuItem())

        self.enabled_item = Gtk.CheckMenuItem(label="Enabled")
        self.enabled_item.set_active(self.settings.enabled)
        self.enabled_item.connect("toggled", self._on_toggle_enabled)
        menu.append(self.enabled_item)
        menu.append(Gtk.SeparatorMenuItem())

        settings_item = Gtk.MenuItem(label="Settings...")
        settings_item.connect("activate", self._on_settings)
        menu.append(settings_item)
        menu.append(Gtk.SeparatorMenuItem())

        exit_item = Gtk.MenuItem(label="Exit")
        exit_item.connect("activate", self._on_exit)
        menu.append(exit_item)

        menu.show_all()
        self.indicator.set_menu(menu)

    def _on_tick(self):
        if not self.settings.enabled or not self.idle_monitor.available:
            return True

        idle_seconds = self.idle_monitor.get_idle_seconds()
        if idle_seconds is None:
            return True

        threshold = self.settings.idle_minutes * 60

        if not self._was_idle and idle_seconds >= threshold:
            self._was_idle = True
            self._mute()
        elif self._was_idle and idle_seconds < threshold:
            self._was_idle = False
            self._unmute()

        return True

    def _mute(self):
        try:
            self.mute_controller.mute_all()
            self._update_visual_state()
            _notify("MicSentry", f"Mic muted — idle for {self.settings.idle_minutes} min.")
        except Exception:
            _notify("MicSentry", "Couldn't mute your mic — check your audio setup.")

    def _unmute(self):
        if not self.mute_controller.is_app_muted:
            return
        try:
            self.mute_controller.unmute_all()
            self._update_visual_state()
            _notify("MicSentry", "Mic unmuted — welcome back.")
        except Exception:
            _notify("MicSentry", "Couldn't unmute your mic — check your audio setup.")

    def _on_toggle_enabled(self, widget):
        self.settings.enabled = widget.get_active()
        self.settings.save()

        if not self.settings.enabled:
            self._was_idle = False
            if self.mute_controller.is_app_muted:
                try:
                    self.mute_controller.unmute_all()
                except Exception:
                    pass

        self._update_visual_state()

    def _on_settings(self, widget):
        dialog = Gtk.Dialog(title="MicSentry Settings", flags=0)
        dialog.add_buttons(
            Gtk.STOCK_CANCEL, Gtk.ResponseType.CANCEL, Gtk.STOCK_SAVE, Gtk.ResponseType.OK
        )
        try:
            dialog.set_icon_from_file(os.path.join(_ICON_DIR, "state-active.png"))
        except Exception:
            pass

        box = dialog.get_content_area()
        box.set_spacing(10)
        box.set_border_width(12)

        row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
        row.pack_start(Gtk.Label(label="Mute after idle (minutes):"), False, False, 0)
        idle_spin = Gtk.SpinButton.new_with_range(1, 60, 1)
        idle_spin.set_value(self.settings.idle_minutes)
        row.pack_start(idle_spin, False, False, 0)
        box.add(row)

        startup_check = Gtk.CheckButton(label="Start with login")
        startup_check.set_active(self.settings.start_with_login)
        box.add(startup_check)

        update_check = Gtk.CheckButton(label="Check for updates on launch (contacts GitHub)")
        update_check.set_active(self.settings.check_for_updates)
        box.add(update_check)

        dialog.show_all()
        response = dialog.run()

        if response == Gtk.ResponseType.OK:
            self.settings.idle_minutes = int(idle_spin.get_value())
            self.settings.start_with_login = startup_check.get_active()
            self.settings.check_for_updates = update_check.get_active()
            self.settings.save()
            self._set_autostart(self.settings.start_with_login)
            self._update_visual_state()

        dialog.destroy()

    def _set_autostart(self, enabled):
        try:
            if enabled:
                os.makedirs(os.path.dirname(_AUTOSTART_PATH), exist_ok=True)
                with open(_AUTOSTART_PATH, "w") as f:
                    f.write(
                        "[Desktop Entry]\n"
                        "Type=Application\n"
                        "Name=MicSentry\n"
                        f'Exec=python3 "{_ENTRY_SCRIPT}"\n'
                        "Terminal=false\n"
                        "X-GNOME-Autostart-enabled=true\n"
                    )
            elif os.path.exists(_AUTOSTART_PATH):
                os.remove(_AUTOSTART_PATH)
        except Exception:
            pass

    def _on_exit(self, widget):
        if self.mute_controller.is_app_muted:
            try:
                self.mute_controller.unmute_all()
            except Exception:
                pass
        Gtk.main_quit()

    def _update_visual_state(self):
        if not self.settings.enabled:
            icon, status = "state-disabled.png", "Disabled"
        elif not self.idle_monitor.available:
            icon, status = "state-disabled.png", "Idle detection unavailable"
        elif self.mute_controller.is_app_muted:
            icon, status = "state-muted.png", "Muted (idle)"
        else:
            icon, status = "state-active.png", f"Watching (mutes after {self.settings.idle_minutes} min idle)"

        self.indicator.set_icon_full(os.path.join(_ICON_DIR, icon), status)
        self.status_item.set_label("Status: " + status)
        try:
            self.indicator.set_title("MicSentry — " + status)
        except Exception:
            pass


def main():
    _set_process_name("micsentry")
    os.makedirs(SETTINGS_DIR, exist_ok=True)
    lock_file = open(_LOCK_PATH, "w")
    try:
        fcntl.flock(lock_file, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        print("MicSentry is already running.")
        return

    TrayApp()
    Gtk.main()
