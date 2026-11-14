using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DiscUtils.Gdrom;
using DiscUtils.Iso9660;

#nullable enable

namespace SDBuilderWin.Dreamcast
{
    internal enum DcMenuKind { GdMenu, OpenMenu }

    /// <summary>One game folder on the SD card (\02, \03, ...) as it will appear in the menu.</summary>
    internal sealed class DcMenuEntry
    {
        public int Number;
        public string Folder = "";
        public string Name = "";
        public string Serial = "";
        public string Disc = "1/1";
        public bool Vga;
        public string Region = "JUE";
        public string Version = "V1.000";
        public string Date = "00000000";
        /// <summary>Where the metadata came from: "IP.BIN", "infoNN.txt" or "defaults".</summary>
        public string Source = "defaults";
    }

    internal sealed class DcMenuBuildResult
    {
        public DcMenuKind Kind;
        public int GameCount;
        public string IniName = "";
        public string IniText = "";
        public string MenuFolder = "";
    }

    /// <summary>
    /// Rebuilds the GDmenu / openMenu boot image in folder \01, the same way GDMENUCardManager does it:
    ///   1. scan every numbered game folder and read its IP.BIN (falls back to name.txt / serial.txt / infoNN.txt);
    ///   2. generate LIST.INI (GDmenu) or OPENMENU.INI (openMenu) in memory;
    ///   3. in a private temp folder, combine tools\&lt;menu&gt;\menu_data + the INI and build a fresh GD-ROM
    ///      in-process with DiscUtilsGD's GDromBuilder (track03 + track05 around the CDDA track04,
    ///      plus a new low-density track01 that also carries the INI) and update disc.gdi;
    ///   4. swap the finished image into \01 on the card.
    /// No external tools (buildgdi.exe / mkisofs.exe) and nothing is written into the app's Tools folder.
    /// This class has no WinForms dependency, so it can be tested or reused on its own.
    /// </summary>
    internal sealed class DreamcastMenuBuilder
    {
        const int LowDensityMaxSectors = 450; // track01 must end before track02 starts at LBA 450

        readonly string _toolsRoot;
        readonly Action<string> _log;

        public DreamcastMenuBuilder(string toolsRoot, Action<string> log)
        {
            _toolsRoot = toolsRoot;
            _log = log ?? (_ => { });
        }

        public static string ToolsSubfolder(DcMenuKind kind) => kind == DcMenuKind.GdMenu ? "gdMenu" : "openMenu";
        public static string IniFileName(DcMenuKind kind) => kind == DcMenuKind.GdMenu ? "LIST.INI" : "OPENMENU.INI";
        static string VolumeId(DcMenuKind kind) => kind == DcMenuKind.GdMenu ? "GDMENU" : "OPENMENU";

        string MenuToolsDir(DcMenuKind kind) => Path.Combine(_toolsRoot, ToolsSubfolder(kind));

        // ------------------------------------------------------------------ detection

        /// <summary>Which menu is installed in \01, or null when \01 is missing or holds something else.</summary>
        public static DcMenuKind? DetectInstalledMenu(string sdRoot)
        {
            var dir01 = Path.Combine(sdRoot, "01");
            if (!Directory.Exists(dir01)) return null;

            var kind = KindFromId(ReadFirstLine(Path.Combine(dir01, "serial.txt")));
            if (kind != null) return kind;

            foreach (var gdi in SafeGetFiles(dir01, "*.gdi"))
            {
                var ip = DcIpBin.FromGdi(gdi);
                kind = KindFromId(ip?.ProductNumber) ?? KindFromId(ip?.Name);
                if (kind != null) return kind;
            }
            return null;
        }

        static DcMenuKind? KindFromId(string? id)
        {
            switch ((id ?? "").Trim().ToUpperInvariant())
            {
                case "GDMENU": return DcMenuKind.GdMenu;
                case "NEODC_1":
                case "OPENMENU": return DcMenuKind.OpenMenu;
                default: return null;
            }
        }

