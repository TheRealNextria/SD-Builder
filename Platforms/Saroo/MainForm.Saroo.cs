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
        HashSet<string> _sarooChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Saroo state
        string _srFolder = string.Empty;
        bool _srBulkChange = false;
        HashSet<string> _srChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _srFilterDebounce;
        CancellationTokenSource? _srScanCts;
        const string S_Url = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/Saroo.latest.zip";

        // Saroo temp
        string S_Work => Path.Combine(TempRoot, "saroo");
        string S_Zip => Path.Combine(S_Work, "saroo_fw.zip");
        string S_Extract => Path.Combine(S_Work, "extracted");


        void SetSrStatus(string text)
        {
            lblStatusSR.Text = text;
        }

        void WireSarooListmaker()
        {
            // columns
            lvSrList.View = View.Details;
            lvSrList.FullRowSelect = true;
            lvSrList.HideSelection = false;
            if (lvSrList.Columns.Count < 2)
            {
                lvSrList.Columns.Clear();
                lvSrList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvSrList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
            }

            void AutoSizeSr()
            {
                if (lvSrList.Columns.Count >= 2)
                {
                    int statusWidth = XsStatusColWidth;
                    lvSrList.Columns[0].Width = Math.Max(120, lvSrList.ClientSize.Width - statusWidth - 8);
                    lvSrList.Columns[1].Width = statusWidth;
                }
            }
            lvSrList.Resize += (_, __) => AutoSizeSr();
            tabSaroo.Enter += (_, __) => AutoSizeSr();

            // Saroo search debounce + wiring
            _srFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _srFilterDebounce.Tick += (_, __) => { _srFilterDebounce!.Stop(); RefreshSarooList(); };
            if (txtSarooFilter != null)
            {
                txtSarooFilter.TextChanged += (_, __) => { _srFilterDebounce?.Stop(); _srFilterDebounce?.Start(); };
                txtSarooFilter.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        _srFilterDebounce?.Stop();
                        RefreshSarooList();
                    }
                };
            }

            // Saroo clear button
            if (btnSarooClearFilter != null && txtSarooFilter != null)
            {
                btnSarooClearFilter.Click += (_, __) =>
                {
                    txtSarooFilter.Text = string.Empty;
                    _srFilterDebounce?.Stop();
                    RefreshSarooList();
                };
            }


            btnSrChoose.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _srFolder = dlg.SelectedPath;
                    SetSrStatus($"Selected: {_srFolder}");
                    RefreshSarooList();
                }
            };
            rbSrFiles.CheckedChanged += (_, __) => { if (rbSrFiles.Checked) { SetSrStatus("Mode: Files"); RefreshSarooList(); } };
            rbSrDirs.CheckedChanged += (_, __) => { if (rbSrDirs.Checked) { SetSrStatus("Mode: Directories"); RefreshSarooList(); } };

            lvSrList.ItemChecked += (s, e) =>
            {
                if (_srBulkChange) return;
                var it = e?.Item as ListViewItem;
                if (it != null)
                {
                    var path = it.Text;
                    if (it.Checked) _srChecked.Add(path); else _srChecked.Remove(path);
                }
                UpdateSrCounters();
            };

            btnSrCheckAll.Click += (_, __) =>
            {
                _srBulkChange = true;
                foreach (ListViewItem it in lvSrList.Items) { it.Checked = true; _srChecked.Add(it.Text); }
                _srBulkChange = false;
                UpdateSrCounters();
            };
            btnSrUncheckAll.Click += (_, __) =>
            {
                _srBulkChange = true;
                foreach (ListViewItem it in lvSrList.Items) { it.Checked = false; _srChecked.Remove(it.Text); }
                _srBulkChange = false;
                UpdateSrCounters();
            };
            btnSrStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopySarooCheckedAsync(d, token));
            };
        }


        void RefreshSarooList() => _ = RefreshSarooListAsync();

        /// <summary>Lists the folder on a background thread, fills the list, then works out the sizes (Common\MainForm.ListScan.cs).</summary>
        async Task RefreshSarooListAsync()
        {
            var ct = RestartScan(ref _srScanCts); // a newer refresh cancels this one
            var patterns = BuildFilterPatterns(txtSarooFilter?.Text);
            try
            {
                SetSrStatus("Scanning...");
                var paths = await ListFolderAsync(_srFolder, rbSrFiles.Checked, patterns, ct);
                if (ct.IsCancellationRequested) return;
                FillScanList(lvSrList, paths ?? new List<string>(), _srChecked, XsStatusColWidth, b => _srBulkChange = b);
                UpdateSrCounters();
                if (paths == null) { SetSrStatus("Ready."); return; }

                if (await ScanRowSizesAsync(lvSrList, ct, (it, size) => { ShowRowSize(it, size); if (it.Checked) UpdateSrCounters(); }))
                    SetSrStatus($"Ready. {lvSrList.Items.Count} items.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Error("Saroo list error: " + ex.Message); }
        }

        void UpdateSrCounters()
        {
            long total = 0;
            int count = 0;
            foreach (ListViewItem it in lvSrList.Items)
            {
                if (!it.Checked) continue;
                count++;
                if (it.Tag is long sz) total += sz;
            }
            lblCountSR.Text = $"Checked: {count}";
            lblSizeSR.Text = $"Size: {FormatSize(total)}";
        }

        // ===== Saroo Copy routine START =====
        Task CopySarooCheckedAsync(string destDrive, CancellationToken token) =>
            CopyCueGroupsCheckedAsync(destDrive, $@"{destDrive}:\Saroo\ISO\", "Saroo", Platform.Saroo, lvSrList, rbSrFiles.Checked, token); // Common\MainForm.CueGroupCopy.cs

        async Task EnsureSarooFirmware(string destDrive, CancellationToken token)
        {
            var sarooBase = $@"{destDrive}:\SAROO";
            var expected = new[] { "cover.bin", "mcuapp.bin", "saroocfg.txt", "ssfirm.bin" }
                .Select(f => Path.Combine(sarooBase, f))
                .ToArray();

            var mainFirmwarePresent = expected.All(File.Exists);
            var dstUpdate = Path.Combine(sarooBase, "update");
            var dstSsmaster = Path.Combine(dstUpdate, "SSMaster.rbf");

            if (mainFirmwarePresent && File.Exists(dstSsmaster))
            {
                Info("SAROO firmware present.");
                return;
            }

            if (mainFirmwarePresent)
            {
                if (MessageBox.Show(this,
                    $"SAROO firmware is present in:\n{sarooBase}\n\nOptional update file is missing:\nupdate\\SSMaster.rbf\n\nDownload now to repair/copy the update folder?",
                    "SAROO",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.No) return;
            }
            else
            {
                if (MessageBox.Show(this, $"SAROO firmware missing in:\n{sarooBase}\n\nDownload now?", "SAROO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;
            }

            EnsureRemoved(S_Work); Directory.CreateDirectory(S_Work);
            await DownloadFileAsync(S_Url, S_Zip, token);
            ExpandZip(S_Zip, S_Extract);

            string? FindFileIgnoreCase(string folder, string fileName)
            {
                try
                {
                    return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                }
                catch (IOException) { return null; }           // unreadable folder: treated as "not found"
                catch (UnauthorizedAccessException) { return null; }
            }

            string? FindChildDirectoryIgnoreCase(string folder, string dirName)
            {
                try
                {
                    return Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(d => string.Equals(Path.GetFileName(d), dirName, StringComparison.OrdinalIgnoreCase));
                }
                catch (IOException) { return null; }
                catch (UnauthorizedAccessException) { return null; }
            }

            bool ContainsMainSarooFiles(string folder)
            {
                return FindFileIgnoreCase(folder, "cover.bin") != null
                    && FindFileIgnoreCase(folder, "mcuapp.bin") != null
                    && FindFileIgnoreCase(folder, "saroocfg.txt") != null
                    && FindFileIgnoreCase(folder, "ssfirm.bin") != null;
            }

            void PatchSarooConfigLanguage()
            {
                var cfg = Path.Combine(sarooBase, "saroocfg.txt");
                if (!File.Exists(cfg)) return;

                try
                {
                    var bytes = File.ReadAllBytes(cfg);
                    var pat = Encoding.ASCII.GetBytes("lang_id = 0");
                    int idx = IndexOf(bytes, pat);
                    if (idx >= 0)
                    {
                        bytes[idx + pat.Length - 1] = 0x31;
                        File.WriteAllBytes(cfg, bytes);
                        Info("saroocfg.txt patched (lang_id = 1) without re-encoding.");
                    }
                    else Info("'lang_id = 0' not found; no change made.");
                }
                catch (Exception ex) { Warn("Could not patch saroocfg.txt: " + ex.Message); }
            }

            // Locate the SAROO firmware folder. Supports firm/SAROO, firm/saroo, or nested archive layouts.
            var srcSaroo = Directory.GetDirectories(S_Extract, "*", SearchOption.AllDirectories)
                .FirstOrDefault(d =>
                    string.Equals(Path.GetFileName(d), "SAROO", StringComparison.OrdinalIgnoreCase) &&
                    ContainsMainSarooFiles(d));

            if (srcSaroo == null)
                throw new Exception("Missing SAROO firmware folder in downloaded ZIP.");

            var srcUpdate = FindChildDirectoryIgnoreCase(srcSaroo, "update");
            var srcSsmaster = srcUpdate == null ? null : FindFileIgnoreCase(srcUpdate, "SSMaster.rbf");

            Directory.CreateDirectory(sarooBase);

            if (mainFirmwarePresent)
            {
                if (srcUpdate != null && srcSsmaster != null && !File.Exists(dstSsmaster))
                {
                    Directory.CreateDirectory(dstUpdate);
                    Info("SAROO firmware present, copying missing update folder...");
                    token.ThrowIfCancellationRequested();
                    await RoboCopyDir(srcUpdate, dstUpdate, token);

                    if (File.Exists(dstSsmaster)) Info("SAROO update folder installed.");
                    else Warn("SAROO update folder was copied, but SSMaster.rbf is still missing.");
                }
                else if (srcSsmaster == null)
                {
                    Info("SAROO firmware present. No update\\SSMaster.rbf found in downloaded ZIP.");
                }
                else
                {
                    Info("SAROO firmware present.");
                }

                EnsureRemoved(S_Work);
                return;
            }

            var decision = ConfirmReplaceCountdown(sarooBase, OverwriteTimeout, "SAROO folder", folderPromptOnlyWhenNonEmpty: true, token: token);
            if (decision == ReplaceDecision.No) { Info("[SKIP] SAROO folder"); EnsureRemoved(S_Work); return; }
            if (decision == ReplaceDecision.Cancel) throw new OperationCanceledException();

            Info("Copying SAROO firmware...");
            token.ThrowIfCancellationRequested();
            await RoboCopyDir(srcSaroo, sarooBase, token);

            if (expected.All(File.Exists))
            {
                Info($"SAROO firmware installed/updated → {sarooBase}");
                PatchSarooConfigLanguage();
            }
            else Warn("Some SAROO files still missing.");

            EnsureRemoved(S_Work);
        }
    }
}
