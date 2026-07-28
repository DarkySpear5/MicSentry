"""Mutes every real microphone input via pactl (PulseAudio, or PipeWire's
pulse-compat layer — covers the large majority of Linux desktops).

Same design as the Windows build: mutes every active source, remembers each
source's own prior mute state so it never restores something that was
already muted for an unrelated reason, and only ever touches the mute flag
— never volume, never default-device assignment.
"""

import subprocess

_TIMEOUT = 3


class MicMuteController:
    def __init__(self):
        self._pre_mute_state = {}
        self.is_app_muted = False

    def mute_all(self):
        if self.is_app_muted:
            return

        for source in self._list_sources():
            try:
                was_muted = self._get_mute(source)
                self._pre_mute_state[source] = was_muted
                if not was_muted:
                    self._set_mute(source, True)
            except Exception:
                # a source may have disappeared mid-loop (e.g. USB mic unplugged) — skip it
                continue

        self.is_app_muted = True

    def unmute_all(self):
        if not self.is_app_muted:
            return

        for source in self._list_sources():
            was_muted = self._pre_mute_state.get(source)
            if was_muted is False:
                try:
                    self._set_mute(source, False)
                except Exception:
                    continue

        self._pre_mute_state.clear()
        self.is_app_muted = False

    def _list_sources(self):
        out = subprocess.run(
            ["pactl", "list", "short", "sources"],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT,
            check=True,
        )
        sources = []
        for line in out.stdout.strip().splitlines():
            if not line:
                continue
            parts = line.split("\t")
            if len(parts) >= 2:
                name = parts[1]
                # ".monitor" sources are loopbacks of output devices (for
                # things like screen-recording audio), not real microphones
                if not name.endswith(".monitor"):
                    sources.append(name)
        return sources

    def _get_mute(self, source):
        out = subprocess.run(
            ["pactl", "get-source-mute", source],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT,
            check=True,
        )
        return "yes" in out.stdout.lower()

    def _set_mute(self, source, muted):
        subprocess.run(
            ["pactl", "set-source-mute", source, "1" if muted else "0"],
            timeout=_TIMEOUT,
            check=True,
        )