        // ------------------------------------------------------------------ scanning

        /// <summary>Reads all numbered game folders (02, 03, ...). Folder 01 is the menu and is skipped.</summary>
        public List<DcMenuEntry> ScanCard(string sdRoot, CancellationToken token)
        {
            var result = new List<DcMenuEntry>();
            foreach (var dir in Directory.GetDirectories(sdRoot))
            {
                token.ThrowIfCancellationRequested();
                var leaf = Path.GetFileName(dir);
                if (leaf.Length < 2 || !leaf.All(char.IsDigit)) continue;
                if (!int.TryParse(leaf, out var number) || number <= 1) continue;
                if (!SafeGetFiles(dir, "*.*").Any(f => IsDiscImage(f))) continue;

                result.Add(ReadEntry(dir, number));
            }
            result.Sort((a, b) => a.Number.CompareTo(b.Number));
            return result;
        }

        static bool IsDiscImage(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".gdi" || ext == ".cdi" || ext == ".ccd" || ext == ".mds";
        }

        static DcMenuEntry ReadEntry(string dir, int number)
        {
            var e = new DcMenuEntry { Number = number, Folder = dir, Name = Path.GetFileName(dir) };

            // 1) IP.BIN straight from the disc image (like GDMENUCardManager)
            DcIpBin? ip = null;
            var gdi = Path.Combine(dir, "disc.gdi");
            if (!File.Exists(gdi)) gdi = SafeGetFiles(dir, "*.gdi").FirstOrDefault() ?? "";
            if (gdi.Length > 0) ip = DcIpBin.FromGdi(gdi);

            if (ip != null)
            {
                e.Name = ip.Name;
                e.Serial = ip.ProductNumber;
                e.Disc = ip.IsCodeBreaker ? "" : ip.Disc;
                e.Vga = ip.Vga;
                e.Region = ip.Region;
                e.Version = ip.Version;
                e.Date = ip.ReleaseDate;
                e.Source = "IP.BIN";
            }
            else
            {
                // 2) infoNN.txt written by SD-Builder when the game was copied (covers .cdi images)
                var nn = Path.GetFileName(dir);
                var info = ReadInfoFile(Path.Combine(dir, "info" + nn + ".txt"), nn);
                if (info.Count > 0)
                {
                    e.Source = "info" + nn + ".txt";
                    if (info.TryGetValue("name", out var v)) e.Name = v;
                    if (info.TryGetValue("product", out v)) e.Serial = v;
                    if (info.TryGetValue("disc", out v)) e.Disc = v;
                    if (info.TryGetValue("vga", out v)) e.Vga = v == "1";
                    if (info.TryGetValue("region", out v)) e.Region = v;
                    if (info.TryGetValue("version", out v)) e.Version = v;
                    if (info.TryGetValue("date", out v)) e.Date = v;
                }
            }

            // 3) name.txt / serial.txt win, so a user-edited title shows up in the menu (GDMENUCardManager behaviour)
            var nameTxt = ReadFirstLine(Path.Combine(dir, "name.txt"));
            if (!string.IsNullOrWhiteSpace(nameTxt)) e.Name = nameTxt!.Trim();
            var serialTxt = ReadFirstLine(Path.Combine(dir, "serial.txt"));
            if (!string.IsNullOrWhiteSpace(serialTxt)) e.Serial = serialTxt!.Trim();

            if (string.IsNullOrWhiteSpace(e.Name)) e.Name = Path.GetFileName(dir);
            return e;
        }

