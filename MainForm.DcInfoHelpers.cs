// Dreamcast IP.BIN helpers (no LINQ, no nullable annotations) - safe for older compilers
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SDBuilderWin
{
    public partial class MainForm
    {
        // Heuristic scan to find version/date when not found at fixed offsets
        static void ExtractIpBinExtras(byte[] ip, out string version, out string date)
        {
            version = "V1.000";
            date = "00000000";
            try
            {
                if (ip == null) return;
                int span = Math.Min(ip.Length, 8192);
                var ascii = Encoding.ASCII.GetString(ip, 0, span);

                Match mVer = Regex.Match(ascii, @"V\d\.\d{3}", RegexOptions.IgnoreCase);
                if (mVer.Success) version = mVer.Value.ToUpperInvariant();

                Match mDate = Regex.Match(ascii, @"\b(19|20)\d{6}\b");
                if (mDate.Success) date = mDate.Value;
            }
            catch
            {
                // best-effort only
            }
        }

        // Try to read IP.BIN from a folder: prefer data track from any .gdi, then common fallbacks
        static bool TryReadIpBinFromFolder(string folder, out byte[] ipOut)
        {
            ipOut = null;
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;

                // 1) Any *.gdi present?
                string[] gdis = Directory.GetFiles(folder, "*.gdi");
                for (int gi = 0; gi < gdis.Length; gi++)
                {
                    string gdi = gdis[gi];
                    string dataTrackPath = null;
                    int sectorSize = 2352;
                    string[] lines = File.ReadAllLines(gdi);
                    for (int li = 0; li < lines.Length; li++)
                    {
                        string line = lines[li].Trim();
                        if (line.Length == 0 || !char.IsDigit(line[0])) continue;
                        string[] parts = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            int tt = 0, ss = 0;
                            int.TryParse(parts[2], out tt);
                            int.TryParse(parts[3], out ss);
                            if (tt == 4)
                            {
                                string maybe = Path.Combine(Path.GetDirectoryName(gdi) ?? folder, parts[4]);
                                if (File.Exists(maybe)) { dataTrackPath = maybe; sectorSize = ss; break; }
                            }
                        }
                    }
                    if (dataTrackPath != null)
                    {
                        byte[] ip = ReadIpBinFromTrack(dataTrackPath, sectorSize);
                        if (ip != null && ip.Length >= 0x200) { ipOut = ip; return true; }
                    }
                }

                // 2) Fallback: common data track names
                string[] names = new string[] { "track03.bin", "track05.iso", "track05.bin" };
                for (int i = 0; i < names.Length; i++)
                {
                    string path = Path.Combine(folder, names[i]);
                    if (File.Exists(path))
                    {
                        int ss = path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ? 2352 : 2048;
                        byte[] ip = ReadIpBinFromTrack(path, ss);
                        if (ip != null && ip.Length >= 0x200) { ipOut = ip; return true; }
                    }
                }

                return false;
            }
            catch
            {
                ipOut = null;
                return false;
            }
        }

        // Write infoNN.txt based on IP.BIN fields using safe offsets and fallbacks
        private static void WriteDcInfoFile(string dcIndexFolder, string titleFromCode, string serialFromCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dcIndexFolder) || !Directory.Exists(dcIndexFolder)) return;
                string nnRaw = Path.GetFileName(dcIndexFolder);
                if (string.IsNullOrWhiteSpace(nnRaw) || !IsAllDigits(nnRaw) || nnRaw.Length < 2) return;
                string nn = nnRaw;

                string name = string.IsNullOrWhiteSpace(titleFromCode) ? (Path.GetFileName(dcIndexFolder) ?? "Unknown") : titleFromCode.Trim();
                string serial = string.IsNullOrWhiteSpace(serialFromCode) ? "" : serialFromCode.Trim().ToUpperInvariant();
                string version = "V1.000";
                string date = "00000000";
                string region = "E";
                int vga = 0;
                int dNum = 1, dTot = 1;

                byte[] ip;
                if (TryReadIpBinFromFolder(dcIndexFolder, out ip) && ip != null)
                {
                    // Title & serial
                    string nameIp = ExtractAscii(ip, 0x80, 0x80);
                    string serialIp = ExtractAscii(ip, 0x40, 10);
                    if (!string.IsNullOrWhiteSpace(nameIp)) name = nameIp.Trim();
                    if (!string.IsNullOrWhiteSpace(serialIp)) serial = serialIp.Trim().ToUpperInvariant();
                    
                    // Serial cleanup: remove hyphens and non-alphanumeric chars
                    if (!string.IsNullOrWhiteSpace(serial))
                    {
                    serial = Regex.Replace(serial, @"[^A-Z0-9]", "");
                    }

                    // Version: scan 0x40..0x60 for Vx.xxx, else fallback to regex scan
                    string verWindow = ExtractAscii(ip, 0x40, 0x20);
                    Match mVer = Regex.Match(verWindow ?? string.Empty, @"V\d\.\d{3}", RegexOptions.IgnoreCase);
                    if (mVer.Success) version = mVer.Value.ToUpperInvariant();
                    else
                    {
                        string vScan, dScan;
                        ExtractIpBinExtras(ip, out vScan, out dScan);
                        if (!string.IsNullOrWhiteSpace(vScan)) version = vScan;
                        if (!string.IsNullOrWhiteSpace(dScan) && dScan != "00000000") date = dScan;
                    }

                    // Date priority: 0x56..0x63, then 0x50..0x5F, then 0x60..
                    date = "00000000";
                    string date56 = ExtractAscii(ip, 0x56, 0x0E);
                    Match md56 = Regex.Match((date56 ?? string.Empty), @"\b(19|20)\d{6}\b");
                    if (md56.Success) date = md56.Value;
                    if (date == "00000000")
                    {
                        string date50 = ExtractAscii(ip, 0x50, 0x10);
                        Match md50 = Regex.Match((date50 ?? string.Empty), @"\b(19|20)\d{6}\b");
                        if (md50.Success) date = md50.Value;
                    }
                    if (date == "00000000")
                    {
                        string date60 = ExtractAscii(ip, 0x60, 0x10);
                        Match md60 = Regex.Match((date60 ?? string.Empty), @"\b(19|20)\d{6}\b");
                        if (md60.Success) date = md60.Value;
                    }
                    if (date == "00000000")
                    {
                        string v2, d2;
                        ExtractIpBinExtras(ip, out v2, out d2);
                        if (!string.IsNullOrWhiteSpace(v2)) version = v2;
                        if (!string.IsNullOrWhiteSpace(d2) && d2 != "00000000") date = d2;
                    }

                    // Region J/U/E at 0x30..0x37
                    string regionRaw = ExtractAscii(ip, 0x30, 8);
                    if (!string.IsNullOrWhiteSpace(regionRaw))
                    {
                        StringBuilder kept = new StringBuilder();
                        for (int i = 0; i < regionRaw.Length; i++)
                        {
                            char ch = char.ToUpperInvariant(regionRaw[i]);
                            if (ch == 'J' || ch == 'U' || ch == 'E')
                            {
                                if (kept.ToString().IndexOf(ch) < 0) kept.Append(ch);
                            }
                        }
                        if (kept.Length > 0) region = kept.ToString();
                    }

                    // VGA flag from peripheral bitmap @0x18 (bit 0x200)
                    if (ip.Length >= 0x1C)
                    {
                        uint per = BitConverter.ToUInt32(ip, 0x18);
                        if ((per & 0x200u) != 0) vga = 1;
                    }

                    // Disc number/total at 0x28..0x2F as ASCII "D/N" (supports multi-digit)
                    dNum = 1; dTot = 1;
                    string discAsciiFull = (ExtractAscii(ip, 0x28, 8) ?? string.Empty).Trim();
                    bool parsedDisc = false;
                    if (discAsciiFull.IndexOf('/') >= 0)
                    {
                        string[] parts = discAsciiFull.Split('/');
                        if (parts.Length >= 2)
                        {
                            int.TryParse(parts[0].Trim(), out dNum);
                            int.TryParse(parts[1].Trim(), out dTot);
                            if (dNum > 0 && dTot > 0) parsedDisc = true;
                        }
                    }
                    if (!parsedDisc)
                    {
                        // Fallback for strict single-digit format "D/N" located at 0x2B..0x2D
                        if (0x2D < ip.Length &&
                            ip[0x2B] >= (byte)'0' && ip[0x2B] <= (byte)'9' &&
                            ip[0x2C] == (byte)'/' &&
                            ip[0x2D] >= (byte)'0' && ip[0x2D] <= (byte)'9')
                        {
                            dNum = ip[0x2B] - (byte)'0';
                            dTot = ip[0x2D] - (byte)'0';
                            if (dNum <= 0) dNum = 1;
                            if (dTot <= 0) dTot = 1;
                        }
                    }
                }



