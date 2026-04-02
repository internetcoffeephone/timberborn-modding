# Sequenced Keys - Timberborn Mod

Navigate toolbar menus using keyboard sequences. Press an entry hotkey, then use a small set of keys to drill down through button groups until you reach the building or action you want — no mouse clicking required.

## How It Works

1. Press the **activation hotkey** (default: `` ` `` backtick/tilde) to enter sequenced build mode
2. The bottom toolbar buttons are divided into groups, each labeled with a key hint badge
3. Press one of the **selection keys** (default: `Q`, `W`, `E`, `R`) to pick a group
4. If the group contains multiple buttons, they subdivide further — repeat step 3
5. If only one button remains, it is activated automatically
6. If the button opens a submenu, the new buttons are scanned and the process repeats
7. Press **Escape** at any time to cancel

## Visual Feedback

- **Key hint badges** appear over each button showing which key activates it
- A **status bar** at the bottom shows the current navigation breadcrumb and how to cancel

## Keybindings

All keys are rebindable in the game's keybinding settings under the "Sequenced Keys" group:

| Action | Default Key | Description |
|--------|-------------|-------------|
| Activate | `` ` `` | Enter sequenced build mode |
| Select 1 | `Q` | Choose the first group |
| Select 2 | `W` | Choose the second group |
| Select 3 | `E` | Choose the third group |
| Select 4 | `R` | Choose the fourth group |
| Cancel | `Escape` | Exit sequenced build mode |

## Adding More Selection Keys

To use more than 4 selection keys, add additional `KeyBindingSpec` JSON files in the `Blueprints/KeyBindings/` folder following the naming pattern `SequencedKeysSelect5.KeyBindingSpec.json`, etc. The mod automatically detects all registered selection keys at startup.

You can also reduce to fewer keys (minimum 2) by removing spec files.

## Building

Set `GameManagedPath` to your Timberborn managed assemblies folder:

```bash
dotnet build -p:GameManagedPath="/path/to/Timberborn/Timberborn_Data/Managed"
```

Then copy the output DLL and the `Blueprints/`, `Localizations/`, and `manifest.json` to your mod folder at `Documents/Timberborn/Mods/SequencedKeys/`.