        static Dictionary<string, string> ReadInfoFile(string path, string nn)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return d;
            try
            {
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    int dot = line.IndexOf('.'), eq = line.IndexOf('=');
                    if (dot <= 0 || eq <= dot + 1) continue;
                    if (!string.Equals(line.Substring(0, dot), nn, StringComparison.Ordinal)) continue;
                    d[line.Substring(dot + 1, eq - dot - 1).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return d;
        }

        // ------------------------------------------------------------------ INI generation

        /// <summary>Builds LIST.INI / OPENMENU.INI text. Entry 01 is the menu itself, taken from its IP.BIN.</summary>
        public string BuildIni(DcMenuKind kind, DcIpBin menuIp, IReadOnlyList<DcMenuEntry> games)
        {
            bool open = kind == DcMenuKind.OpenMenu;
            var sb = new StringBuilder();
            if (open)
            {
                sb.AppendLine("[OPENMENU]");
                sb.AppendLine($"num_items={games.Count + 1}");
                sb.AppendLine();
                sb.AppendLine("[ITEMS]");
            }
            else
            {
                sb.AppendLine("[GDMENU]");
            }

            var menuEntry = new DcMenuEntry
            {
                Number = 1,
                Name = menuIp.Name,
                Serial = menuIp.ProductNumber,
                Disc = menuIp.Disc,
                Vga = menuIp.Vga,
                Region = menuIp.Region,
                Version = menuIp.Version,
                Date = menuIp.ReleaseDate
            };
            AppendEntry(sb, menuEntry, open);
            foreach (var g in games) AppendEntry(sb, g, open);
            return sb.ToString();
        }

        static void AppendEntry(StringBuilder sb, DcMenuEntry e, bool openMenu)
        {
            string nn = FormatNumber(e.Number);
            sb.AppendLine($"{nn}.name={e.Name}");
            sb.AppendLine($"{nn}.disc={e.Disc}");
            sb.AppendLine($"{nn}.vga={(e.Vga ? '1' : '0')}");
            sb.AppendLine($"{nn}.region={e.Region}");
            sb.AppendLine($"{nn}.version={e.Version}");
            sb.AppendLine($"{nn}.date={e.Date}");
            if (openMenu)
            {
                // openMenu matches artwork on the serial without dashes, e.g. "T-8101N" -> "T8101N"
                var product = (e.Serial ?? "").Replace("-", "").Split(' ')[0];
                sb.AppendLine($"{nn}.product={product}");
            }
            sb.AppendLine();
        }

        static string FormatNumber(int n) =>
            n < 100 ? n.ToString("00") : n < 1000 ? n.ToString("000") : n.ToString("0000");

        // ------------------------------------------------------------------ build + install

        /// <summary>Checks that tools\&lt;menu&gt;\ holds everything needed to build the image.</summary>
        public void ValidateTemplate(DcMenuKind kind)
        {
            var root = MenuToolsDir(kind);
            var sub = ToolsSubfolder(kind);
            void Need(string rel, bool dir = false)
            {
                var p = Path.Combine(root, rel);
                if (dir ? !Directory.Exists(p) : !File.Exists(p))
                    throw new FileNotFoundException($"Missing Tools\\{sub}\\{rel}");
            }
            Need("IP.BIN");
            Need("menu_data", dir: true);
            Need(Path.Combine("menu_data", "1ST_READ.BIN"));
            Need(Path.Combine("menu_gdi", "disc.gdi"));
            Need(Path.Combine("menu_gdi", "track01.iso"));
            Need(Path.Combine("menu_gdi", "track02.raw"));
            Need(Path.Combine("menu_gdi", "track04.raw"));

            var len = new FileInfo(Path.Combine(root, "IP.BIN")).Length;
            if (len != DcIpBin.Size) throw new InvalidDataException($"Tools\\{sub}\\IP.BIN is {len} bytes, expected {DcIpBin.Size}.");
        }

        public DcIpBin ReadMenuIp(DcMenuKind kind)
        {
            var path = Path.Combine(MenuToolsDir(kind), "IP.BIN");
            return DcIpBin.FromFile(path) ?? throw new InvalidDataException("Not a valid Dreamcast IP.BIN: " + path);
        }

        /// <summary>Scans the card, builds the menu image and installs it in \01. Runs synchronously; call from a worker thread.</summary>
        public DcMenuBuildResult BuildAndInstall(string sdRoot, DcMenuKind kind, CancellationToken token)
        {
            ValidateTemplate(kind);

            var installed = DetectInstalledMenu(sdRoot);
            var dir01 = Path.Combine(sdRoot, "01");
            if (installed == null && Directory.Exists(dir01) && Directory.EnumerateFileSystemEntries(dir01).Any())
                throw new InvalidOperationException(
                    "Folder \\01 exists but does not contain GDmenu/openMenu. GDEMU boots \\01 as the menu, " +
                    "so move that game to a free number first; nothing was changed.");
            if (installed != null && installed != kind)
                _log($"[DC menu] Switching menu on card from {installed} to {kind}.");

            var menuIp = ReadMenuIp(kind);
            var games = ScanCard(sdRoot, token);
            var iniName = IniFileName(kind);
            var iniText = BuildIni(kind, menuIp, games);
            _log($"[DC menu] {games.Count} game(s) found; building {kind} image…");
            foreach (var g in games.Where(g => g.Source == "defaults"))
                _log($"[DC menu] [WARN] \\{FormatNumber(g.Number)}: no IP.BIN or info file, using defaults for '{g.Name}'.");

            var temp = Path.Combine(Path.GetTempPath(), "SD-Builder", "dcmenu-" + Guid.NewGuid().ToString("N"));
            try
            {
                var gdiDir = BuildImage(kind, iniName, iniText, menuIp, temp, token);
                token.ThrowIfCancellationRequested();
                InstallToCard(gdiDir, sdRoot);
                EnsureGdemuIni(sdRoot);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }

            return new DcMenuBuildResult
            {
                Kind = kind,
                GameCount = games.Count,
                IniName = iniName,
                IniText = iniText,
                MenuFolder = dir01
            };
        }

        /// <summary>Builds the complete menu GD-ROM into &lt;temp&gt;\menu_gdi and returns that folder.</summary>
        string BuildImage(DcMenuKind kind, string iniName, string iniText, DcIpBin menuIp, string temp, CancellationToken token)
        {
            var src = MenuToolsDir(kind);
            var dataDir = Path.Combine(temp, "data");          // high density file system
            var lowDir = Path.Combine(temp, "lowdensity_data"); // track01 file system
            var gdiDir = Path.Combine(temp, "menu_gdi");        // finished image

            CopyDirectory(Path.Combine(src, "menu_data"), dataDir);
            Directory.CreateDirectory(lowDir);
            var lowSrc = Path.Combine(src, "menu_low_data");
            if (Directory.Exists(lowSrc)) CopyDirectory(lowSrc, lowDir);

            // Remove any stale INI left in the template, then write the fresh one to both areas.
            foreach (var stale in new[] { "LIST.INI", "OPENMENU.INI" })
            {
                var p = Path.Combine(dataDir, stale);
                if (File.Exists(p)) File.Delete(p);
            }
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(dataDir, iniName), iniText, utf8);
            File.WriteAllText(Path.Combine(lowDir, iniName), iniText, utf8);

            // Copy the static tracks (01, 02, 04 + disc.gdi); 03/05 are regenerated below.
            Directory.CreateDirectory(gdiDir);
            foreach (var f in Directory.GetFiles(Path.Combine(src, "menu_gdi")))
            {
                var n = Path.GetFileName(f).ToLowerInvariant();
                if (n.StartsWith("track03.") || n.StartsWith("track05.")) continue;
                File.Copy(f, Path.Combine(gdiDir, Path.GetFileName(f)), true);
            }
            token.ThrowIfCancellationRequested();

            // High density area: track03 (IP.BIN + file system) and track05 (1ST_READ.BIN at the end of the disc).
            // Output names come from the library (track03.bin / track05.bin, 2048-byte sectors); it writes
            // the last track's name into disc.gdi itself, so don't override Track03Path/LastTrackPath.
            var builder = new GDromBuilder(Path.Combine(src, "IP.BIN"), new List<string> { Path.Combine(gdiDir, "track04.raw") })
            {
                RawMode = false,
                TruncateData = true,
                VolumeIdentifier = VolumeId(kind),
            };
            builder.ImportFolder(dataDir, token: token);
            var tracks = builder.BuildGDROM(gdiDir, token);
            token.ThrowIfCancellationRequested();
            if (tracks == null) throw new OperationCanceledException(token);
            builder.UpdateGdiFile(tracks, Path.Combine(gdiDir, "disc.gdi"));

            // Low density area: regenerate track01 with the INI (as GDMENUCardManager does), if it still fits.
            BuildLowDensityTrack(kind, lowDir, Path.Combine(gdiDir, "track01.iso"));

            // name.txt / serial.txt let SD-Builder and GDMENUCardManager recognise the menu folder.
            File.WriteAllText(Path.Combine(gdiDir, "name.txt"), menuIp.Name, utf8);
            File.WriteAllText(Path.Combine(gdiDir, "serial.txt"), menuIp.ProductNumber, utf8);

            _log($"[DC menu] Image built: {string.Join(", ", tracks.Select(t => t.FileName))}");
            return gdiDir;
        }

