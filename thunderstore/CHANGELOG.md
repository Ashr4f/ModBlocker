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
