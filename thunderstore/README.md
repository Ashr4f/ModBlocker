# ModBlocker

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

## Warnings

- Do not block a library other mods depend on (e.g. Jotunn) — dependents will error.
- Blocking a content mod (items/creatures) has the same effect on an existing
  world as uninstalling it: its objects will be missing while blocked.

## Building from source

```
cd src
dotnet build -c Release
```
Patcher: `dotnet build src` → `src/bin/Release/net472/ModBlocker.dll` → `BepInEx/patchers/`
Plugin:  `dotnet build src/Plugin` → `src/Plugin/bin/Release/net472/ModBlockerUI.dll` → `BepInEx/plugins/`
