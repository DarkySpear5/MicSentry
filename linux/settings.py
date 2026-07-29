"""Plain JSON settings, mirroring the Windows build's %AppData%\\MicSentry\\settings.json."""

import json
import os

SETTINGS_DIR = os.path.expanduser("~/.config/micsentry")
SETTINGS_PATH = os.path.join(SETTINGS_DIR, "settings.json")

DEFAULTS = {
    "enabled": True,
    "idle_minutes": 5,
    "start_with_login": False,
    # Off by default — this is the one deliberate exception to "zero network
    # calls," so it stays opt-in rather than silently phoning home out of
    # the box. See update_checker.py.
    "check_for_updates": False,
    # Source names to never mute, even though they're active inputs — empty
    # by default, which keeps the original "mute everything" behaviour for
    # anyone who never opens the Devices menu.
    "excluded_devices": [],
}


class AppSettings:
    def __init__(self, data=None):
        data = data or {}
        self.enabled = bool(data.get("enabled", DEFAULTS["enabled"]))
        self.idle_minutes = int(data.get("idle_minutes", DEFAULTS["idle_minutes"]))
        self.start_with_login = bool(data.get("start_with_login", DEFAULTS["start_with_login"]))
        self.check_for_updates = bool(data.get("check_for_updates", DEFAULTS["check_for_updates"]))
        self.excluded_devices = list(data.get("excluded_devices", DEFAULTS["excluded_devices"]))

    @classmethod
    def load(cls):
        try:
            with open(SETTINGS_PATH, "r") as f:
                return cls(json.load(f))
        except Exception:
            # missing or corrupt settings file — fall back to defaults rather than crash
            return cls()

    def save(self):
        os.makedirs(SETTINGS_DIR, exist_ok=True)
        with open(SETTINGS_PATH, "w") as f:
            json.dump(
                {
                    "enabled": self.enabled,
                    "idle_minutes": self.idle_minutes,
                    "start_with_login": self.start_with_login,
                    "check_for_updates": self.check_for_updates,
                    "excluded_devices": self.excluded_devices,
                },
                f,
                indent=2,
            )
