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
}


class AppSettings:
    def __init__(self, data=None):
        data = data or {}
        self.enabled = bool(data.get("enabled", DEFAULTS["enabled"]))
        self.idle_minutes = int(data.get("idle_minutes", DEFAULTS["idle_minutes"]))
        self.start_with_login = bool(data.get("start_with_login", DEFAULTS["start_with_login"]))
        self.check_for_updates = bool(data.get("check_for_updates", DEFAULTS["check_for_updates"]))

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
                },
                f,
                indent=2,
            )
