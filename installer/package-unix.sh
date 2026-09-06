#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Runtime identifier is required}"
version="${2:?Version is required}"
numeric_version="${version%%-*}"
source_dir="$PWD/publish/$rid"
mkdir -p packages
test -f "$source_dir/Sortography"
chmod +x "$source_dir/Sortography"
cp LICENSE "$source_dir/LICENSE.txt"

case "$rid" in
  osx-x64|osx-arm64)
    app="$PWD/staging/$rid/Sortography.app"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    cp -R "$source_dir/." "$app/Contents/MacOS/"
    cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>Sortography</string>
  <key>CFBundleDisplayName</key><string>Sortography</string>
  <key>CFBundleIdentifier</key><string>com.atalic2.sortography</string>
  <key>CFBundleExecutable</key><string>Sortography</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$numeric_version</string>
  <key>CFBundleVersion</key><string>$numeric_version</string>
  <key>CFBundleIconFile</key><string>Sortography.icns</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
EOF
    iconset="$PWD/staging/$rid/Sortography.iconset"
    mkdir -p "$iconset"
    for size in 16 32 128 256 512; do
      sips -z "$size" "$size" Assets/icon.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
      double=$((size * 2))
      sips -z "$double" "$double" Assets/icon.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
    done
    iconutil -c icns "$iconset" -o "$app/Contents/Resources/Sortography.icns"
    plutil -lint "$app/Contents/Info.plist"
    # Ad-hoc signing supports local execution, but is not Developer ID signing/notarization.
    codesign --force --deep --sign - "$app"
    codesign --verify --deep --strict "$app"
    disk="$PWD/staging/$rid/disk"
    mkdir -p "$disk"
    mv "$app" "$disk/"
    ln -s /Applications "$disk/Applications"
    hdiutil create -volname Sortography -srcfolder "$disk" -ov -format UDZO \
      "packages/Sortography-$version-$rid.dmg"
    hdiutil verify "packages/Sortography-$version-$rid.dmg"
    ;;
  linux-x64)
    tar -C "$source_dir" -czf "packages/Sortography-$version-$rid.tar.gz" .
    deb="$PWD/staging/$rid/deb"
    mkdir -p "$deb/DEBIAN" "$deb/opt/sortography" "$deb/usr/share/applications" \
      "$deb/usr/share/icons/hicolor/256x256/apps"
    cp -R "$source_dir/." "$deb/opt/sortography/"
    cp Assets/icon.png "$deb/usr/share/icons/hicolor/256x256/apps/sortography.png"
    cat > "$deb/usr/share/applications/sortography.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Sortography
Comment=Put every photo in its place
Exec=/opt/sortography/Sortography
Icon=sortography
Terminal=false
Categories=Graphics;Photography;
EOF
    # Debian sorts prereleases before their corresponding stable version.
    deb_version="${version/-/~}"
    cat > "$deb/DEBIAN/control" <<EOF
Package: sortography
Version: $deb_version
Architecture: amd64
Maintainer: Sortography <noreply@github.com>
Section: graphics
Priority: optional
Depends: libc6 (>= 2.38), libgcc-s1, libstdc++6, zlib1g, libssl3t64, libicu74, libx11-6, libice6, libsm6, libfontconfig1
Description: Sort photos into folders with previews and keyboard shortcuts.
EOF
    chmod 755 "$deb" "$deb/DEBIAN"
    dpkg-deb --root-owner-group --build "$deb" "packages/Sortography-$version-$rid.deb"
    dpkg-deb --info "packages/Sortography-$version-$rid.deb"
    ;;
  *) echo "Unsupported runtime: $rid" >&2; exit 1 ;;
esac
