# Sequenced Keys - Timberborn Mod

Navigate Timberborn's entire bottom toolbar using only your keyboard — no mouse needed. Press an entry hotkey, then use a small set of keys to drill down through button groups until you reach the building or action you want.

## How It Works

1. Press the **activation hotkey** (default: `B`) to enter sequenced build mode
2. The toolbar buttons light up with key-hint badges showing which key selects which button (or group of buttons)
3. Press a **selection key** to drill down:
   - If it maps to a single button, that button is clicked
   - If it maps to a group, the group subdivides further — press again to narrow down
4. If the selected button opens a submenu, the new buttons are scanned and the process repeats
5. Press **Escape**, **right-click**, or **B** again to exit

The buttons on the left side of the toolbar get single-key shortcuts first, since they tend to be used more often. Hexagonal monument icons are supported alongside regular buttons.

## Visual Feedback

- **Key-hint badges** appear over each button showing which key activates it
- A **status bar** at the bottom shows the current key-sequence breadcrumb and how to exit

## Keybindings

All keys are rebindable in the game's keybinding settings under the **Sequenced Keys** group.

| Action | Default Key |
|--------|-------------|
| Activate | `B` |
| Selection keys | `Q` `W` `E` `R` `A` `S` `D` `F` `Z` `X` `C` `V` `1` `2` `3` `4` `5` `T` `G` |

While sequenced mode is active, conflicting game bindings (such as game speed on `1`/`2`/`3` or transparency on `T`) are automatically suppressed so your selection keys work without interference.

You can bind fewer than the full set of keys — but expect more menu-subdivision keypresses when you do (minimum 2 selection keys).

## Adding or Removing Selection Keys

The mod automatically detects all registered selection keys at startup. To change how many are available, add or remove `KeyBindingSpec` blueprint files in `Data/Blueprints/KeyBindings/`, following the naming pattern `SequencedKeysSelect<N>.KeyBindingSpec.blueprint.json` (and add a matching localization entry in `Data/Localizations/enUS.csv`).

## Compatibility

Currently incompatible with **Moddable Tool Groups / MoreGroups**.

## Building

This mod is built with the official Timberborn modding tools (the **Unity Mod Builder**). See Mechanistry's modding documentation and Unity setup guide:

- Modding tools and examples: https://github.com/mechanistry/timberborn-modding
- Wiki: https://github.com/mechanistry/timberborn-modding/wiki

Open the mod project in the Unity editor configured per the Unity setup guide, then use the in-editor **Mod Builder** to compile the scripts and package the assets. The builder copies the compiled assembly together with the `Data/` contents (Blueprints, Localizations) and `manifest.json` into your mods folder.

## Debugging

The mod outputs log messages prefixed with `[SequencedKeys]`. To view them, open the Unity player log and search for that prefix:

- **Windows**: `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`
- **Linux**: `~/.config/unity3d/Mechanistry/Timberborn/Player.log`
- **macOS**: `~/Library/Logs/Mechanistry/Timberborn/Player.log`

## Source & Feedback

Source: https://github.com/internetcoffeephone/timberborn-modding/tree/main/SequencedKeys

For bug reports or feature requests, please use the Issues tab on GitHub.
</content>
</invoke>
