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
    /// Safety nets:
    ///  - Self-protection: ModBlocker never blocks its own components.
    ///  - Core protection: BepInEx and Jotunn can never be blocked.
    ///  - Cascade blocking: blocking a mod also blocks every mod that depends
    ///    on it (recursively), so nothing is left loading against a missing
    ///    dependency. Each cascade is logged: "Also blocked: X (depends on Y)".
    /// </summary>
    public static class Patcher
    {
        // BepInEx 5 preloader contract - we do not patch any game assembly.
        public static IEnumerable<string> TargetDLLs { get { yield break; } }
        public static void Patch(AssemblyDefinition assembly) { }

        private static readonly string BepInExRoot =
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Patcher).Assembly.Location) ?? ".", ".."));
        private static readonly string LogFile = Path.Combine(BepInExRoot, "ModBlocker.log");

        private static readonly string[] Protected = { "modblocker.dll", "modblockerui.dll" };

        // Called by the preloader before plugins are loaded.
        public static void Initialize()
        {
            try { File.WriteAllText(LogFile, ""); Run(); }
            catch (Exception e) { Log("Fatal error: " + e); }
        }

        // ------------------------------------------------------------------
        // Model: one "unit" = one mod folder (r2modman layout) or one loose DLL.
        // ------------------------------------------------------------------
        private class Unit
        {
            public string Name;
            public List<string> Files = new List<string>();   // all files of the unit
            public bool Blocked;

            // Metadata of the unit's DLLs (filled by Analyze)
            public List<string> AssemblyNames = new List<string>();
            public List<string> PluginGuids = new List<string>();
            public List<string> ReferencedAssemblies = new List<string>();
            public List<string> HardDependencyGuids = new List<string>();
        }

        private static void Run()
        {
            string cfgPath = Path.Combine(BepInExRoot, "config", "modblocker.cfg");
            List<string> blocklist = LoadBlocklist(cfgPath);
            Log("Blocklist (" + blocklist.Count + "): " + string.Join(", ", blocklist.ToArray()));

            string plugins = Path.Combine(BepInExRoot, "plugins");
            if (!Directory.Exists(plugins)) { Log("Plugins folder not found: " + plugins); return; }

            // Collect units
            var units = new List<Unit>();
            foreach (string dir in Directory.GetDirectories(plugins))
            {
                var u = new Unit { Name = Path.GetFileName(dir) };
                u.Files.AddRange(Directory.GetFiles(dir, "*", SearchOption.AllDirectories));
                u.Blocked = blocklist.Contains(Normalize(u.Name)) || AnyDllListed(u, blocklist);
                units.Add(u);
            }
            foreach (string file in Directory.GetFiles(plugins))
            {
                var u = new Unit { Name = Path.GetFileName(file) };
                u.Files.Add(file);
                u.Blocked = AnyDllListed(u, blocklist);
                units.Add(u);
            }

            // Analyze DLL metadata (assembly names, plugin GUIDs, references, hard deps)
            foreach (Unit u in units) Analyze(u);

            // Core protection: BepInEx and Jotunn can never be blocked.
            foreach (Unit u in units)
            {
                if (u.Blocked && IsCoreLibrary(u))
                {
                    u.Blocked = false;
                    Log("Refused: " + u.Name + " is a protected core library (BepInEx/Jotunn).");
                }
            }

            // Cascade blocking: every mod that depends on a blocked mod gets
            // blocked too, recursively, so nothing loads against a missing dependency.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (Unit kept in units)
                {
                    if (kept.Blocked || IsCoreLibrary(kept)) continue;
                    foreach (Unit blocked in units)
                    {
                        if (!blocked.Blocked || blocked == kept) continue;
                        string reason = Needs(kept, blocked);
                        if (reason != null)
                        {
                            kept.Blocked = true;
                            changed = true;
                            Log("Also blocked: " + kept.Name + " (depends on " + blocked.Name + ", " + reason + ")");
                            break;
                        }
                    }
                }
            }

            // Apply renames
            foreach (Unit u in units)
                foreach (string file in u.Files)
                    Apply(file, u.Blocked);
        }

        /// <summary>Does 'kept' need 'blocked'? Returns a reason string, or null.</summary>
        private static string Needs(Unit kept, Unit blocked)
        {
            foreach (string r in kept.ReferencedAssemblies)
                foreach (string a in blocked.AssemblyNames)
                    if (r.Equals(a, StringComparison.OrdinalIgnoreCase))
                        return "assembly reference " + a;
            foreach (string g in kept.HardDependencyGuids)
                foreach (string bg in blocked.PluginGuids)
                    if (g.Equals(bg, StringComparison.OrdinalIgnoreCase))
                        return "hard BepInDependency " + bg;
            return null;
        }

        /// <summary>BepInEx and Jotunn must never be blocked.</summary>
        private static bool IsCoreLibrary(Unit u)
        {
            if (Normalize(u.Name).Contains("jotunn")) return true;
            foreach (string a in u.AssemblyNames)
                if (a.Equals("Jotunn", StringComparison.OrdinalIgnoreCase)
                 || a.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (string g in u.PluginGuids)
                if (g.Equals("com.jotunn.jotunn", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Analyze(Unit u)
        {
            foreach (string file in u.Files)
            {
                string path = file;
                bool isDll = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                          || path.EndsWith(".dll.blocked", StringComparison.OrdinalIgnoreCase);
                if (!isDll) continue;
                try
                {
                    using (ModuleDefinition module = ModuleDefinition.ReadModule(path))
                    {
                        if (module.Assembly != null)
                            u.AssemblyNames.Add(module.Assembly.Name.Name);
                        foreach (AssemblyNameReference r in module.AssemblyReferences)
                            u.ReferencedAssemblies.Add(r.Name);
                        foreach (TypeDefinition type in module.Types)
                        {
                            if (!type.HasCustomAttributes) continue;
                            foreach (CustomAttribute attr in type.CustomAttributes)
                            {
                                string full = attr.AttributeType.FullName;
                                if (full == "BepInEx.BepInPlugin" && attr.ConstructorArguments.Count > 0)
                                {
                                    u.PluginGuids.Add(attr.ConstructorArguments[0].Value as string ?? "");
                                }
                                else if (full == "BepInEx.BepInDependency" && attr.ConstructorArguments.Count > 0)
                                {
                                    // Single-arg ctor = hard dependency. Two-arg: flags bit 1 = hard.
                                    bool hard = true;
                                    if (attr.ConstructorArguments.Count > 1 && attr.ConstructorArguments[1].Value is int)
                                        hard = (((int)attr.ConstructorArguments[1].Value) & 1) != 0;
                                    if (hard)
                                        u.HardDependencyGuids.Add(attr.ConstructorArguments[0].Value as string ?? "");
                                }
                            }
                        }
                    }
                }
                catch { /* unreadable/native DLL - ignore */ }
            }
        }

        private static bool AnyDllListed(Unit u, List<string> blocklist)
        {
            foreach (string file in u.Files)
                if (IsDllMatch(file, blocklist)) return true;
            return false;
        }

        /// <summary>Lowercase + trim, so entries match 100% regardless of case or stray spaces.</summary>
        private static string Normalize(string s)
        {
            return (s ?? "").Trim().ToLowerInvariant();
        }

        private static bool IsProtected(string file)
        {
            string name = Path.GetFileName(file).ToLowerInvariant();
            if (name.EndsWith(".blocked")) name = name.Substring(0, name.Length - ".blocked".Length);
            foreach (string p in Protected)
                if (name == p) return true;
            return false;
        }

        private static void Apply(string file, bool block)
        {
            if (block && IsProtected(file))
            {
                Log("Ignored: " + Path.GetFileName(file) + " (ModBlocker cannot block itself)");
                block = false; // fall through so a previously .blocked copy gets re-enabled
            }
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
                    "## Example: SomeAuthor-SomeMod, SomeOldPlugin.dll",
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
