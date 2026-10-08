# MLCountriesMod

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod for **Broken Arrow** that lets the Arsenal show more than the two vanilla countries.

The vanilla country bar stops after two flags, so a country added by a database mod never appears in the Arsenal. MLCountriesMod draws a flag and specialization icons for every country in the loaded database. If the bar gets too wide, it is shrunk to fit.

On its own, with only the vanilla countries, the mod changes nothing. It's meant to be used alongside mods that add countries.

## Installation

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) for Broken Arrow.
2. Download `MLCountriesMod.dll` from the [Releases](../../releases) page.
3. Put it in `<Broken Arrow folder>\Mods`.

To uninstall, delete the DLL from `Mods`.

## Settings

After the first launch, the settings are in `UserData\MelonPreferences.cfg`, under `[MLCountriesMod]`:

| Setting    | Default | What it does |
|------------|---------|--------------|
| `FitBar`   | `true`  | Shrink the country bar if it is wider than the unit list. |
| `MinScale` | `0.6`   | The smallest size the bar may shrink to (0.3 – 1.0). |
| `Verbose`  | `false` | Log every country and its specializations when the Arsenal opens. |

## For country modders

If your country doesn't show up, set `Verbose = true`, open the Arsenal and look for `[MLCountriesMod]` lines in `MelonLoader\Latest.log`. Common causes:

- **`Hidden = true`** on the country: the game skips it.
- **No specialization with `ShowInHangar = true`**: the country has nothing to show.
- **`ContentMembership`** set to a DLC the player doesn't own: shown as locked.
- **Missing flag sprite** for `FlagFileName`: the flag is blank.

**Limit:** the game stores country and spec selections in one 32-bit value. Every visible country and every specialization shown in the hangar uses one bit. The two vanilla countries use 13 of the 31 available. If you go over, filtering by the last countries misbehaves, and the log warns you.

## Building from source

Requirements: the [.NET SDK](https://dotnet.microsoft.com/download) (6.0 or newer) and Broken Arrow with MelonLoader installed. Start the game once so MelonLoader generates `MelonLoader\Il2CppAssemblies`.

1. Run `build.bat` once. It creates `GamePath.props` from the example file.
2. Open `GamePath.props` and set your Broken Arrow folder.
3. Run `build.bat` again. The DLL is built and copied to `<game>\Mods`.

Instead of `GamePath.props`, you can set a `GamePath` environment variable. `GamePath.props` is ignored by git, so your local path never gets committed.

The game's assemblies are referenced from your own install and are never part of this repository.

## Credits

This mod was made with [Claude](https://claude.ai) by Anthropic.

## License

[MIT](LICENSE)

This is a fan-made mod, not affiliated with or endorsed by the developers or publishers of Broken Arrow.
