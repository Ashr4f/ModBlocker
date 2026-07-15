# ModBlocker

[![build](https://github.com/Ashr4f/ModBlocker/actions/workflows/build.yml/badge.svg)](https://github.com/Ashr4f/ModBlocker/actions)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A BepInEx 5 preloader patcher for Valheim (and other BepInEx games) that
**prevents blocklisted mods from loading** — without uninstalling them.

## Why?

Modpack maintainers have no way to *remove* a mod from players' machines:
updating a modpack installs new dependencies but never uninstalls dropped ones.
ModBlocker fixes that. Ship it in your modpack with its config file, and
removing a mod for every player becomes a one-line config change in your next
pack update.

## How it works

At game startup — before the BepInEx chainloader scans plugins — ModBlocker
reads `BepInEx/config/modblocker.cfg` and renames the DLLs of blocklisted mods
to `.dll.blocked`. Blocked mods simply do not exist as far as the game is
concerned. Removing an entry restores the DLLs on the next launch. Nothing is
ever deleted.

## Config: `BepInEx/config/modblocker.cfg`

Comma-separated entries. Two formats are supported, matching is case-insensitive:

```
[Blocklist]
Mods = SomeAuthor-SomeMod, SomeOldPlugin.dll
```

## Editing in-game

The companion plugin **ModBlockerUI** exposes the blocklist as a text field in
the ConfigurationManager window (F1). Changes are saved instantly and applied
on the **next launch** (blocking happens before plugins load, by design).

A log of every block/re-enable action is written to `BepInEx/ModBlocker.log`.

## Safety nets

- **Core protection**: BepInEx and Jotunn can never be blocked — entries
  targeting them are refused and logged.
- **Cascade blocking**: if you block a mod that other mods depend on, those
  dependents are **automatically blocked too**, recursively (2, 3, 5 mods —
  the whole chain). Each cascade is logged as `Also blocked: X (depends on Y)`.
  Removing the original entry restores the entire chain on the next launch.
- **Self-protection**: entries targeting ModBlocker's own components are ignored.

## Warnings

- Blocking a mod that others depend on blocks the dependents as well (see
  cascade above) — check `BepInEx/ModBlocker.log` to see the full chain.
- Blocking a content mod (items/creatures) has the same effect on an existing
  world as uninstalling it: its objects will be missing while blocked.

## Building from source

```
cd src
dotnet build -c Release
```
Patcher: `dotnet build src` → `src/bin/Release/net472/ModBlocker.dll` → `BepInEx/patchers/`
Plugin:  `dotnet build src/Plugin` → `src/Plugin/bin/Release/net472/ModBlockerUI.dll` → `BepInEx/plugins/`

## FAQ

**Does blocking delete anything?** No. DLLs are renamed to `.blocked`, never deleted.
Remove the entry and they are restored on the next launch.

**Can it block itself?** No — entries targeting ModBlocker's own components are
ignored and logged.

**Does it work outside Valheim?** It should work with any BepInEx 5 game, but it
is only tested on Valheim.

## Contributing

Issues and pull requests are welcome. The CI builds both DLLs and assembles the
ready-to-upload Thunderstore package on every push (see Actions artifacts).
Pushing a `vX.Y.Z` tag creates a GitHub Release with the package attached.

## License

[MIT](LICENSE) — © 2026 Ashraf
