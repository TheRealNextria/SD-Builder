using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace SDBuilderWin
{
    // Copying one Dreamcast game to the card. Used by both "Start" (game list .txt)
    // and "Start checked" (Dreamcast tab), so a fix here applies to both.
    public sealed partial class MainForm
    {
        /// <summary>Serial numbers / discs already on the card, used to skip games that are present.</summary>
        sealed class DcCardIndex
        {
            public Dictionary<string, string> Serials = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, HashSet<string>> SerialDiscs = new(StringComparer.OrdinalIgnoreCase);

            public static DcCardIndex Read(string destRoot, Action<string>? warn = null) => new DcCardIndex
            {
                Serials = IndexDcSerials(destRoot, warn),
                SerialDiscs = IndexDcSerialDiscs(destRoot, warn),
            };
        }

        // The card index reads every \NN folder and the serial check reads IP.BIN from the source (often the NAS):
        // both run on a background thread so the window keeps responding during a copy job.
        // Unreadable info files are logged: such a game is not recognised and could be copied again.
        Task<DcCardIndex> ReadDcCardIndexAsync(string destRoot, CancellationToken token) =>
            Task.Run(() => DcCardIndex.Read(destRoot, msg => Warn(msg + " (duplicate check may miss this game)")), token);

        Task<bool> IsDcGameAlreadyOnCardAsync(string source, DcCardIndex index, CancellationToken token) =>
            Task.Run(() => IsDcGameAlreadyOnCard(source, index), token);

        /// <summary>True (and logs a [SKIP]) when the game in <paramref name="source"/> is already on the card.</summary>
        bool IsDcGameAlreadyOnCard(string source, DcCardIndex index)
        {
            try
            {
                if (!TryExtractDcSerialFromSource(source, out var serial) || string.IsNullOrWhiteSpace(serial)) return false;
                if (!index.SerialDiscs.TryGetValue(serial, out var discs)) return false;
                index.Serials.TryGetValue(serial, out var folder);

                if (TryGuessDiscKey(Path.GetFileName(source) ?? string.Empty, source, out var dk))
                {
                    var discNo = dk.Split('/')[0] + "/";
                    if (discs.Contains(dk) || discs.Any(v => v.StartsWith(discNo, StringComparison.OrdinalIgnoreCase)))
                    {
                        Info($"[SKIP] Already present as \\{folder} (serial {serial}, disc {dk})");
                        return true;
                    }
                }
                else if (discs.Contains("1/1"))
                {
                    Info($"[SKIP] Already present as \\{folder} (serial {serial})");
                    return true;
                }
            }
            catch (IOException ex) { Warn($"Could not read serial of '{Path.GetFileName(source)}': {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { Warn($"Could not read serial of '{Path.GetFileName(source)}': {ex.Message}"); }
            return false;
        }

        /// <summary>
        /// Copies one game (a folder, or an image file) into the next free \NN folder, renames the image to
        /// disc.gdi / disc.cdi and writes name.txt, serial.txt and infoNN.txt once.
        /// A .gdi file is copied together with the track files it lists, a .ccd / .mds together with the
        /// files that share its name (.img/.sub, .mdf).
        /// </summary>
        async Task CopyDreamcastGameAsync(string source, string destRoot, CancellationToken token)
        {
            WaitForCopyCooldown(token); // pause for the previous game before starting (and logging) this one
            token.ThrowIfCancellationRequested();

            bool isDir = Directory.Exists(source);
            if (!isDir && !File.Exists(source)) { Info($"[SKIP] Not found: '{source}'"); return; }

            var dcDest = CreateDcGameFolder(destRoot); // next free 02..9999; removed again if the copy fails
            var idx = Path.GetFileName(dcDest);

            if (isDir)
            {
                Info($"Copying Dreamcast folder → '{Path.GetFileName(source.TrimEnd('\\', '/'))}' → {idx}");
                await RoboCopyDir(source, dcDest, token);
            }
            else
            {
                var files = DcImageFileSet(source);
                var extra = files.Count > 1 ? $" (+{files.Count - 1} track file(s))" : "";
                Info($"Copying Dreamcast file → '{Path.GetFileName(source)}'{extra} → {idx}");
                foreach (var f in files) await RoboCopyFile(f, dcDest, token);
            }

            RenameDcImageToDisc(dcDest, ".gdi");
            RenameDcImageToDisc(dcDest, ".cdi");
            WriteDcMetadata(dcDest);
            CopyCooldown(token); // game complete: keep the folder, pause before the next write
        }

        /// <summary>The image file plus the files it needs: .gdi → listed tracks, .ccd/.mds → same-named companions.</summary>
        static List<string> DcImageFileSet(string imagePath)
        {
            var files = new List<string> { imagePath };
            var dir = Path.GetDirectoryName(imagePath) ?? "";
            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            try
            {
                if (ext == ".gdi")
                {
                    // "3 45000 4 2352 track03.bin 0" — file names may be quoted and contain spaces.
                    foreach (var line in File.ReadLines(imagePath).Skip(1))
                    {
                        var parts = Regex.Matches(line.Trim(), "\"[^\"]*\"|\\S+").Select(m => m.Value.Trim('"')).ToArray();
                        if (parts.Length < 5) continue;
                        var track = Path.Combine(dir, parts[4]);
                        if (File.Exists(track) && !files.Contains(track, StringComparer.OrdinalIgnoreCase)) files.Add(track);
                    }
                }
                else if (ext == ".ccd" || ext == ".mds")
                {
                    var stem = Path.GetFileNameWithoutExtension(imagePath);
                    foreach (var f in Directory.GetFiles(dir, stem + ".*"))
                        if (!files.Contains(f, StringComparer.OrdinalIgnoreCase)) files.Add(f);
                }
            }
            // Unreadable image: only the image file itself is copied; robocopy then reports why it fails.
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return files;
        }

        /// <summary>GDEMU wants the image named disc.gdi / disc.cdi; rename when there is exactly one of that type.</summary>
        void RenameDcImageToDisc(string dcDest, string ext)
        {
            try
            {
                var images = Directory.GetFiles(dcDest, "*" + ext, SearchOption.TopDirectoryOnly);
                if (images.Length != 1) return;
                var target = Path.Combine(dcDest, "disc" + ext);
                if (string.Equals(Path.GetFileName(images[0]), "disc" + ext, StringComparison.OrdinalIgnoreCase)) return;
                File.Move(images[0], target);
            }
            catch (IOException ex) { Warn($"Could not rename image to disc{ext}: {ex.Message}"); }
        }

        /// <summary>Writes name.txt, serial.txt and infoNN.txt for a game folder (once per game).</summary>
        void WriteDcMetadata(string dcDest)
        {
            try
            {
                if (!TryExtractDcIpMetadata(dcDest, out var title, out var serial))
                {
                    // A .cdi has no readable IP.BIN here, so only a .gdi without info is worth reporting.
                    if (File.Exists(Path.Combine(dcDest, "disc.gdi")))
                        Warn($"No game info (IP.BIN) found in \\{Path.GetFileName(dcDest)}: name.txt / serial.txt not written.");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(title)) File.WriteAllText(Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(title));
                if (!string.IsNullOrWhiteSpace(serial)) File.WriteAllText(Path.Combine(dcDest, "serial.txt"), serial);
                WriteDcInfoFile(dcDest, title ?? "", serial ?? "");
            }
            // The game itself is on the card; missing info files are reported, the job continues with the next game.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Warn($"Could not write game info in \\{Path.GetFileName(dcDest)}: {ex.Message}");
            }
        }
    }
}
