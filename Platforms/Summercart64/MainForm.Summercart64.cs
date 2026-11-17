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
        string _scFolder = string.Empty;
        bool _scBulkChange = false;
        HashSet<string> _scChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _scFilterDebounce;
        CancellationTokenSource? _scScanCts;
        // Repo moved to n64brew and the "rolling_release" tag is gone; "latest" always points at the newest stable release.
        const string SC64_Url = "https://github.com/n64brew/N64FlashcartMenu/releases/latest/download/sc64menu.n64";
        const string SC64_64DD_Url = "https://64dd.org/download/sc64_64ddipl.zip";

        // ---- Summercart64 Emulator URLs
        const string SC64_EMU_GB = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/gb.v64";
        const string SC64_EMU_GBC = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/gbc.v64";
        const string SC64_EMU_NEON = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/neon64bu.rom";
        const string SC64_EMU_PF = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/Press-F.z64";
        const string SC64_EMU_SMS = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/smsPlus64.z64";
        const string SC64_EMU_SOD = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/sodium64.z64";


        void WireSummercartListmaker()
        {
            // Mirrors Xstation: rebuild list for current folder + mode
            void RefreshScList() => _ = RefreshScListAsync();

            // Lists the folder on a background thread, fills the list, then works out the sizes (Common\MainForm.ListScan.cs).
            async Task RefreshScListAsync()
            {
                var ct = RestartScan(ref _scScanCts); // a newer refresh cancels this one
                var patterns = BuildFilterPatterns(txtScFilter?.Text);
                try
                {
                    SetScStatus("Scanning...");
                    var paths = await ListFolderAsync(_scFolder, rbScFiles.Checked, patterns, ct);
                    if (ct.IsCancellationRequested) return;
                    FillScanList(lvScList, paths ?? new List<string>(), _scChecked, 160, b => _scBulkChange = b);
                    UpdateScCounters();
                    if (paths == null) { SetScStatus("Ready."); return; }
                    AutoSizeSc();

                    if (await ScanRowSizesAsync(lvScList, ct, (it, size) =>
                        {
                            it.Tag = size ?? 0;
                            it.SubItems[1].Text = size.HasValue ? FormatSize(size.Value) : "Missing";
                            if (it.Checked) UpdateScCounters();
                        }))
                        SetScStatus($"Ready. {lvScList.Items.Count} items.");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Error("Summercart64 list error: " + ex.Message); }
            }

            // columns
            lvScList.View = View.Details;
            lvScList.FullRowSelect = true;
            lvScList.HideSelection = false;
            if (lvScList.Columns.Count < 2)
            {
                lvScList.Columns.Clear();
                lvScList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvScList.Columns.Add("Size", 160, HorizontalAlignment.Left);
            }

            void AutoSizeSc()
            {
                if (lvScList.Columns.Count >= 2)
                {
                    int statusWidth = 160;
                    lvScList.Columns[0].Width = Math.Max(120, lvScList.ClientSize.Width - statusWidth - 8);
                    lvScList.Columns[1].Width = statusWidth;
                }
            }
            lvScList.Resize += (_, __) => AutoSizeSc();
            tabSC64.Enter += (_, __) => AutoSizeSc();

            void SetScStatus(string text)
            {
                lblStatusSC.Text = text;
            }
            void UpdateScCounters()
            {
                int checkedCount = 0;
                long bytes = 0;
                foreach (ListViewItem it in lvScList.Items)
                {
                    if (it.Checked) { checkedCount++; if (it.Tag is long b) bytes += b; }
                }
                lblCountSC.Text = $"Checked: {checkedCount}";
                lblSizeSC.Text = $"Size: {FormatSize(bytes)}";
            }

            btnScChoose.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Choose folder to scan" };
                if (!string.IsNullOrEmpty(_scFolder) && Directory.Exists(_scFolder))
                    dlg.SelectedPath = _scFolder;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _scFolder = dlg.SelectedPath;
                    RefreshScList();
                }
            };


            // SummerCart64 search debounce + wiring
            _scFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _scFilterDebounce.Tick += (_, __) => { _scFilterDebounce!.Stop(); RefreshScList(); };
            if (txtScFilter != null)
            {
                txtScFilter.TextChanged += (_, __) => { _scFilterDebounce?.Stop(); _scFilterDebounce?.Start(); };
                txtScFilter.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        _scFilterDebounce?.Stop();
                        RefreshScList();
                    }
                };
            }
            if (btnScClearFilter != null && txtScFilter != null)
            {
                btnScClearFilter.Click += (_, __) =>
                {
                    txtScFilter.Text = string.Empty;
                    _scFilterDebounce?.Stop();
                    RefreshScList();
                };
            }
            lvScList.ItemChecked += (s, e) =>
                    {
                        if (_scBulkChange) return;
                        var it = e?.Item as ListViewItem;
                        if (it != null)
                        {
                            var path = it.Text;
                            if (it.Checked) _scChecked.Add(path); else _scChecked.Remove(path);
                        }
                        UpdateScCounters();
                    };

            btnScCheckAll.Click += (_, __) =>
            {
                _scBulkChange = true;
                foreach (ListViewItem it in lvScList.Items) { it.Checked = true; _scChecked.Add(it.Text); }
                _scBulkChange = false;
                UpdateScCounters();
            };
            btnScUncheckAll.Click += (_, __) =>
            {
                _scBulkChange = true;
                foreach (ListViewItem it in lvScList.Items) { it.Checked = false; _scChecked.Remove(it.Text); }
                _scBulkChange = false;
                UpdateScCounters();
            };
            btnScStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopySummercartCheckedAsync(d, token));
            };

            rbScFiles.CheckedChanged += (_, __) =>
            {
                if (!rbScFiles.Checked) return;
                RefreshScList();
            };

            rbScDirs.CheckedChanged += (_, __) =>
            {
                if (!rbScDirs.Checked) return;
                RefreshScList();
            };
        }


        async Task CopySummercartCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\Roms";
            Directory.CreateDirectory(destRoot);
            int copied = 0;
            if (!await ConfirmFreeSpaceAsync(destDrive, CheckedPaths(lvScList), token)) return;

            foreach (ListViewItem it in lvScList.Items)
            {
                if (!it.Checked) continue;
                var path = it.Text;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Info($"[SKIP] Not found: \"{path}\"");
                    continue;
                }
                await CopyEntry(path, Platform.Summercart64, destRoot, token);
                copied++;
            }

            if (copied == 0) Warn("No items checked.");
        }
        async Task EnsureSummercart64Firmware(string destDrive, CancellationToken token)
        {
            var root = $@"{destDrive}:\";
            var fw = Path.Combine(root, "sc64menu.n64");
            if (File.Exists(fw)) { Info("Summercart64 firmware present."); return; }
            if (MessageBox.Show(this, $"sc64menu.n64 not found in:\n{root}\n\nDownload now?", "Summercart64", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(TempRoot); Directory.CreateDirectory(TempRoot);
            var tmpFile = Path.Combine(TempRoot, "sc64menu.n64");
            await DownloadFileAsync(SC64_Url, tmpFile, token);

            Info("Copying sc64menu.n64 to SD root...");
            await RoboCopyFile(tmpFile, root, token);
            Info($"sc64menu.n64 installed → {root}");
            EnsureRemoved(TempRoot);
        }
        async Task InstallSummercart64Ipl(string destDrive, CancellationToken token)
        {
            var root = $@"{destDrive}:\";
            var destMenu = System.IO.Path.Combine(root, "menu");
            var ddipl = System.IO.Path.Combine(destMenu, "64ddipl");

            // Required filenames (as you specified)
            string[] required = {
            "NDDE0.n64",
            "NDDJ2.n64",
            "NDXJ0.n64"
};

            // Case-insensitive check for presence
            bool ExistsAll(string baseDir)
            {
                if (!System.IO.Directory.Exists(baseDir)) return false;
                var names = new HashSet<string>(
                    System.IO.Directory.GetFiles(baseDir)
                        .Select(System.IO.Path.GetFileName)
                        .Where(n => n is not null)
                        .Select(n => n!),
                    StringComparer.OrdinalIgnoreCase);
                return required.All(r => names.Contains(r));
            }
            // Skip or prompt
            if (ExistsAll(ddipl))
            {
                Info("64DD IPL already installed.");
                return;
            }

            if (MessageBox.Show(this,
                    $"64DD IPL files not found in:\n{ddipl}\n\nDownload and install now?",
                    "Summercart64 – 64DD IPL",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            var work = System.IO.Path.Combine(TempRoot, "sc64_64ddipl");
            EnsureRemoved(work);
            System.IO.Directory.CreateDirectory(work);
            var zipPath = System.IO.Path.Combine(work, "sc64_64ddipl.zip");
            var extract = System.IO.Path.Combine(work, "extracted");

            Info("Downloading 64DD IPL package...");
            await DownloadFileAsync(SC64_64DD_Url, zipPath, token);
            ExpandZip(zipPath, extract);

            // Find the 'menu' folder inside the extracted files
            var menuSrc = System.IO.Directory.EnumerateDirectories(extract, "menu", System.IO.SearchOption.AllDirectories).FirstOrDefault();
            if (menuSrc == null) throw new Exception("Could not find 'menu' folder in the 64DD IPL package.");

            destMenu = System.IO.Path.Combine(root, "menu");
            System.IO.Directory.CreateDirectory(destMenu);

            // Copy the *contents* of the extracted "menu" folder into \menu
            Info($"Copying 64DD IPL contents → {destMenu}");
            foreach (var dir in System.IO.Directory.GetDirectories(menuSrc))
                await RoboCopyDir(dir, System.IO.Path.Combine(destMenu, System.IO.Path.GetFileName(dir)), token);
            foreach (var file in System.IO.Directory.GetFiles(menuSrc))
                await RoboCopyFile(file, destMenu, token);

            Info("64DD IPL installed (menu folder at SD root).");
            EnsureRemoved(work);
        }


        async Task InstallSummercart64Emulators(string destDrive, CancellationToken token)
        {
            var root = destDrive + @":\";
            var destMenu = Path.Combine(root, "menu");
            var destEmu = Path.Combine(destMenu, "Emulators");

            string[] required =
            {
        "Press-F.z64",
        "smsPlus64.z64",
        "sodium64.z64",
        "neon64bu.rom",
        "gb.v64",
        "gbc.v64",
    };

            bool ExistsAll(string dir)
            {
                if (!Directory.Exists(dir)) return false;
                var names = new HashSet<string>(Directory.GetFiles(dir).Select(Path.GetFileName).Where(n => n is not null).Select(n => n!), StringComparer.OrdinalIgnoreCase);
                return required.All(r => names.Contains(r));
            }

            if (ExistsAll(destEmu))
            {
                Info(@"Emulators already installed in \menu\Emulators");
                return;
            }

            if (MessageBox.Show(this,
                    $"Emulator files not found in:\n{destEmu}\n\nDownload and install now?",
                    "Summercart64 – Emulators",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            var work = Path.Combine(TempRoot, "sc64_emulators");
            var dlDir = Path.Combine(work, "dl");
            var extract = Path.Combine(work, "extracted");

            EnsureRemoved(work);
            Directory.CreateDirectory(dlDir);
            Directory.CreateDirectory(extract);

            var items = new (string Url, string TargetName)[]
            {
        (SC64_EMU_GB,   "gb.v64"),
        (SC64_EMU_GBC,  "gbc.v64"),
        (SC64_EMU_NEON, "neon64bu.rom"),
        (SC64_EMU_PF,   "Press-F.z64"),
        (SC64_EMU_SMS,  "smsPlus64.z64"),
        (SC64_EMU_SOD,  "sodium64.z64"),
            };

            Info("Downloading emulator set...");
            foreach (var (url, tgt) in items)
            {
                token.ThrowIfCancellationRequested();

                var fileNameFromUrl = Path.GetFileName(new Uri(url).LocalPath);
                var dlPath = Path.Combine(dlDir, fileNameFromUrl);
                await DownloadFileAsync(url, dlPath, token);

                if (Path.GetExtension(dlPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var outDir = Path.Combine(extract, Path.GetFileNameWithoutExtension(dlPath));
                    Directory.CreateDirectory(outDir);
                    ExpandZip(dlPath, outDir);
                }
            }

            Directory.CreateDirectory(destMenu);
            Directory.CreateDirectory(destEmu);

            async Task<bool> CopyIfFoundAsync(string preferredName, CancellationToken ct)
            {
                foreach (var p in Directory.EnumerateFiles(dlDir, "*", SearchOption.TopDirectoryOnly))
                {
                    if (string.Equals(Path.GetFileName(p), preferredName, StringComparison.OrdinalIgnoreCase))
                    {
                        await RoboCopyFile(p, destEmu, ct);
                        return true;
                    }
                }

                if (Directory.Exists(extract))
                {
                    var hit = Directory.EnumerateFiles(extract, preferredName, SearchOption.AllDirectories).FirstOrDefault();
                    if (hit != null)
                    {
                        await RoboCopyFile(hit, destEmu, ct);
                        return true;
                    }
                }

                return false;
            }

            foreach (var name in required)
            {
                token.ThrowIfCancellationRequested();
                var ok = await CopyIfFoundAsync(name, token);
                if (!ok)
                    Info($"[WARN] Could not locate a payload for: {name}.");
            }

            Info(@"Emulators installed to \menu\Emulators");
            EnsureRemoved(work);
        }

        private void btnSC64Install64DD_Click(object sender, EventArgs e)
        {

        }
    }
}
