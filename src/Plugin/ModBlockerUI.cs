using System.IO;
using BepInEx;
using BepInEx.Configuration;
using ServerSync;

namespace ModBlocker
{
    /// <summary>
    /// Companion plugin for the ModBlocker preloader patcher.
    ///
    /// v1.3.0 adds server enforcement via ServerSync (by blaxxun):
    ///  - When installed on a server (or in-game host), the server's blocklist is
    ///    pushed to every connecting client and applies on their NEXT launch.
    ///  - ModRequired: players WITHOUT ModBlocker installed are kicked on connect.
    ///
    /// ServerSync intentionally does not persist server values into the local
    /// config file, so this plugin mirrors the effective blocklist into
    /// BepInEx/config/modblocker.server - which the preloader patcher merges
    /// with the local blocklist at startup.
    /// </summary>
    [BepInPlugin("modblocker", "ModBlocker", "1.3.0")]
    public class ModBlockerUI : BaseUnityPlugin
    {
        private static readonly ConfigSync configSync = new ConfigSync("modblocker")
        {
            DisplayName = "ModBlocker",
            CurrentVersion = "1.3.0",
            MinimumRequiredVersion = "1.3.0",
            ModRequired = true, // kick players who do not have ModBlocker installed
        };

        private ConfigEntry<bool> _lockConfig;
        private ConfigEntry<string> _mods;

        private void Awake()
        {
            _lockConfig = Config.Bind("Blocklist", "Lock Configuration", true,
                "Server side: if enabled, the server's blocklist overrides every client's " +
                "(applies on their next launch). Players without ModBlocker are kicked either way.");
            configSync.AddLockingConfigEntry(_lockConfig);

            _mods = Config.Bind("Blocklist", "Mods", "",
                "Comma-separated list of mods to block at the NEXT launch.\n" +
                "Use mod manager folder names (Author-ModName) or DLL file names (SomePlugin.dll).\n" +
                "Case-insensitive. Example: SomeAuthor-SomeMod, SomeOldPlugin.dll");
            configSync.AddConfigEntry(_mods);

            _mods.SettingChanged += (_, __) => OnBlocklistChanged();
            MirrorServerBlocklist();

            Logger.LogInfo("Current blocklist: " + (_mods.Value.Trim().Length == 0 ? "(empty)" : _mods.Value));
        }

        private void OnBlocklistChanged()
        {
            Config.Save();
            MirrorServerBlocklist();
            Logger.LogInfo("Blocklist updated (takes effect on next launch): " + _mods.Value);
        }

        /// <summary>Mirror the effective (possibly server-enforced) blocklist for the preloader.</summary>
        private void MirrorServerBlocklist()
        {
            try
            {
                string path = Path.Combine(Paths.ConfigPath, "modblocker.server");
                string value = _mods.Value ?? "";
                if (!File.Exists(path) || File.ReadAllText(path) != value)
                    File.WriteAllText(path, value);
            }
            catch { /* non-fatal */ }
        }
    }
}
