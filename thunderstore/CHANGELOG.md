# Changelog

## 1.0.0
- Initial release: folder-name and DLL-name blocklist, automatic re-enable, action log.

## 1.1.0
- Normalized matching (lowercase + trim) for 100% reliable entries.
- New config format `Mods = a, b, c` (legacy one-per-line still supported).
- Companion plugin ModBlockerUI: edit the blocklist in-game via ConfigurationManager (F1); applies on next launch.

## 1.1.1
- Self-protection: ModBlocker ignores blocklist entries targeting its own components (and auto-restores them if previously blocked).

## 1.1.2
- Multi-line config description, neutral example in docs.

## 1.2.0
- Core protection: BepInEx and Jotunn can never be blocked.
- Cascade blocking: blocking a mod automatically blocks every mod that depends on it (recursively), logged as 'Also blocked: X (depends on Y)'. Removing the entry restores the whole chain.

## 1.3.0
- Server enforcement via ServerSync (credits: blaxxun): the server's blocklist is pushed to all clients (applies on their next launch), and players without ModBlocker are kicked on connect (ModRequired).
- The effective blocklist is mirrored to modblocker.server; the preloader merges it with the local list.
