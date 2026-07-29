"""Optional, opt-in update check against GitHub Releases — the one
deliberate exception to MicSentry's "zero network calls" design, which is
exactly why it's gated behind a settings toggle that defaults to off (see
settings.py). Mirrors the pattern already used in the Windows/Gamut sibling
app: a single check at launch, never auto-downloads or auto-installs
anything, and stays completely silent on failure or "no update available"
— it should never nag.
"""

import json
import threading
import urllib.request

CURRENT_VERSION = "1.2.2"  # bump alongside the repo's release tags

_RELEASES_API = "https://api.github.com/repos/DarkySpear5/MicSentry/releases/latest"
RELEASES_PAGE = "https://github.com/DarkySpear5/MicSentry/releases/latest"


def check_for_updates_on_launch(on_update_available):
    """Fire-and-forget on a background thread so it can never block startup.

    Calls on_update_available(version, url) only if a genuinely newer
    release is found. Note: that callback runs on the background thread,
    not the GTK main thread — the caller is responsible for marshaling
    back with GLib.idle_add before touching any GTK/AppIndicator objects.
    """
    thread = threading.Thread(target=_check, args=(on_update_available,), daemon=True)
    thread.start()


def _check(on_update_available):
    try:
        req = urllib.request.Request(_RELEASES_API, headers={"Accept": "application/vnd.github+json"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.load(resp)

        latest_version = str(data.get("tag_name", "")).lstrip("v")
        if _is_newer(latest_version, CURRENT_VERSION):
            on_update_available(latest_version, RELEASES_PAGE)
    except Exception:
        # no internet, rate-limited, API shape changed, whatever happened —
        # this must never surface as an error or a nag, only ever a
        # positive "here's a new version" when there genuinely is one.
        pass


def _is_newer(latest, current):
    def parts(v):
        return [int(p) for p in v.split(".") if p.isdigit()]

    try:
        latest_parts, current_parts = parts(latest), parts(current)
        return bool(latest_parts) and latest_parts > current_parts
    except Exception:
        return False
