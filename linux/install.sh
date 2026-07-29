#!/usr/bin/env bash
# Installs MicSentry entirely under your home directory — never touches
# system paths, never asks for sudo, never runs anything as root.
set -euo pipefail

INSTALL_DIR="$HOME/.local/share/micsentry"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Respects a localized/redirected Desktop folder (xdg-user-dirs) if present,
# falls back to the plain ~/Desktop convention if that tool isn't installed.
if command -v xdg-user-dir >/dev/null 2>&1; then
    USER_DESKTOP_DIR="$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")"
else
    USER_DESKTOP_DIR="$HOME/Desktop"
fi

echo "Checking dependencies..."
missing=()
command -v python3 >/dev/null 2>&1 || missing+=("python3")
command -v pactl >/dev/null 2>&1 || missing+=("pactl")
python3 - <<'EOF' >/dev/null 2>&1 || missing+=("python3-gi (PyGObject) + GTK3")
import gi
gi.require_version("Gtk", "3.0")
from gi.repository import Gtk
EOF
python3 -c "import Xlib" >/dev/null 2>&1 || missing+=("python3-xlib")

if [ ${#missing[@]} -ne 0 ]; then
    echo ""
    echo "Missing dependencies:"
    for m in "${missing[@]}"; do echo "  - $m"; done
    echo ""
    echo "Install the relevant packages for your distro, then re-run this script:"
    echo ""
    echo "  Debian/Ubuntu:  sudo apt install python3-gi gir1.2-ayatanaappindicator3-0.1 gir1.2-notify-0.7 python3-xlib pulseaudio-utils"
    echo "  Fedora:         sudo dnf install python3-gobject libappindicator-gtk3 python3-xlib pulseaudio-utils"
    echo "  Arch:           sudo pacman -S python-gobject libayatana-appindicator python-xlib libpulse libnotify"
    echo ""
    exit 1
fi

WAS_RUNNING=0
if pgrep -f "python3.*micsentry\.py" >/dev/null 2>&1; then
    echo "MicSentry is currently running — stopping it so the update actually takes effect"
    echo "(overwriting the files on disk doesn't change code already loaded into a running process)..."
    pkill -f "python3.*micsentry\.py" 2>/dev/null || true
    sleep 1
    WAS_RUNNING=1
fi

echo "Installing to $INSTALL_DIR ..."
mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$DESKTOP_DIR" "$HOME/.config/micsentry"

cp "$SCRIPT_DIR"/*.py "$INSTALL_DIR/"
cp -r "$SCRIPT_DIR/icons" "$INSTALL_DIR/"

cat > "$BIN_DIR/micsentry" <<EOF
#!/usr/bin/env bash
exec python3 "$INSTALL_DIR/micsentry.py" "\$@"
EOF
chmod +x "$BIN_DIR/micsentry"

DESKTOP_ENTRY_CONTENTS="[Desktop Entry]
Type=Application
Name=MicSentry
Comment=Mutes your mic when you're idle, unmutes when you're back
Exec=$BIN_DIR/micsentry
Icon=$INSTALL_DIR/icons/state-active.png
Terminal=false
Categories=Utility;
"

echo "$DESKTOP_ENTRY_CONTENTS" > "$DESKTOP_DIR/micsentry.desktop"
chmod +x "$DESKTOP_DIR/micsentry.desktop"

if mkdir -p "$USER_DESKTOP_DIR" 2>/dev/null; then
    echo "$DESKTOP_ENTRY_CONTENTS" > "$USER_DESKTOP_DIR/micsentry.desktop"
    chmod +x "$USER_DESKTOP_DIR/micsentry.desktop"
fi

echo ""
if [ "$WAS_RUNNING" -eq 1 ]; then
    echo "Updated — restarting MicSentry with the new version now..."
    nohup "$BIN_DIR/micsentry" >/dev/null 2>&1 &
    disown
    echo "Done. Right-click the tray icon to confirm the new version's changes are there."
else
    echo "Installed. Launch it with:  micsentry"
fi
echo "(make sure $BIN_DIR is on your PATH — add 'export PATH=\"\$HOME/.local/bin:\$PATH\"' to your shell rc file if not)"
echo "It also shows up in your application launcher, and as an icon on your Desktop, as \"MicSentry\"."
echo "(Some file managers — GNOME Files/Nautilus in particular — show a new Desktop icon as untrusted the"
echo "first time; right-click it and choose \"Allow Launching\" if double-clicking doesn't work right away.)"
echo ""
echo "NOTE for GNOME users: stock GNOME Shell hides tray icons by default."
echo "Install the \"AppIndicator and KStatusNotifierItem Support\" extension to see it:"
echo "https://extensions.gnome.org/extension/615/appindicator-support/"
echo ""
echo "To uninstall later, run uninstall.sh from this same folder."
