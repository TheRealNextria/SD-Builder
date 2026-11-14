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
        System.Windows.Forms.Timer? _glPoll;
        string _glLastSig = string.Empty;
        FileSystemWatcher? _glWatcher;
        System.Windows.Forms.Timer? _glDebounce;
        void RefreshAllGameLists()
        {
            try { PopulateListCombo(cmbXstationList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            try { PopulateListCombo(cmbSarooList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            try { PopulateListCombo(cmbGamecubeList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            try { PopulateListCombo(cmbDreamcastList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            try { PopulateListCombo(cmbSC64List); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }


        string GetGameListsSignature()
        {
            try
            {
                if (!Directory.Exists(GameListsDir)) return "";
                var files = Directory.EnumerateFiles(GameListsDir, "*.txt", SearchOption.TopDirectoryOnly)
                                     .Select(p => new FileInfo(p))
                                     .Select(fi => $"{fi.Name}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}")
                                     .OrderBy(x => x);
                return string.Join(";", files);
            }
            catch { return ""; }
        }

        void WirePlatformTab(
            Platform p,
            Button btnFw, ComboBox cmbList, Button btnRefreshLists, Button btnOpenLists, Button btnStart,
            Button? btnInstall64DD, Button? btnInstallEmu)
        {
            btnRefreshLists.Click += (_, __) => { PopulateListCombo(cmbList); btnStart.Enabled = cmbList.Items.Count > 0; };
            cmbList.SelectedIndexChanged += (_, __) => btnStart.Enabled = cmbList.SelectedIndex >= 0;
            btnOpenLists.Click += (_, __) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{GameListsDir}\"") { UseShellExecute = true });

            PopulateListCombo(cmbList);
            btnStart.Enabled = cmbList.Items.Count > 0;

            btnFw.Click += async (_, __) => await WithDriveJobAsync(async (d, token) =>
            {
                switch (p)
                {
                    case Platform.Xstation: await EnsureXstationFirmware(d, token); break;
                    case Platform.Saroo: await EnsureSarooFirmware(d, token); break;
                    case Platform.Gamecube: await EnsureGamecubeIPL(d, token); break;
                    case Platform.Dreamcast:
                        {
                            bool useGd = (rbDcGdMenu != null && rbDcGdMenu.Checked);
                            string chosenName = useGd ? "GDmenu" : "OpenMenu";
                            var ans = MessageBox.Show(
                                this,
                                $"Do you want to install {chosenName} firmware to " + d + ":\01?",
                                "Install firmware",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question
                            );
                            if (ans != DialogResult.Yes) break;
                            await EnsureDreamcastFirmwareSelectedAsync(d, token);
                            break;
                        }

                    case Platform.Summercart64: await EnsureSummercart64Firmware(d, token); break;
                }
            });

            btnStart.Click += async (_, __) =>
            {
                var list = GetSelectedListPath(cmbList);
                if (list == null) { Warn("No list selected."); return; }
                await WithDriveJobAsync((d, token) => ReadListAndCopy(list, p, d, token));
            };

            if (p == Platform.Summercart64 && btnInstall64DD != null && btnInstallEmu != null)
            {
                btnInstall64DD.Click += async (_, __) => await WithDriveJobAsync(async (d, token) =>
                {
                    await InstallSummercart64Ipl(d, token);
                });
                btnInstallEmu.Click += async (_, __) => await WithDriveJobAsync(async (d, token) =>
                {
                    await InstallSummercart64Emulators(d, token);
                });
            }
        }



        // List-driven copy
        async Task ReadListAndCopy(string listPath, Platform platform, string destDrive, CancellationToken token)
        {
            if (!File.Exists(listPath)) throw new Exception($"List file not found: {listPath}");

            string destRoot = platform switch
            {
                Platform.Xstation => $@"{destDrive}:\",
                Platform.Saroo => $@"{destDrive}:\SAROO\ISO",
                Platform.Gamecube => $@"{destDrive}:\Games",
                Platform.Summercart64 => $@"{destDrive}:\Roms",
                Platform.Dreamcast => $@"{destDrive}:\",
                _ => throw new InvalidOperationException()
            };
            Directory.CreateDirectory(destRoot);
            Info($"Destination path: {destRoot}");


            var existingSerials = platform == Platform.Dreamcast
                ? IndexDcSerials(destRoot)
                : new System.Collections.Generic.Dictionary<string, string>();

            var existingSerialDiscs = platform == Platform.Dreamcast
                ? IndexDcSerialDiscs(destRoot)
                : new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>();
            foreach (var raw in File.ReadAllLines(listPath))
            {
                token.ThrowIfCancellationRequested();
                if (raw == null) continue;
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (Regex.IsMatch(line, @"^\s*(#|;|//|::|REM\b)")) continue;
                if (line.StartsWith("\"") && line.EndsWith("\"") && line.Length >= 2) line = line[1..^1];
                line = line.TrimEnd(' ', '.');
                if (!File.Exists(line) && !Directory.Exists(line)) { Info($"[SKIP] Not found: \"{line}\""); continue; }

                // Dreamcast duplicate-by-serial skip for Start (list)
                if (platform == Platform.Dreamcast)
                {
                    try
                    {
                        if (TryExtractDcSerialFromSource(line, out var srcSerial) && !string.IsNullOrWhiteSpace(srcSerial))
                        {
                            if (existingSerialDiscs.TryGetValue(srcSerial, out var setDiscs))
                            {
                                string dk;
                                if (TryGuessDiscKey(System.IO.Path.GetFileName(line) ?? string.Empty, line, out dk))
                                {
                                    if (setDiscs.Contains(dk) || System.Linq.Enumerable.Any(setDiscs, v => v.StartsWith(dk.Split('/')[0] + "/", System.StringComparison.OrdinalIgnoreCase)))
                                    {
                                        Info(string.Format("[SKIP] Already present as \\\\{0} (serial {1}, disc {2})", existingSerials[srcSerial], srcSerial, dk));
                                        WaitEnterOrTimeout(CopyTimeout, token);
                                        continue;
                                    }
                                }
                                else if (setDiscs.Contains("1/1"))
                                {
                                    Info(string.Format("[SKIP] Already present as \\\\{0} (serial {1})", existingSerials[srcSerial], srcSerial));
                                    WaitEnterOrTimeout(CopyTimeout, token);
                                    continue;
                                }
                            }
                        }
                    }
                    catch { /* ignore */ }
                }
                if (token.IsCancellationRequested) return;
                await CopyEntry(line, platform, destRoot, token);
            }
            Info("All entries processed.");
        }

        async Task CopyEntry(string source, Platform platform, string destRoot, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;


            // Dreamcast: always copy into next free 02..9999 index folder and rename disc
            if (platform == Platform.Dreamcast)
            {
                if (Directory.Exists(source))
                {
                    var dcDest = NextDreamcastIndexFolder(destRoot);
                    Directory.CreateDirectory(dcDest);

                    var name = new DirectoryInfo(source).Name;
                    Info($"Copying Dreamcast folder → '{name}' → {Path.GetFileName(dcDest)}");
                    await RoboCopyDir(source, dcDest, token);

                    // Rename any top-level .gdi to disc.gdi
                    try
                    {
                        var gdis = Directory.GetFiles(dcDest, "*.gdi", SearchOption.TopDirectoryOnly);
                        if (gdis.Length > 0)
                        {
                            var gdiSrc = gdis[0];
                            var gdiDst = Path.Combine(dcDest, "disc.gdi");
                            if (!string.Equals(Path.GetFileName(gdiSrc), "disc.gdi", StringComparison.OrdinalIgnoreCase))
                            {
                                if (File.Exists(gdiDst)) File.Delete(gdiDst);
                                File.Move(gdiSrc, gdiDst);
                            }
                        }
                    }
                    catch { /* ignore */ }

                    try { { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } } } catch { { /* ignore */ } }
                    WaitEnterOrTimeout(CopyTimeout, token);
                    try { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } catch { /* ignore */ }
                    return;
                }
                else if (File.Exists(source))
                {
                    var dcDest = NextDreamcastIndexFolder(destRoot);
                    Directory.CreateDirectory(dcDest);

                    var leaf = Path.GetFileName(source);
                    var ext = Path.GetExtension(source).ToLowerInvariant();

                    if (ext == ".cdi")
                    {
                        var target = Path.Combine(dcDest, "disc.cdi");
                        var dec = ConfirmReplaceCountdown(target, OverwriteTimeout, $"{platform} file '{leaf}'", token: token);
                        if (dec == ReplaceDecision.No) { Info($"[SKIP] {leaf}"); return; }
                        if (dec == ReplaceDecision.Cancel) throw new OperationCanceledException();

                        Info($"Copying file → '{leaf}' → Dreamcast → {Path.GetFileName(dcDest)}");
                        await RoboCopyFile(source, dcDest, token);

                        var copied = Path.Combine(dcDest, leaf);
                        if (!string.Equals(leaf, "disc.cdi", StringComparison.OrdinalIgnoreCase) && File.Exists(copied))
                        {
                            if (File.Exists(target)) File.Delete(target);
                            File.Move(copied, target);
                        }
                        try { { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } } } catch { { /* ignore */ } }
                        WaitEnterOrTimeout(CopyTimeout, token);
                        try { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } catch { /* ignore */ }
                        return;
                    }
                    else if (ext == ".gdi")
                    {
                        var target = Path.Combine(dcDest, "disc.gdi");
                        var dec = ConfirmReplaceCountdown(target, OverwriteTimeout, $"{platform} file '{leaf}'", token: token);
                        if (dec == ReplaceDecision.No) { Info($"[SKIP] {leaf}"); return; }
                        if (dec == ReplaceDecision.Cancel) throw new OperationCanceledException();

                        Info($"Copying file → '{leaf}' → Dreamcast → {Path.GetFileName(dcDest)}");
                        await RoboCopyFile(source, dcDest, token);

                        var copied = Path.Combine(dcDest, leaf);
                        if (!string.Equals(leaf, "disc.gdi", StringComparison.OrdinalIgnoreCase) && File.Exists(copied))
                        {
                            if (File.Exists(target)) File.Delete(target);
                            File.Move(copied, target);
                        }
                        try { { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } } } catch { { /* ignore */ } }
                        WaitEnterOrTimeout(CopyTimeout, token);
                        try { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } catch { /* ignore */ }
                        return;
                    }
                    else
                    {
                        var target = Path.Combine(dcDest, leaf);
                        var dec = ConfirmReplaceCountdown(target, OverwriteTimeout, $"{platform} file '{leaf}'", token: token);
                        if (dec == ReplaceDecision.No) { Info($"[SKIP] {leaf}"); return; }
                        if (dec == ReplaceDecision.Cancel) throw new OperationCanceledException();

                        Info($"Copying file → '{leaf}' → Dreamcast → {Path.GetFileName(dcDest)}");
                        await RoboCopyFile(source, dcDest, token);
                        try { { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } } } catch { { /* ignore */ } }
                        WaitEnterOrTimeout(CopyTimeout, token);
                        try { if (TryExtractDcIpMetadata(dcDest, out var t, out var sn)) { if (!string.IsNullOrWhiteSpace(t)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "name.txt"), SanitizeMenuTitle32(t)); try { WriteDcInfoFile(dcDest, t, sn); } catch { } if (!string.IsNullOrWhiteSpace(sn)) System.IO.File.WriteAllText(System.IO.Path.Combine(dcDest, "serial.txt"), sn); try { WriteDcInfoFile(dcDest, t, sn); } catch { } } } catch { /* ignore */ }
                        return;
                    }
                }

                Info($"[SKIP] Not found: '{source}'");
                return;
            }





            if (Directory.Exists(source))
            {
                var name = new DirectoryInfo(source).Name;
                if (platform == Platform.Gamecube)
                {
                    var dest = Path.Combine(destRoot, name);
                    bool needsPrompt = Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any();
                    if (needsPrompt)
                    {
                        var d = ConfirmReplaceCountdown(dest, OverwriteTimeout, $"Folder \'{name}\'", folderPromptOnlyWhenNonEmpty: true, token: token);
                        if (d == ReplaceDecision.No) { Info($"[SKIP] Folder \'{name}\'"); return; }
                        if (d == ReplaceDecision.Cancel) throw new OperationCanceledException();
                    }
                    Directory.CreateDirectory(dest);
                    Info($"Copying folder → '{name}' → {GetPlatformCopyLabel(platform)}");
                    await RoboCopyDir(source, dest, token);
                    WaitEnterOrTimeout(CopyTimeout, token);
                    return;
                }
                else
                {
                    var dest = Path.Combine(destRoot, name);
                    bool needsPrompt = Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any();
                    if (needsPrompt)
                    {
                        var d = ConfirmReplaceCountdown(dest, OverwriteTimeout, $"{platform} folder '{name}'", folderPromptOnlyWhenNonEmpty: true, token: token);
                        if (d == ReplaceDecision.No) { Info($"[SKIP] Folder '{name}'"); return; }
                        if (d == ReplaceDecision.Cancel) throw new OperationCanceledException();
                    }
                    Directory.CreateDirectory(dest);
                    Info($"Copying folder → '{Path.GetFileName(source.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))}' → {GetPlatformCopyLabel(platform)}");
                    await RoboCopyDir(source, dest, token);
                    WaitEnterOrTimeout(CopyTimeout, token);
                    return;
                }
            }

            if (File.Exists(source))
            {
                var leaf = Path.GetFileName(source);
                string target = platform switch
                {
                    Platform.Gamecube => Path.Combine(destRoot, leaf),
                    Platform.Summercart64 => Path.Combine(destRoot, leaf),
                    _ => Path.Combine(Path.Combine(destRoot, Path.GetFileNameWithoutExtension(source)), leaf),
                };

                var dec = ConfirmReplaceCountdown(target, OverwriteTimeout, $"{platform} file '{leaf}'", token: token);
                if (dec == ReplaceDecision.No) { Info($"[SKIP] {leaf}"); return; }
                if (dec == ReplaceDecision.Cancel) throw new OperationCanceledException();

                Info($"Copying file → '{leaf}'→ {GetPlatformCopyLabel(platform)}");
                var dstDir = Path.GetDirectoryName(target)!;
                await RoboCopyFile(source, dstDir, token);
                WaitEnterOrTimeout(CopyTimeout, token);
                return;
            }

            Info($"[SKIP] Not found: '{source}'");
        }

        // List combo helpers
        void PopulateListCombo(ComboBox combo)
        {
            combo.Items.Clear();
            try
            {
                Directory.CreateDirectory(GameListsDir);
                var files = Directory.EnumerateFiles(GameListsDir, "*.txt", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileNameWithoutExtension).ToList();
                foreach (var f in files) combo.Items.Add(new ComboItem(Path.GetFileNameWithoutExtension(f), f));
                if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            }
            catch (Exception ex) { Warn("Could not enumerate GameLists: " + ex.Message); }
        }
        string? GetSelectedListPath(ComboBox combo) => combo.SelectedItem is ComboItem ci ? ci.FullPath : null;
        sealed class ComboItem
        {
            public string Text { get; }
            public string FullPath { get; }
            public ComboItem(string text, string fullPath) { Text = text; FullPath = fullPath; }
            public override string ToString() => Text;
        }
    }
}
