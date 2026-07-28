#!/usr/bin/env bash
# Installs MicSentry entirely under your home directory — never touches
# system paths, never asks for sudo, never runs anything as root.
set -euo pipefail

INSTALL_DIR="$HOME/.local/share/micsentry"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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

echo "Installing to $INSTALL_DIR ..."
mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$DESKTOP_DIR" "$HOME/.config/micsentry"

cp "$SCRIPT_DIR"/*.py "$INSTALL_DIR/"
cp -r "$SCRIPT_DIR/icons" "$INSTALL_DIR/"

cat > "$BIN_DIR/micsentry" <<EOF
#!/usr/bin/env bash
exec python3 "$INSTALL_DIR/micsentry.py" "\$@"
EOF
chmod +x "$BIN_DIR/micsentry"

cat > "$DESKTOP_DIR/micsentry.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MicSentry
Comment=Mutes your mic when you're idle, unmutes when you're back
Exec=$BIN_DIR/micsentry
Icon=$INSTALL_DIR/icons/state-active.png
Terminal=false
Categories=Utility;
EOF

echo ""
echo "Installed. Launch it with:  micsentry"
echo "(make sure $BIN_DIR is on your PATH — add 'export PATH=\"\$HOME/.local/bin:\$PATH\"' to your shell rc file if not)"
echo "It also shows up in your application launcher as \"MicSentry\"."
echo ""
echo "NOTE for GNOME users: stock GNOME Shell hides tray icons by default."
echo "Install the \"AppIndicator and KStatusNotifierItem Support\" extension to see it:"
echo "https://extensions.gnome.org/extension/615/appindicator-support/"
echo ""
echo "To uninstall later, run uninstall.sh from this same folder."
