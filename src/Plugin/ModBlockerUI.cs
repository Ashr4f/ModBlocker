using BepInEx;
using BepInEx.Configuration;

namespace ModBlocker
{
    /// <summary>
    /// Companion plugin for the ModBlocker preloader patcher.
    ///
    /// Its only job is to expose the blocklist as an editable field in the standard
    /// BepInEx ConfigurationManager window (F1). The plugin GUID is "modblocker",
    /// so BepInEx stores its config in BepInEx/config/modblocker.cfg - the exact
    /// file the preloader patcher reads at startup. One file, one source of truth.
    ///
    /// Changes made in-game are saved immediately and take effect on the NEXT
    /// launch (blocking happens before plugins load, by design).
    /// </summary>
    [BepInPlugin("modblocker", "ModBlocker", "1.1.0")]
    public class ModBlockerUI : BaseUnityPlugin
    {
        private ConfigEntry<string> _mods;

        private void Awake()
        {
            _mods = Config.Bind(
                "Blocklist",
                "Mods",
                "",
                "Comma-separated list of mods to block at the NEXT launch. " +
                "Use mod manager folder names (Author-ModName) or DLL file names (SomePlugin.dll). " +
                "Case-insensitive. Example: Marlthon-Cats, SomeOldPlugin.dll");

            _mods.SettingChanged += (_, __) =>
                Logger.LogInfo("Blocklist updated (takes effect on next launch): " + _mods.Value);

            Logger.LogInfo("Current blocklist: " + (_mods.Value.Trim().Length == 0 ? "(empty)" : _mods.Value));
        }
    }
}
