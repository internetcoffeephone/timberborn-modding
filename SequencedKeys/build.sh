#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_NAME="SequencedKeys"

# --- Detect Timberborn install path ---
if [ -n "${GAME_PATH:-}" ]; then
    MANAGED_PATH="$GAME_PATH/Timberborn_Data/Managed"
elif [ -d "$HOME/.local/share/Steam/steamapps/common/Timberborn" ]; then
    MANAGED_PATH="$HOME/.local/share/Steam/steamapps/common/Timberborn/Timberborn_Data/Managed"
elif [ -d "$HOME/.steam/steam/steamapps/common/Timberborn" ]; then
    MANAGED_PATH="$HOME/.steam/steam/steamapps/common/Timberborn/Timberborn_Data/Managed"
else
    echo "ERROR: Could not find Timberborn installation."
    echo "Set GAME_PATH to your Timberborn install directory, e.g.:"
    echo "  GAME_PATH=/path/to/Timberborn bash $0"
    exit 1
fi

if [ ! -d "$MANAGED_PATH" ]; then
    echo "ERROR: Managed DLL directory not found at: $MANAGED_PATH"
    exit 1
fi
echo "Using game DLLs from: $MANAGED_PATH"

# --- Detect mods folder ---
if [ -n "${MODS_PATH:-}" ]; then
    MOD_DEST="$MODS_PATH/$MOD_NAME"
elif [ -d "$HOME/Documents/Timberborn/Mods" ]; then
    MOD_DEST="$HOME/Documents/Timberborn/Mods/$MOD_NAME"
elif [ -d "$HOME/.config/unity3d/Mechanistry/Timberborn" ]; then
    MOD_DEST="$HOME/.config/unity3d/Mechanistry/Timberborn/Mods/$MOD_NAME"
else
    MOD_DEST="$HOME/Documents/Timberborn/Mods/$MOD_NAME"
    echo "WARNING: Could not detect Timberborn mods folder, defaulting to: $MOD_DEST"
fi

# --- Build ---
echo "Building $MOD_NAME..."
dotnet build "$SCRIPT_DIR/$MOD_NAME.csproj" \
    -p:GameManagedPath="$MANAGED_PATH" \
    -c Release \
    --nologo

DLL_PATH="$SCRIPT_DIR/bin/Release/netstandard2.1/$MOD_NAME.dll"
if [ ! -f "$DLL_PATH" ]; then
    echo "ERROR: Build succeeded but DLL not found at: $DLL_PATH"
    exit 1
fi

# --- Copy to mods folder ---
echo "Installing to: $MOD_DEST"
mkdir -p "$MOD_DEST"

cp "$DLL_PATH" "$MOD_DEST/"
cp "$SCRIPT_DIR/manifest.json" "$MOD_DEST/"
cp -r "$SCRIPT_DIR/Data/Blueprints" "$MOD_DEST/"
cp -r "$SCRIPT_DIR/Data/Localizations" "$MOD_DEST/"

echo ""
echo "Done! Installed $MOD_NAME to $MOD_DEST"
echo "Contents:"
find "$MOD_DEST" -type f | sort | sed 's|^|  |'
