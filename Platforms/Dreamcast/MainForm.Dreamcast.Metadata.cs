// Version: v2.31 - Master + Summercart64_64DDIPL (Designer refactor)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#nullable enable

namespace SDBuilderWin
{
    public sealed partial class MainForm
    {
        // Dreamcast index helpers

        static bool TryExtractDcIpMetadataFromGdi(string gdiPath, out string? title, out string? serial)
        {
            title = null; serial = null;
            try
            {
                if (!System.IO.File.Exists(gdiPath)) return false;
                var folder = System.IO.Path.GetDirectoryName(gdiPath);
                if (string.IsNullOrEmpty(folder)) return false;

                string? dataTrackPath = null;
                int sectorSize = 2352; // default

                foreach (var raw in System.IO.File.ReadAllLines(gdiPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || !char.IsDigit(line[0])) continue;
                    var parts = line.Split(System.Array.Empty<char>(), System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && int.TryParse(parts[2], out var tt) && int.TryParse(parts[3], out var ss))
                    {
                        if (tt == 4)
                        {
                            var maybe = System.IO.Path.Combine(folder, parts[4]);
                            if (System.IO.File.Exists(maybe))
                            {
                                dataTrackPath = maybe; sectorSize = ss; break;
                            }
                        }
                    }
                }
                if (dataTrackPath == null)
                {
                    var fallback = System.IO.Path.Combine(folder, "track03.bin");
                    if (System.IO.File.Exists(fallback)) { dataTrackPath = fallback; sectorSize = 2352; }
                }
                if (dataTrackPath == null) return false;

                var ip = ReadIpBinFromTrack(dataTrackPath, sectorSize);
                if (ip == null || ip.Length < 0x200) return false;

                serial = ExtractAscii(ip, 0x40, 10)?.Trim();
                title = ExtractAscii(ip, 0x80, 0x80)?.Trim();
                return !(string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(serial));
            }
            catch { return false; }
        }

        // Determine serial from a folder containing a .gdi or a single .gdi file (CDI skipped)
        static bool TryExtractDcSerialFromSource(string source, out string? serial)
        {
            serial = null;
            try
            {
                if (System.IO.Directory.Exists(source))
                {
                    // Look for .gdi in the folder and read IP.BIN
                    var gdi = System.IO.Directory.EnumerateFiles(source, "*.gdi", System.IO.SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (gdi != null && TryExtractDcIpMetadataFromGdi(gdi, out var _, out var sn))
                    {
                        if (!string.IsNullOrWhiteSpace(sn)) { serial = sn.ToUpperInvariant(); return true; }
                    }
                    return false;
                }
                if (System.IO.File.Exists(source))
                {
                    var ext = System.IO.Path.GetExtension(source).ToLowerInvariant();
                    if (ext == ".gdi")
                    {
                        if (TryExtractDcIpMetadataFromGdi(source, out var _, out var sn))
                        {
                            if (!string.IsNullOrWhiteSpace(sn)) { serial = sn.ToUpperInvariant(); return true; }
                        }
                    }
                }
                return false;
            }
            catch { return false; }
        }

        // Build a map of existing serials on the SD: serial -> index folder (02..9999). 01 is reserved.
        static System.Collections.Generic.Dictionary<string, string> IndexDcSerials(string destRoot)
        {
            var map = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!System.IO.Directory.Exists(destRoot)) return map;
                for (int i = 2; i <= 9999; i++)
                {
                    var idx = (i < 100) ? i.ToString("00") : (i < 1000) ? i.ToString("000") : i.ToString("0000");
                    var dir = System.IO.Path.Combine(destRoot, idx);
                    if (!System.IO.Directory.Exists(dir)) continue;
                    var p = System.IO.Path.Combine(dir, "serial.txt");
                    if (!System.IO.File.Exists(p)) continue;
                    var s = System.IO.File.ReadAllText(p).Trim();
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    s = s.ToUpperInvariant();
                    if (!map.ContainsKey(s)) map[s] = idx;
                }
            }
            catch { /* ignore */ }
            return map;
        }

