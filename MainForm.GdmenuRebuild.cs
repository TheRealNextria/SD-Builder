// Rebuild GDMenu LIST.INI from infoNN.txt files
// Writes output to: <app>\tools\menu_data\GDmenu\LIST.INI (NOT the SD root)
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms; // Application.StartupPath

namespace SDBuilderWin
{
    public partial class MainForm
    {
        /// <summary>
        /// Rebuilds LIST.INI under <app>\tools\menu_data\GDmenu\LIST.INI.
        /// Reads infoNN.txt from sdRoot\NN\ (but always forces 01 to GDMenu fixed block).
        /// </summary>
        public static bool RebuildGdmenuList(string sdRoot, out string listPath, out int writtenCount)
        {
            // Resolve output dir next to the .exe
            string appBase = Application.StartupPath;
            string outDir = Path.Combine(appBase, "Tools", "gdMenu", "menu_data");
            try { Directory.CreateDirectory(outDir); } catch { }
            listPath = Path.Combine(outDir, "LIST.INI");

            writtenCount = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(sdRoot) || !Directory.Exists(sdRoot))
                    return false;

                // Collect entries from infoNN.txt (except 01 - we will hardcode 01 as GDMenu)
                var entries = new List<_Entry>();
                string[] dirs = Directory.GetDirectories(sdRoot, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dir = dirs[i];
                    string nn = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(nn)) continue;
                    if (nn.Length < 2) continue;
                    if (nn == "01") continue; // 01 is always GDMenu
                    if (!IsAllDigits(nn)) continue;

                    string infoPath = Path.Combine(dir, "info" + nn + ".txt");
                    if (!File.Exists(infoPath)) continue;

                    _Entry e;
                    if (TryReadInfo(infoPath, nn, out e))
                        entries.Add(e);
                }

                // Sort numerically
                entries.Sort((a, b) =>
                {
                    int ai = 0, bi = 0;
                    int.TryParse(a.NN, out ai);
                    int.TryParse(b.NN, out bi);
                    return ai.CompareTo(bi);
                });

                // Build LIST.INI content
                var sb = new StringBuilder();

                // Always start with GDMenu fixed 01 block
                sb.AppendLine("[GDMENU]");
                sb.AppendLine("01.name=GDMENU");
                sb.AppendLine("01.disc=1/1");
                sb.AppendLine("01.vga=1");
                sb.AppendLine("01.region=JUE");
                sb.AppendLine("01.version=V0.6.0");
                sb.AppendLine("01.date=20160812");
                sb.AppendLine();

