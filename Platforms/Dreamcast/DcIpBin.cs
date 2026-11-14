using System;
using System.IO;
using System.Text;

#nullable enable

namespace SDBuilderWin.Dreamcast
{
    /// <summary>
    /// Parsed Dreamcast IP.BIN header (the first 0x8000 bytes of the high-density data track).
    /// Field layout follows the Sega spec, the same fields GDMENUCardManager reads.
    /// </summary>
    internal sealed class DcIpBin
    {
        public const int Size = 0x8000;
        const string Signature = "SEGA SEGAKATANA";

        public string Name { get; private set; } = "";
        public string ProductNumber { get; private set; } = "";
        public string Version { get; private set; } = "V1.000";
        public string ReleaseDate { get; private set; } = "00000000";
        public string Region { get; private set; } = "JUE";
        public string Disc { get; private set; } = "1/1";
        public bool Vga { get; private set; }
        public bool IsCodeBreaker { get; private set; }

        public static DcIpBin? Parse(byte[]? ip)
        {
            if (ip == null || ip.Length < 0x100) return null;
            if (!Encoding.ASCII.GetString(ip, 0, Signature.Length).Equals(Signature, StringComparison.Ordinal)) return null;

            var media = Str(ip, 0x25, 6);           // "GD-ROM" / "CD-ROM" / "FCD"
            byte discNo = ip[0x2B], discTotal = ip[0x2D];
            var version = Str(ip, 0x4A, 6);
            var date = Str(ip, 0x50, 8);
            var boot = Str(ip, 0x60, 16);

            var r = new DcIpBin
            {
                Region = Str(ip, 0x30, 8),
                Vga = ip[0x38 + 5] == (byte)'1',      // peripherals[5]
                ProductNumber = Str(ip, 0x40, 10),
                Version = string.IsNullOrEmpty(version) ? "V1.000" : version,
                ReleaseDate = string.IsNullOrEmpty(date) ? "00000000" : date,
                Name = Str(ip, 0x80, 0x80),
            };

            if (discNo == 0x20 || discTotal == 0x20 || !IsDigit(discNo) || !IsDigit(discTotal))
            {
                r.Disc = "1/1";
                r.IsCodeBreaker = media == "FCD" && r.ReleaseDate == "20000627" && r.Version == "V1.000" && boot == "PELICAN.BIN";
            }
            else
            {
                r.Disc = $"{(char)discNo}/{(char)discTotal}";
            }
            return r;
        }

        public static DcIpBin? FromFile(string ipBinPath)
        {
            try
            {
                using var fs = File.OpenRead(ipBinPath);
                var buf = new byte[0x100];
                return fs.Read(buf, 0, buf.Length) == buf.Length ? Parse(buf) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Reads IP.BIN from the first high-density data track (track 3, LBA 45000) listed in a .gdi file.
        /// Only the first 256 bytes are read, so this is cheap even on an SD card.
        /// </summary>
        public static DcIpBin? FromGdi(string gdiPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(gdiPath) ?? "";
                foreach (var raw in File.ReadAllLines(gdiPath))
                {
                    var p = SplitGdiLine(raw);
                    if (p.Length < 5) continue;
                    if (!int.TryParse(p[0], out var trackNo) || !long.TryParse(p[1], out var lba)) continue;
                    if (!int.TryParse(p[2], out var type) || !int.TryParse(p[3], out var sectorSize)) continue;
                    if (type != 4 || lba < 45000) continue; // first HD data track

                    var track = Path.Combine(dir, p[4]);
                    if (!File.Exists(track)) return null;
                    using var fs = File.OpenRead(track);
                    if (sectorSize == 2352) fs.Seek(16, SeekOrigin.Begin); // skip raw Mode1 sync+header
                    var buf = new byte[0x100];
                    return fs.Read(buf, 0, buf.Length) == buf.Length ? Parse(buf) : null;
                }
            }
            catch { }
            return null;
        }

        // GDI lines: "3 45000 4 2048 track03.bin 0" — file names may be quoted and contain spaces.
        static string[] SplitGdiLine(string line)
        {
            var parts = new System.Collections.Generic.List<string>();
            var m = System.Text.RegularExpressions.Regex.Matches(line.Trim(), "\"[^\"]*\"|\\S+");
            foreach (System.Text.RegularExpressions.Match x in m) parts.Add(x.Value.Trim('"'));
            return parts.ToArray();
        }

        static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';

        static string Str(byte[] buf, int offset, int count)
        {
            var s = Encoding.ASCII.GetString(buf, offset, Math.Min(count, buf.Length - offset));
            int nul = s.IndexOf('\0');
            if (nul >= 0) s = s.Substring(0, nul);
            return s.Trim();
        }
    }
}
