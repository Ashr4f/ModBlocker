using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace ModBlocker
{
    /// <summary>
    /// BepInEx 5 preloader patcher that prevents blocklisted mods from loading.
    ///
    /// Reads BepInEx/config/modblocker.cfg and renames the matching plugin DLLs to
    /// ".blocked" BEFORE the chainloader scans them. Removing an entry re-enables
    /// the mod on the next launch. Nothing is ever deleted.
    ///
    /// Config format (editable in-game through the companion plugin, F1):
    ///   [Blocklist]
    ///   Mods = Author-ModName, SomePlugin.dll
    ///
    /// Bare lines (one entry per line) are also accepted for backward compatibility.
    /// Matching is case-insensitive and whitespace-tolerant.
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
                bool blockFolder = blocklist.Contains(Normalize(Path.GetFileName(dir)));
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    Apply(file, blockFolder || IsDllMatch(file, blocklist));
            }

            // Manual installs: loose DLLs at the root of plugins/
            foreach (string file in Directory.GetFiles(plugins))
                Apply(file, IsDllMatch(file, blocklist));
        }

        /// <summary>Lowercase + trim, so entries match 100% regardless of case or stray spaces.</summary>
        private static string Normalize(string s)
        {
            return (s ?? "").Trim().ToLowerInvariant();
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

        private static bool IsDllMatch(string file, List<string> blocklist)
        {
            string dll = Path.GetFileName(file);
            if (dll.EndsWith(".blocked", StringComparison.OrdinalIgnoreCase))
                dll = dll.Substring(0, dll.Length - ".blocked".Length);
            return blocklist.Contains(Normalize(dll));
        }

        private static List<string> LoadBlocklist(string path)
        {
            var list = new List<string>();
            if (!File.Exists(path))
            {
                // BepInEx-style layout so the r2modman Config Editor renders it
                // as a proper section with a description and an editable field.
                File.WriteAllLines(path, new[]
                {
                    "## Settings file was created by plugin ModBlocker",
                    "## Plugin GUID: modblocker",
                    "",
                    "[Blocklist]",
                    "",
                    "## Comma-separated list of mods to block at the NEXT launch.",
                    "## Mod manager folder names (Author-ModName) or DLL file names (SomePlugin.dll). Case-insensitive.",
                    "## Example: Marlthon-Cats, SomeOldPlugin.dll",
                    "# Setting type: String",
                    "# Default value: ",
                    "Mods = ",
                });
                return list;
            }
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;

                if (line.IndexOf('=') >= 0)
                {
                    // "Mods = a, b, c" (BepInEx config entry written by the companion plugin)
                    string key = line.Substring(0, line.IndexOf('=')).Trim();
                    if (!key.Equals("Mods", StringComparison.OrdinalIgnoreCase)) continue;
                    string value = line.Substring(line.IndexOf('=') + 1);
                    foreach (string part in value.Split(','))
                    {
                        string entry = Normalize(part);
                        if (entry.Length > 0 && !list.Contains(entry)) list.Add(entry);
                    }
                }
                else
                {
                    // Legacy format: one bare entry per line
                    string entry = Normalize(line);
                    if (entry.Length > 0 && !list.Contains(entry)) list.Add(entry);
                }
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