                // Append the rest (02, 03, ...) from infoNN.txt
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.NN == "01") continue; // defensive

                    sb.AppendLine(e.NN + ".name=" + e.Name);
                    sb.AppendLine(e.NN + ".disc=" + e.Disc);
                    sb.AppendLine(e.NN + ".vga=" + e.Vga);
                    sb.AppendLine(e.NN + ".region=" + e.Region);
                    sb.AppendLine(e.NN + ".version=" + e.Version);
                    sb.AppendLine(e.NN + ".date=" + e.Date);
                    sb.AppendLine();
                }

                // Atomic-ish write to <app>\tools\menu_data\LIST.INI
                string tmp = listPath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(listPath)) File.Delete(listPath);
                File.Move(tmp, listPath);

                writtenCount = 1 + entries.Count;
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Rebuilds OPENMENU.INI under <app>\tools\menu_data\OpenMenu\OPENMENU.INI.
        /// Reads infoNN.txt from sdRoot\NN\ (but always forces 01 as fixed OpenMenu entry).
        /// </summary>
        public static bool RebuildOpenmenuList(string sdRoot, out string listPath, out int writtenCount)
        {
            string appBase = Application.StartupPath;
            string outDir = Path.Combine(appBase, "Tools", "OpenMenu", "menu_data");
            try { Directory.CreateDirectory(outDir); } catch { }
            listPath = Path.Combine(outDir, "OPENMENU.INI");
            writtenCount = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(sdRoot) || !Directory.Exists(sdRoot))
                    return false;

                var entries = new List<_Entry>();
                string[] dirs = Directory.GetDirectories(sdRoot, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dir = dirs[i];
                    string nn = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(nn)) continue;
                    if (nn.Length < 2) continue;
                    if (nn == "01") continue; // 01 is openMenu header
                    if (!IsAllDigits(nn)) continue;

                    string infoPath = Path.Combine(dir, "info" + nn + ".txt");
                    if (!File.Exists(infoPath)) continue;

                    _Entry e;
                    if (TryReadInfo(infoPath, nn, out e))
                        entries.Add(e);
                }

                // sort numerically
                entries.Sort((a, b) =>
                {
                    int ai = 0, bi = 0;
                    int.TryParse(a.NN, out ai);
                    int.TryParse(b.NN, out bi);
                    return ai.CompareTo(bi);
                });

                var sb = new StringBuilder();
                sb.AppendLine("[OPENMENU]");
                int total = 1 + entries.Count; // include slot 01 (openMenu)
                sb.Append("num_items=").Append(total.ToString()).AppendLine();
                sb.AppendLine();
                sb.AppendLine("[ITEMS]");
                // Fixed 01 openMenu block (matches NEODC_1 baseline)
                sb.AppendLine("01.name=openMenu");
                sb.AppendLine("01.disc=1/1");
                sb.AppendLine("01.vga=1");
                sb.AppendLine("01.region=JUE");
                sb.AppendLine("01.version=V0.1.0");
                sb.AppendLine("01.date=20210609");
                sb.AppendLine("01.product=NEODC_1");
                sb.AppendLine();

                foreach (var e in entries)
                {
                    // Load product (serial) from infoNN.txt if available
                    string prod = "";
                    try {
                        string infoPath = Path.Combine(sdRoot, e.NN, "info" + e.NN + ".txt");
                        if (File.Exists(infoPath))
                        {
                            foreach (var line in File.ReadAllLines(infoPath))
                            {
                                var t = (line ?? string.Empty).Trim();
                                if (t.Length == 0) continue;
                                if (t.StartsWith(e.NN + ".product=", StringComparison.OrdinalIgnoreCase))
                                {
                                    prod = t.Substring((e.NN + ".product=").Length).Trim();
                                    break;
                                }
                            }
                        }
                    } catch { prod = ""; }

                    if (e.NN == "01") continue;
                    sb.AppendLine(e.NN + ".name=" + e.Name);
                    sb.AppendLine(e.NN + ".disc=" + e.Disc);
                    if (!string.IsNullOrEmpty(e.Vga)) sb.AppendLine(e.NN + ".vga=" + e.Vga);
                    if (!string.IsNullOrEmpty(e.Region)) sb.AppendLine(e.NN + ".region=" + e.Region);
                    if (!string.IsNullOrEmpty(e.Version)) sb.AppendLine(e.NN + ".version=" + e.Version);
                    if (!string.IsNullOrEmpty(e.Date)) sb.AppendLine(e.NN + ".date=" + e.Date);
                    if (!string.IsNullOrEmpty(prod)) sb.AppendLine(e.NN + ".product=" + prod);
                    sb.AppendLine();
                }

                string tmp = listPath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(listPath)) File.Delete(listPath);
                File.Move(tmp, listPath);

                writtenCount = total;
                return true;
            }
            catch
            {
                return false;
            }
        }
    


        // Parses infoNN.txt; fills defaults if keys are missing.
        private static bool TryReadInfo(string infoPath, string nn, out _Entry entry)
        {
            entry = new _Entry();
            entry.NN = nn;
            entry.Name = "Unknown";
            entry.Disc = "1/1";
            entry.Vga = "0";
            entry.Region = "E";
            entry.Version = "V1.000";
            entry.Date = "00000000";

            try
            {
                string[] lines = File.ReadAllLines(infoPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int dot = line.IndexOf('.');
                    int eq = line.IndexOf('=');
                    if (dot <= 0 || eq <= dot + 1) continue;

                    string keyNN = line.Substring(0, dot).Trim();
                    if (!string.Equals(keyNN, nn, StringComparison.Ordinal)) continue;

                    string key = line.Substring(dot + 1, eq - dot - 1).Trim().ToLowerInvariant();
                    string val = (eq + 1 < line.Length) ? line.Substring(eq + 1).Trim() : "";

                    if (key == "name") entry.Name = val;
                    else if (key == "disc") entry.Disc = NormalizeDisc(val);
                    else if (key == "vga") entry.Vga = Normalize01(val);
                    else if (key == "region") entry.Region = NormalizeRegion(val);
                    else if (key == "version") entry.Version = NormalizeVersion(val);
                    else if (key == "date") entry.Date = NormalizeDate(val);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeDisc(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "1/1";
            s = s.Trim();
            int slash = s.IndexOf('/');
            if (slash <= 0 || slash >= s.Length - 1) return "1/1";
            int dn = 1, dt = 1;
            int.TryParse(s.Substring(0, slash).Trim(), out dn);
            int.TryParse(s.Substring(slash + 1).Trim(), out dt);
            if (dn <= 0) dn = 1;
            if (dt <= 0) dt = 1;
            return dn.ToString() + "/" + dt.ToString();
        }

        private static string Normalize01(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "0";
            s = s.Trim();
            if (s == "1" || s.ToLowerInvariant() == "true" || s.ToLowerInvariant() == "yes") return "1";
            return "0";
        }

        private static string NormalizeRegion(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "E";
            s = s.Trim().ToUpperInvariant();
            bool hasJ = false, hasU = false, hasE = false;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == 'J' && !hasJ) { sb.Append('J'); hasJ = true; }
                else if (c == 'U' && !hasU) { sb.Append('U'); hasU = true; }
                else if (c == 'E' && !hasE) { sb.Append('E'); hasE = true; }
            }
            if (sb.Length == 0) sb.Append('E');
            return sb.ToString();
        }

        private static string NormalizeVersion(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "V1.000";
            s = s.Trim().ToUpperInvariant();
            if (!s.StartsWith("V")) s = "V" + s;
            return s;
        }

        private static string NormalizeDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "00000000";
            s = s.Trim();
            if (s.Length == 8)
            {
                bool allDigits = true;
                for (int i = 0; i < 8; i++) if (!char.IsDigit(s[i])) { allDigits = false; break; }
                if (allDigits) return s;
            }
            return "00000000";
        }

        private struct _Entry
        {
            public string NN;
            public string Name;
            public string Disc;
            public string Vga;
            public string Region;
            public string Version;
            public string Date;
        }
        // NOTE: IsAllDigits(string) is provided by another partial (DcInfoHelpers.cs).
    
        // Returns a friendly folder label for the menu data output
        private static string GetMenuFolderLabel(string fmt)
        {
            return string.Equals(fmt, "OpenMenu", StringComparison.OrdinalIgnoreCase)
                ? "OpenMenu Folder"
                : "GDMenu Folder";
        }
}
}
