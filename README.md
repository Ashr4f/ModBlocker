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

One entry per line. Two formats are supported:

```
## Mod manager folder name (r2modman / Thunderstore Mod Manager):
Marlthon-Cats
## Plain DLL file name (manual installs):
SomeOldPlugin.dll
```

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
Output: `src/bin/Release/net472/ModBlocker.dll` → drop into `BepInEx/patchers/`.