        void BuildLowDensityTrack(DcMenuKind kind, string lowDir, string track01Path)
        {
            var tmp = track01Path + ".new";
            try
            {
                // CDBuilder throws on null identifiers, so set them all explicitly.
                var cd = new CDBuilder
                {
                    VolumeIdentifier = VolumeId(kind),
                    SystemIdentifier = string.Empty,
                    VolumeSetIdentifier = string.Empty,
                    PublisherIdentifier = string.Empty,
                    DataPreparerIdentifier = string.Empty,
                    ApplicationIdentifier = string.Empty,
                    UseJoliet = false,
                };
                foreach (var f in new DirectoryInfo(lowDir).GetFiles()) cd.AddFile(f.Name, f.FullName);
                cd.Build(tmp);
                if (new FileInfo(tmp).Length > LowDensityMaxSectors * 2048L)
                {
                    _log("[DC menu] [WARN] INI too large for track01; keeping the template track01.");
                    File.Delete(tmp);
                    return;
                }
                File.Copy(tmp, track01Path, true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        /// <summary>
        /// Writes the image to \01_new first, then swaps it in, so a failed copy never leaves the card without a menu.
        /// </summary>
        void InstallToCard(string gdiDir, string sdRoot)
        {
            var dir01 = Path.Combine(sdRoot, "01");
            var staging = Path.Combine(sdRoot, "01_new");
            var old = Path.Combine(sdRoot, "01_old");

            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            if (Directory.Exists(old)) Directory.Delete(old, true);

            CopyDirectory(gdiDir, staging);
            if (Directory.Exists(dir01)) Directory.Move(dir01, old);
            Directory.Move(staging, dir01);
            if (Directory.Exists(old)) Directory.Delete(old, true);
            _log($"[DC menu] Installed to {dir01}");
        }

        void EnsureGdemuIni(string sdRoot)
        {
            var path = Path.Combine(sdRoot, "GDEMU.ini");
            if (SafeGetFiles(sdRoot, "*.ini").Any(f => Path.GetFileName(f).Equals("GDEMU.ini", StringComparison.OrdinalIgnoreCase))) return;
            File.WriteAllLines(path, new[] { "open_time = 150", "detect_time = 150", "reset_goto = 1" });
            _log("[DC menu] Created default GDEMU.ini");
        }

        // ------------------------------------------------------------------ helpers

        static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(source))
                File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(source))
                CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
        }

        static string[] SafeGetFiles(string dir, string pattern)
        {
            try { return Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly); }
            catch { return Array.Empty<string>(); }
        }

        static string? ReadFirstLine(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            }
            catch { return null; }
        }
    }
}
