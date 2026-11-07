#!/bin/bash
set -e

echo "=============================================="
echo "    Compiling Skyblox Launcher for macOS"
echo "=============================================="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCH="$(uname -m)"

# Choose RID based on Mac architecture
if [ "$ARCH" = "arm64" ]; then
    RID="osx-arm64"
else
    RID="osx-x64"
fi

PUBLISH_DIR="$SCRIPT_DIR/publish/$RID"
APP_DIR="$SCRIPT_DIR/publish/SkybloxLauncher.app"

echo "Building self-contained binary for $RID..."
dotnet publish "$SCRIPT_DIR/SkybloxLauncher.csproj" -c Release -r "$RID" --self-contained true -o "$PUBLISH_DIR"

echo "Creating SkybloxLauncher.app bundle..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

cp -R "$PUBLISH_DIR/"* "$APP_DIR/Contents/MacOS/"
chmod +x "$APP_DIR/Contents/MacOS/SkybloxLauncher"

if [ -f "$SCRIPT_DIR/Skyblox_logo.png" ]; then
    cp "$SCRIPT_DIR/Skyblox_logo.png" "$APP_DIR/Contents/Resources/"
fi

cat << 'EOF' > "$APP_DIR/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>SkybloxLauncher</string>
    <key>CFBundleIdentifier</key>
    <string>co.skyblox.launcher</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>SkybloxLauncher</string>
    <key>CFBundleDisplayName</key>
    <string>Skyblox Launcher</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.6</string>
    <key>CFBundleVersion</key>
    <string>1.0.6</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>CFBundleURLTypes</key>
    <array>
        <dict>
            <key>CFBundleURLName</key>
            <string>Skyblox Protocol</string>
            <key>CFBundleURLSchemes</key>
            <array>
                <string>sclient</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
EOF

echo "APPL????" > "$APP_DIR/Contents/PkgInfo"

echo "=============================================="
echo "Successfully built SkybloxLauncher.app at:"
echo "$APP_DIR"
echo "=============================================="
