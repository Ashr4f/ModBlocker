using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace ModBlocker
{
    /// <summary>
    /// BepInEx 5 preloader patcher that prevents blocklisted mods from loading.
    ///
    /// Reads BepInEx/config/modblocker.cfg (one entry per line) and renames the
    /// matching plugin DLLs to ".blocked" BEFORE the chainloader scans them.
    /// Removing an entry re-enables the mod on the next launch.
    ///
    /// Entries can be either:
    ///   - a mod manager folder name:  Author-ModName   (r2modman / Thunderstore Mod Manager)
    ///   - a plain DLL file name:      SomePlugin.dll   (manual installs)
    ///
    /// Designed for modpack maintainers: ship this patcher and its config file in
    /// your modpack, and you can remotely disable a mod for every player by adding
    /// one line to the config and publishing a pack update.
    /// </summary>
    public static class Patcher
    {
        // BepInEx 5 preloader contract - we do not patch any game assembly.
        public static IEnumerable<string> TargetDLLs { get { yield break; } }
        public static void Patch(AssemblyDefinition assembly) { }

        private static readonly string BepInExRoot =
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Patcher).Assembly.Location) ?? ".", ".."));
        private static readonly string LogFile = Path.Combine(BepInExRoot, "ModBlocker.log");

        // Called by the preloader before plugins are loaded.
        public static void Initialize()
        {
            try { File.WriteAllText(LogFile, ""); Run(); }
            catch (Exception e) { Log("Fatal error: " + e); }
        }

        private static void Run()
        {
            string cfgPath = Path.Combine(BepInExRoot, "config", "modblocker.cfg");
            List<string> blocklist = LoadBlocklist(cfgPath);
            Log("Blocklist (" + blocklist.Count + "): " + string.Join(", ", blocklist.ToArray()));

            string plugins = Path.Combine(BepInExRoot, "plugins");
            if (!Directory.Exists(plugins)) { Log("Plugins folder not found: " + plugins); return; }

            // Mod-manager layout: plugins/Author-ModName/
            foreach (string dir in Directory.GetDirectories(plugins))
            {
                string folderName = Path.GetFileName(dir);
                bool blockFolder = Matches(folderName, blocklist);

                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    Apply(file, blockFolder || IsDllMatch(file, blocklist));
            }

            // Manual installs: loose DLLs at the root of plugins/
            foreach (string file in Directory.GetFiles(plugins))
                Apply(file, IsDllMatch(file, blocklist));
        }

        private static void Apply(string file, bool block)
        {
            if (block && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                TryMove(file, file + ".blocked", "Blocked");
            }
            else if (!block && file.EndsWith(".dll.blocked", StringComparison.OrdinalIgnoreCase))
            {
                string original = file.Substring(0, file.Length - ".blocked".Length);
                if (!File.Exists(original))
                    TryMove(file, original, "Re-enabled");
            }
        }

        private static bool Matches(string name, List<string> blocklist)
        {
            foreach (string entry in blocklist)
                if (name.Equals(entry, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsDllMatch(string file, List<string> blocklist)
        {
            string dll = Path.GetFileName(file);
            string dllNoExt = dll.EndsWith(".blocked", StringComparison.OrdinalIgnoreCase)
                ? dll.Substring(0, dll.Length - ".blocked".Length) : dll;
            foreach (string entry in blocklist)
                if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && dllNoExt.Equals(entry, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static List<string> LoadBlocklist(string path)
        {
            var list = new List<string>();
            if (!File.Exists(path))
            {
                File.WriteAllLines(path, new[]
                {
                    "## ModBlocker - mods that must NOT load.",
                    "## One entry per line. Either a mod manager folder name (Author-ModName)",
                    "## or a plain DLL file name (SomePlugin.dll). Examples:",
                    "## Marlthon-Cats",
                    "## SomeOldPlugin.dll",
                    "## Remove a line to re-enable the mod on the next launch.",
                    "[Blocklist]",
                });
                return list;
            }
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;
                list.Add(line);
            }
            return list;
        }

        private static void TryMove(string from, string to, string action)
        {
            try { File.Move(from, to); Log(action + ": " + Path.GetFileName(to)); }
            catch (Exception e) { Log("Could not rename " + from + ": " + e.Message); }
        }

        private static void Log(string msg)
        {
            string line = "[ModBlocker] " + msg;
            Console.WriteLine(line);
            try { File.AppendAllText(LogFile, line + Environment.NewLine); } catch { }
        }
    }
}