// ASCII sanitize + word-safe 32-char trim for title


{


    // Remove diacritics


    string __norm = (name ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);


    var __sb = new System.Text.StringBuilder(__norm.Length);


    for (int __i = 0; __i < __norm.Length; __i++)


    {


        char __ch = __norm[__i];


        var __cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(__ch);


        if (__cat != System.Globalization.UnicodeCategory.NonSpacingMark) __sb.Append(__ch);


    }


    string __s = __sb.ToString().Normalize(System.Text.NormalizationForm.FormC);


    // Keep only A–Z a–z 0–9 space underscore dash; collapse spaces


    var __out = new System.Text.StringBuilder(__s.Length);


    bool __lastSpace = false;


    for (int __i = 0; __i < __s.Length; __i++)


    {


        char __ch = __s[__i];


        bool __ok = (__ch >= 'A' && __ch <= 'Z') || (__ch >= 'a' && __ch <= 'z') || (__ch >= '0' && __ch <= '9') || __ch == '_' || __ch == '-' || __ch == ' ';


        if (!__ok) __ch = ' ';


        if (__ch == ' ')


        {


            if (__lastSpace) continue;


            __lastSpace = true;


            __out.Append(' ');


        }


        else


        {


            __lastSpace = false;


            __out.Append(__ch);


        }


    }


    name = __out.ToString().Trim();


    if (string.IsNullOrWhiteSpace(name)) name = "UNKNOWN";


    // Word-safe 32-char trim


    int __maxNameLen = 32;


    if (name.Length > __maxNameLen)


    {


        int cut = name.LastIndexOf(' ', __maxNameLen);


        if (cut > 0) name = name.Substring(0, cut);


        else name = name.Substring(0, __maxNameLen);


    }


}


                var linesOut = new System.Collections.Generic.List<string>()
                {
                    nn + ".name=" + name,
                    nn + ".disc=" + dNum + "/" + dTot,
                    nn + ".vga=" + vga,
                    nn + ".region=" + region,
                    nn + ".version=" + version,
                    nn + ".date=" + date
                };
                if (!string.IsNullOrEmpty(serial)) linesOut.Add(nn + ".product=" + serial);
                string content = string.Join("\r\n", linesOut) + "\r\n";

                string pathFinal = Path.Combine(dcIndexFolder, "info" + nn + ".txt");
                string pathTemp  = pathFinal + ".tmp";

                if (File.Exists(pathFinal))
                {
                    try
                    {
                        string existing = File.ReadAllText(pathFinal, Encoding.UTF8);
                        if (string.Equals(existing, content, StringComparison.Ordinal))
                            return;
                    }
                    catch
                    {
                        // ignore read errors, we will overwrite
                    }
                }

                File.WriteAllText(pathTemp, content, new UTF8Encoding(false));
                if (File.Exists(pathFinal)) File.Delete(pathFinal);
                File.Move(pathTemp, pathFinal);
            }
            catch
            {
                // best-effort only
            }
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsDigit(s[i])) return false;
            }
            return true;
        }
    }
}
