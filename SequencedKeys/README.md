# Sequenced Keys - Timberborn Mod

Navigate toolbar menus using keyboard sequences. Press an entry hotkey, then use a small set of keys to drill down through button groups until you reach the building or action you want — no mouse clicking required.

## How It Works

1. Press the **activation hotkey** (default: `B`) to enter sequenced build mode
2. The bottom toolbar buttons are divided into groups, each labeled with a key hint badge
3. Press one of the **selection keys** (default: `Q W E R A S D F Z X C V`) to pick a group
4. If the group contains multiple buttons, they subdivide further — repeat step 3
5. If only one button remains, it is activated automatically
6. If the button opens a submenu, the new buttons are scanned and the process repeats
7. Press `G` at any time to cancel

## Visual Feedback

- **Key hint badges** appear over each button showing which key activates it
- A **status bar** at the bottom shows the current navigation breadcrumb and how to cancel

## Keybindings

All keys are rebindable in the game's keybinding settings under the "Sequenced Keys" group:

| Action | Default Key | Description |
|--------|-------------|-------------|
| Activate | `B` | Enter sequenced build mode |
| Select 1 | `Q` | Choose group 1 |
| Select 2 | `W` | Choose group 2 |
| Select 3 | `E` | Choose group 3 |
| Select 4 | `R` | Choose group 4 |
| Select 5 | `A` | Choose group 5 |
| Select 6 | `S` | Choose group 6 |
| Select 7 | `D` | Choose group 7 |
| Select 8 | `F` | Choose group 8 |
| Select 9 | `Z` | Choose group 9 |
| Select 10 | `X` | Choose group 10 |
| Select 11 | `C` | Choose group 11 |
| Select 12 | `V` | Choose group 12 |
| Cancel | `G` | Exit sequenced build mode |

## Adding or Removing Selection Keys

To add more selection keys, create additional `KeyBindingSpec` JSON files in the `Blueprints/KeyBindings/` folder following the naming pattern `SequencedKeysSelect13.KeyBindingSpec.json`, etc. The mod automatically detects all registered selection keys at startup.

You can also reduce to fewer keys (minimum 2) by removing spec files.

## Debugging

The mod outputs verbose log messages prefixed with `[SequencedKeys]`. To view them:

1. Launch Timberborn
2. Open the Unity log file:
   - **Windows**: `%APPDATA%\..\LocalLow\Mechanistry\Timberborn\Player.log`
   - **Linux**: `~/.config/unity3d/Mechanistry/Timberborn/Player.log`
   - **macOS**: `~/Library/Logs/Mechanistry/Timberborn/Player.log`
3. Search for `[SequencedKeys]` to find all mod log entries

Key log messages to look for:
- `Configurator.Configure() called` — confirms the DLL was loaded and DI is running
- `Service constructor called` — confirms the service was instantiated
- `Load() called` — confirms the singleton lifecycle started
- `Activate binding found` — confirms the keybinding was registered correctly
- `ProcessInput() is alive` — confirms input processing is active (logged after ~60 frames)
- `Activation key pressed!` — confirms the entry hotkey was detected
- `Scan found N visible button(s)` — confirms toolbar scanning is working

## Building

Set `GameManagedPath` to your Timberborn managed assemblies folder:

```bash
dotnet build -p:GameManagedPath="/path/to/Timberborn/Timberborn_Data/Managed"
```

Then copy the output DLL and the `Blueprints/`, `Localizations/`, and `manifest.json` to your mod folder at `Documents/Timberborn/Mods/SequencedKeys/`.
