#!/usr/bin/env bash
# Removes everything install.sh created. Never touched anything outside
# your home directory in the first place, so there's nothing else to undo.
set -euo pipefail

pkill -f "python3.*micsentry.py" 2>/dev/null || true

if command -v xdg-user-dir >/dev/null 2>&1; then
    USER_DESKTOP_DIR="$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")"
else
    USER_DESKTOP_DIR="$HOME/Desktop"
fi

rm -rf "$HOME/.local/share/micsentry"
rm -f "$HOME/.local/bin/micsentry"
rm -f "$HOME/.local/share/applications/micsentry.desktop"
rm -f "$HOME/.config/autostart/micsentry.desktop"
rm -f "$USER_DESKTOP_DIR/micsentry.desktop"

echo "MicSentry removed."
echo "Your settings at ~/.config/micsentry were kept in case you reinstall later —"
echo "delete that folder too if you want a completely clean slate."