        // Build a map of existing serials + disc markers on the SD: serial -> set of "D/T" strings (e.g., "1/2").
        static System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> IndexDcSerialDiscs(string destRoot)
        {
            var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!System.IO.Directory.Exists(destRoot)) return map;
                for (int i = 2; i <= 9999; i++)
                {
                    var idx = (i < 100) ? i.ToString("00") : (i < 1000) ? i.ToString("000") : i.ToString("0000");
                    var dir = System.IO.Path.Combine(destRoot, idx);
                    if (!System.IO.Directory.Exists(dir)) continue;

                    string? serial = null;
                    string? disc = null;

                    // Prefer infoNN.txt (has disc + product)
                    var infoPath = System.IO.Path.Combine(dir, "info" + idx + ".txt");
                    if (System.IO.File.Exists(infoPath))
                    {
                        try
                        {
                            foreach (var line in System.IO.File.ReadAllLines(infoPath, System.Text.Encoding.UTF8))
                            {
                                var s = (line ?? string.Empty).Trim();
                                if (s.Length == 0) continue;
                                if (disc == null && s.IndexOf(".disc=", System.StringComparison.OrdinalIgnoreCase) > 0)
                                {
                                    var p = s.Split(new[] { ".disc=" }, StringSplitOptions.None);
                                    if (p.Length == 2) disc = (p[1] ?? string.Empty).Trim();
                                }
                                if (!string.IsNullOrWhiteSpace(serial) && !string.IsNullOrWhiteSpace(disc)) break;
                            }
                        }
                        catch { /* ignore */ }
                    }

                    // Fallback to serial.txt (no disc info)
                    if (string.IsNullOrWhiteSpace(serial))
                    {
                        var p = System.IO.Path.Combine(dir, "serial.txt");
                        if (System.IO.File.Exists(p))
                        {
                            try { serial = System.IO.File.ReadAllText(p).Trim(); } catch { }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(serial)) continue;
                    disc = string.IsNullOrWhiteSpace(disc) ? "1/1" : disc;

                    if (!map.TryGetValue(serial, out var set))
                    {
                        set = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                        map[serial] = set;
                    }
                    set.Add(disc);
                }
            }
            catch { /* ignore */ }
            return map;
        }

        // Best-effort parse of disc number from a filename/title. Returns true if a disc number was found.
        static bool TryParseDiscFromName(string? name, out int discNum, out int discTot)
        {
            discNum = 1; discTot = 1;
            if (string.IsNullOrWhiteSpace(name)) return false;
            string s = name!;

            // Normalize
            s = s.Replace('_', ' ').Trim();

            // Patterns: (Disc 1 of 2), (Disc 2), Disc 1, CD1, CD 2, (1of2), [Disc 2/2], etc.
            var patterns = new string[]
            {
        @"\bDisc\s*(\d+)\s*(?:of|/)\s*(\d+)\b",
        @"\bCD\s*(\d+)\s*(?:of|/)\s*(\d+)\b",
        @"\bDisk\s*(\d+)\s*(?:of|/)\s*(\d+)\b",
        @"\bDisc\s*(\d+)\b",
        @"\bCD\s*(\d+)\b",
        @"\bDisk\s*(\d+)\b",
        @"\b\(?(\d+)\s*(?:of|/)\s*(\d+)\)?\b",
        @"\bCD(\d+)\b",
        @"\bDisc(\d+)\b"
            };

            foreach (var pat in patterns)
            {
                var m = System.Text.RegularExpressions.Regex.Match(s, pat, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    if (m.Groups.Count >= 2 && int.TryParse(m.Groups[1].Value, out int n))
                    {
                        discNum = n;
                        if (m.Groups.Count >= 3 && int.TryParse(m.Groups[2].Value, out int t)) discTot = t;
                        return true;
                    }
                }
            }
            return false;
        }

        // Try to produce a "D/T" disc key from UI text and/or a file path.
        static bool TryGuessDiscKey(string itemText, string path, out string discKey)
        {
            discKey = "";
            int dNum, dTot;
            // 1) From the item text
            if (TryParseDiscFromName(itemText, out dNum, out dTot))
            {
                discKey = dNum.ToString() + "/" + (dTot <= 0 ? "1" : dTot.ToString());
                return true;
            }
            // 2) From the file/folder name
            var leaf = System.IO.Path.GetFileName(path) ?? "";
            if (TryParseDiscFromName(leaf, out dNum, out dTot))
            {
                discKey = dNum.ToString() + "/" + (dTot <= 0 ? "1" : dTot.ToString());
                return true;
            }
            var parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "") ?? "";
            if (TryParseDiscFromName(parent, out dNum, out dTot))
            {
                discKey = dNum.ToString() + "/" + (dTot <= 0 ? "1" : dTot.ToString());
                return true;
            }
            return false;
        }



        // --- Helper: sanitize Dreamcast menu title for both infoNN.txt and name.txt ---
        private static string SanitizeMenuTitle32(string? s)
        {
            s ??= string.Empty;
            // Remove diacritics
            string n = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(n.Length);
            for (int i = 0; i < n.Length; i++)
            {
                var ch = n[i];
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            string noDiac = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

            // Keep only A–Z a–z 0–9 space underscore dash; collapse spaces
            var outSb = new System.Text.StringBuilder(noDiac.Length);
            bool lastSpace = false;
            for (int i = 0; i < noDiac.Length; i++)
            {
                char ch = noDiac[i];
                bool ok = (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-' || ch == ' ';
                if (!ok) ch = ' ';
                if (ch == ' ')
                {
                    if (lastSpace) continue;
                    lastSpace = true;
                    outSb.Append(' ');
                }
                else
                {
                    lastSpace = false;
                    outSb.Append(ch);
                }
            }
            string clean = outSb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(clean)) clean = "UNKNOWN";

            // Word-safe 32-char trim
            const int Max = 32;
            if (clean.Length > Max)
            {
                int cut = clean.LastIndexOf(' ', Max);
                clean = (cut > 0) ? clean.Substring(0, cut) : clean.Substring(0, Max);
            }
            return clean;
        }


        // ---- Dreamcast: Extract title.txt and serial.txt from IP.BIN (from disc.gdi data track) ----
        static bool TryExtractDcIpMetadata(string dcIndexFolder, out string? title, out string? serial)
        {
            title = null;
            serial = null;
            try
            {
                var gdiPath = System.IO.Path.Combine(dcIndexFolder, "disc.gdi");
                if (!System.IO.File.Exists(gdiPath))
                    return false;

                // Parse disc.gdi to locate the first data track (trackType == 4).
                string? dataTrackPath = null;
                int sectorSize = 2352; // default/fallback
                var lines = System.IO.File.ReadAllLines(gdiPath);
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (!char.IsDigit(line[0])) continue;

                    // Typical format per line: <trackNo> <LBA> <trackType> <sectorSize> <filename> <offset>
                    var parts = line.Split(System.Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        int tt = 0, ss = 0;
                        if (int.TryParse(parts[2], out tt) && int.TryParse(parts[3], out ss))
                        {
                            if (tt == 4)
                            {
                                var maybe = System.IO.Path.Combine(dcIndexFolder, parts[4]);
                                if (System.IO.File.Exists(maybe))
                                {
                                    dataTrackPath = maybe;
                                    sectorSize = ss;
                                    break; // first data track
                                }
                            }
                        }
                    }
                }

                if (dataTrackPath == null)
                {
                    // Fallback: many images use track03.bin as first data track
                    var maybe = System.IO.Path.Combine(dcIndexFolder, "track03.bin");
                    if (System.IO.File.Exists(maybe))
                    {
                        dataTrackPath = maybe;
                        sectorSize = 2352;
                    }
                }
                if (dataTrackPath == null) return false;

                byte[] ip = ReadIpBinFromTrack(dataTrackPath, sectorSize);
                if (ip == null || ip.Length < 0x200) return false;

                // Serial/product at 0x40 (length ~10, ASCII padded)
                var prod = ExtractAscii(ip, 0x40, 10);
                if (!string.IsNullOrWhiteSpace(prod)) serial = prod.Trim();

                // Title/internal name at 0x80, length 0x80 (128 bytes), ASCII padded
                var name = ExtractAscii(ip, 0x80, 0x80);
                if (!string.IsNullOrWhiteSpace(name)) title = name.Trim();

                return (title != null) || (serial != null);
            }
            catch
            {
                return false;
            }
        }

        static byte[] ReadIpBinFromTrack(string trackPath, int sectorSize)
        {
            // IP.BIN is the first 32 KiB of user data (16 sectors * 2048 bytes).
            const int IP_LEN = 32768;
            using (var fs = System.IO.File.OpenRead(trackPath))
            {
                if (sectorSize == 2048)
                {
                    int toRead = (int)System.Math.Min(IP_LEN, System.Math.Max(0, fs.Length));
                    var buf = new byte[toRead];
                    fs.Read(buf, 0, toRead);
                    return buf;
                }
                else if (sectorSize == 2352)
                {
                    int sectors = 16;
                    int per = 2352;
                    var outBuf = new byte[sectors * 2048];
                    var inBuf = new byte[per];
                    int outOfs = 0;
                    for (int i = 0; i < sectors; i++)
                    {
                        int read = fs.Read(inBuf, 0, per);
                        if (read < per) break;
                        // Mode1 user data usually begins at offset 16 (skip sync + header)
                        System.Buffer.BlockCopy(inBuf, 16, outBuf, outOfs, System.Math.Min(2048, read - 16));
                        outOfs += 2048;
                    }
                    return outBuf;
                }
                else
                {
                    // Unknown sector size: read raw up to IP_LEN
                    int toRead = (int)System.Math.Min(IP_LEN, System.Math.Max(0, fs.Length));
                    var buf = new byte[toRead];
                    fs.Read(buf, 0, toRead);
                    return buf;
                }
            }
        }

        static string ExtractAscii(byte[] buf, int offset, int count)
        {
            try
            {
                if (offset < 0 || offset + count > buf.Length) return string.Empty;
                var s = System.Text.Encoding.ASCII.GetString(buf, offset, count);
                // Normalize: replace NULs with spaces, trim, collapse double spaces
                s = s.Replace('\0', ' ').Trim();
                while (s.Contains("  ")) s = s.Replace("  ", " ");
                return s;
            }
            catch { return string.Empty; }
        }
    }
}
