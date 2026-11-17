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
        const int GcStatusColWidth = 160;

        // Gamecube state
        string _gcFolder = string.Empty;
        bool _gcBulkChange = false;
        HashSet<string> _gcChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _gcFilterDebounce;

        CancellationTokenSource? _gcScanCts;
        const string G_Url = "https://github.com/emukidid/swiss-gc/releases/download/v0.6r1913/swiss_r1913.7z";
        const string GC_CHEATS_Url = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/GC.Swiss.cheats.zip";
        const string G_Owner = "emukidid";
        const string G_Repo = "swiss-gc";
        const string G_AssetPattern = @"^swiss_r\d+\.(?:7z|zip)$";
        const string G_7zr_Url = "https://www.7-zip.org/a/7zr.exe";

        // Swiss temp
        string G_Work => Path.Combine(TempRoot, "gamecube");
        string G_7z => Path.Combine(G_Work, "swiss.7z");
        string G_Extract => Path.Combine(G_Work, "extracted");
        string G_7zr => Path.Combine(G_Work, "7zr.exe");

        // -------- Gamecube Listmaker (mirrors Xstation) --------
        void WireGamecubeListmaker()
        {
            // columns
            // ===== GameCube UI wiring START =====
            lvGcList.View = View.Details;
            lvGcList.FullRowSelect = true;
            lvGcList.HideSelection = false;
            if (lvGcList.Columns.Count < 2)
            {
                lvGcList.Columns.Clear();
                lvGcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvGcList.Columns.Add("Size", GcStatusColWidth, HorizontalAlignment.Left);
            }
            // ===== END GameCube UI wiring =====

            void AutoSizeGc()
            {
                if (lvGcList.Columns.Count >= 2)
                {
                    int statusWidth = GcStatusColWidth;
                    lvGcList.Columns[0].Width = Math.Max(120, lvGcList.ClientSize.Width - statusWidth - 8);
                    lvGcList.Columns[1].Width = statusWidth;
                }
            }
            lvGcList.Resize += (_, __) => AutoSizeGc();
            tabGamecube.Enter += (_, __) => AutoSizeGc();

            btnGcChoose.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _gcFolder = dlg.SelectedPath;
                    SetGcStatus($"Selected: {_gcFolder}");
                    RefreshGamecubeList();
                }
            };
            rbGcFiles.CheckedChanged += (_, __) => { if (rbGcFiles.Checked) { SetGcStatus("Mode: Files"); RefreshGamecubeList(); } };
            rbGcDirs.CheckedChanged += (_, __) => { if (rbGcDirs.Checked) { SetGcStatus("Mode: Directories"); RefreshGamecubeList(); } };

            lvGcList.ItemChecked += (s, e) =>
            {
                if (_gcBulkChange) return;
                var it = e?.Item as ListViewItem;
                if (it != null)
                {
                    var path = it.Text;
                    if (it.Checked) _gcChecked.Add(path); else _gcChecked.Remove(path);
                }
                UpdateGcCounters();
            };

            btnGcCheckAll.Click += (_, __) =>
            {
                _gcBulkChange = true;
                foreach (ListViewItem it in lvGcList.Items) { it.Checked = true; _gcChecked.Add(it.Text); }
                _gcBulkChange = false;
                UpdateGcCounters();
            };
            btnGcUncheckAll.Click += (_, __) =>
            {
                _gcBulkChange = true;
                foreach (ListViewItem it in lvGcList.Items) { it.Checked = false; _gcChecked.Remove(it.Text); }
                _gcBulkChange = false;
                UpdateGcCounters();
            };
            btnGcStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopyGamecubeCheckedAsync(d, token));
            };
        }



        void SetGcStatus(string text)
        {
            lblStatusGC.Text = text;
        }

        void UpdateGcCounters()
        {
            long total = 0;
            int count = 0;
            foreach (ListViewItem it in lvGcList.Items)
            {
                if (!it.Checked) continue;
                count++;
                if (it.Tag is long sz) total += sz;
            }
            lblCountGC.Text = $"Checked: {count}";
            lblSizeGC.Text = $"Size: {FormatSize(total)}";
        }

        void RefreshGamecubeList() => _ = RefreshGamecubeListAsync();

        /// <summary>Lists the folder on a background thread, fills the list, then works out the sizes (Common\MainForm.ListScan.cs).</summary>
        async Task RefreshGamecubeListAsync()
        {
            var ct = RestartScan(ref _gcScanCts); // a newer refresh cancels this one
            var patterns = BuildFilterPatterns(txtGcFilter?.Text);
            try
            {
                SetGcStatus("Scanning...");
                var paths = await ListFolderAsync(_gcFolder, rbGcFiles.Checked, patterns, ct);
                if (ct.IsCancellationRequested) return;
                FillScanList(lvGcList, paths ?? new List<string>(), _gcChecked, GcStatusColWidth, b => _gcBulkChange = b);
                UpdateGcCounters();
                if (paths == null) { SetGcStatus("Ready."); return; }

                if (await ScanRowSizesAsync(lvGcList, ct, (it, size) => { ShowRowSize(it, size); if (it.Checked) UpdateGcCounters(); }))
                    SetGcStatus($"Ready. {lvGcList.Items.Count} items.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Error("Error refreshing list: " + ex.Message); }
        }

        // ===== GameCube Copy routine START =====
        async Task CopyGamecubeCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\Games";
            Directory.CreateDirectory(destRoot);
            int copied = 0;
            if (!await ConfirmFreeSpaceAsync(destDrive, CheckedPaths(lvGcList), token)) return;

            foreach (ListViewItem it in lvGcList.Items)
            {
                if (!it.Checked) continue;
                var path = it.Text;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Info($"[SKIP] Not found: \"{path}\"");
                    continue;
                }
                await CopyEntry(path, Platform.Gamecube, destRoot, token);
                copied++;
            }

            if (copied == 0) Warn("No items checked.");
        }        // ===== END GameCube Copy routine =====


        async Task InstallGamecubeCheats(string destDrive, CancellationToken token)
        {
            var gcBase = $@"{destDrive}:\Swiss";
            Directory.CreateDirectory(gcBase);
            var cheatsDest = Path.Combine(gcBase, "Cheats");
            Directory.CreateDirectory(cheatsDest);

            EnsureRemoved(G_Work); Directory.CreateDirectory(G_Work);
            var zipPath = Path.Combine(G_Work, "GC.Swiss.cheats.zip");
            Info($"Downloading: {GC_CHEATS_Url}");
            await DownloadFileAsync(GC_CHEATS_Url, zipPath, token);

            var extractDir = Path.Combine(G_Work, "cheats_extracted");
            EnsureRemoved(extractDir); Directory.CreateDirectory(extractDir);
            Info($"Extracting Cheats → {extractDir}");
            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, true);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to extract cheats zip.", ex);
            }

            string? srcCheats = null;
            foreach (var dir in Directory.EnumerateDirectories(extractDir, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(dir), "cheats", StringComparison.OrdinalIgnoreCase))
                {
                    srcCheats = dir; break;
                }
            }
            if (srcCheats == null)
            {
                var maybe = Path.Combine(extractDir, "cheats");
                if (Directory.Exists(maybe)) srcCheats = maybe;
            }
            if (srcCheats == null) throw new Exception("Could not find 'cheats' folder inside the archive.");

            Info($"Copying Cheats → {cheatsDest}");
            await RoboCopyDir(srcCheats, cheatsDest, token);
            Info($"Cheat files installed → {cheatsDest}");
        }
        async Task EnsureGamecubeIPL(string destDrive, CancellationToken token)
        {
            var gcBase = $@"{destDrive}:\";
            Directory.CreateDirectory(gcBase);
            var ipl = Path.Combine(gcBase, "IPL.dol");
            if (File.Exists(ipl)) { Info("Swiss firmware present."); return; }
            if (MessageBox.Show(this, $"IPL.dol not found in:\n{gcBase}\n\nDownload Swiss now?", "Gamecube", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(G_Work); Directory.CreateDirectory(G_Work);
            var latest = await GetLatestAssetUrlAsync(G_Owner, G_Repo, G_AssetPattern, token);
            var chosen = latest ?? G_Url;
            Info($"Resolved Swiss URL: {chosen}");

            // --- Swiss firmware update check (reads hidden marker if present)
            int ParseR(string s)
            {
                var m = Regex.Match(s ?? string.Empty, @"r(\d+)", RegexOptions.IgnoreCase);
                return m.Success ? int.Parse(m.Groups[1].Value) : -1;
            }
            string markerPath = Path.Combine(gcBase, ".swiss.install.json");
            int latestR = ParseR(chosen);
            int installedR = -1;
            if (File.Exists(markerPath))
            {
                try
                {
                    var json = File.ReadAllText(markerPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("version", out var v))
                    {
                        installedR = ParseR(v.GetString() ?? string.Empty);
                    }
                }
                catch (IOException ex) { Warn($"Could not read the Swiss marker, installed version unknown: {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { Warn($"Could not read the Swiss marker, installed version unknown: {ex.Message}"); }
                catch (JsonException ex) { Warn($"Swiss marker is damaged, installed version unknown: {ex.Message}"); }
                catch (InvalidOperationException ex) { Warn($"Swiss marker is damaged, installed version unknown: {ex.Message}"); } // "version" is not a string
            }
            if (installedR >= 0 && latestR >= 0)
            {
                if (installedR >= latestR) Info($"Swiss: up-to-date (r{installedR}).");
                else Info($"Swiss: update available r{installedR} → r{latestR}.");
            }
            else if (latestR >= 0)
            {
                Info($"Swiss: latest is r{latestR}. No installed version detected.");
            }
            await DownloadFileAsync(chosen, G_7z, token);
            await Ensure7zr(token);
            Directory.CreateDirectory(G_Extract);
            Info($"Extracting 7z → {G_Extract}");
            var rc = await RunProcess(G_7zr, $"x -y -o\"{G_Extract}\" \"{G_7z}\"", token);
            if (rc != 0) throw new Exception($"7zr extraction failed (rc={rc})");

            foreach (var dir in Directory.EnumerateDirectories(G_Extract, "Legacy", SearchOption.AllDirectories))
                try { Directory.Delete(dir, true); Info($"Removed Legacy directory: {dir}"); }
                    catch (IOException ex) { Warn($"Could not remove Legacy directory '{dir}': {ex.Message}"); }
                    catch (UnauthorizedAccessException ex) { Warn($"Could not remove Legacy directory '{dir}': {ex.Message}"); }

            var defaultDol = Path.Combine(G_Extract, @"swiss_r1913\DOL\swiss_r1913.dol");
            string? src = File.Exists(defaultDol) ? defaultDol : Directory.EnumerateFiles(G_Extract, "swiss_*.dol", SearchOption.AllDirectories).FirstOrDefault();
            if (src == null) throw new Exception($"Could not find swiss_*.dol in {G_Extract}");

            Info($"Copying file → '{Path.GetFileName(src)}'");
            await RoboCopyFile(src, gcBase, token);

            var srcLeaf = Path.GetFileName(src);
            var copied = Path.Combine(gcBase, srcLeaf);
            if (!srcLeaf.Equals("IPL.dol", StringComparison.OrdinalIgnoreCase))
            {
                try { if (File.Exists(ipl)) File.Delete(ipl); File.Move(copied, ipl); }
                catch (IOException) { File.Copy(copied, ipl, true); File.Delete(copied); } // rename failed: copy instead
                catch (UnauthorizedAccessException) { File.Copy(copied, ipl, true); File.Delete(copied); }
            }

            var legacyOnSd = Path.Combine(gcBase, "Legacy");
            if (Directory.Exists(legacyOnSd)) try { Directory.Delete(legacyOnSd, true); Info("Removed Legacy directory from SD root."); }
                catch (IOException ex) { Warn($"Could not remove '{legacyOnSd}': {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { Warn($"Could not remove '{legacyOnSd}': {ex.Message}"); }
            Info($"IPL.dol installed → {gcBase}");

            // --- Write hidden marker with install metadata
            try
            {
                using var sha = SHA256.Create();
                using var fs = File.OpenRead(ipl);
                var hash = sha.ComputeHash(fs);
                var shaHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                var verR = Regex.Match(chosen ?? string.Empty, @"r(\d+)", RegexOptions.IgnoreCase);
                var verStr = verR.Success ? $"r{verR.Groups[1].Value}" : "unknown";

                var payload = new
                {
                    version = verStr,
                    url = chosen,
                    installedUtc = DateTime.UtcNow.ToString("o"),
                    sha256 = shaHex
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(Path.Combine(gcBase, ".swiss.install.json"), json);
                try
                {
                    var mp = Path.Combine(gcBase, ".swiss.install.json");
                    if (File.Exists(mp))
                    {
                        var attrs = File.GetAttributes(mp);
                        File.SetAttributes(mp, attrs | FileAttributes.Hidden);
                    }
                }
                catch (IOException) { }                    // only cosmetic: the marker works when visible too
                catch (UnauthorizedAccessException) { }
                Info("Swiss marker written (.swiss.install.json).");
            }
            catch (IOException ex) { Warn($"Could not write Swiss marker (update check will not know the installed version): {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { Warn($"Could not write Swiss marker (update check will not know the installed version): {ex.Message}"); }
            EnsureRemoved(G_Work);
        }
        async Task Ensure7zr(CancellationToken token)
        {
            if (!File.Exists(G_7zr)) { Info($"Downloading 7zr.exe → {G_7zr}"); await DownloadFileAsync(G_7zr_Url, G_7zr, token); }
        }

        private void btnGcCheckAll_Click(object sender, EventArgs e)
        {

        }
    }
}
