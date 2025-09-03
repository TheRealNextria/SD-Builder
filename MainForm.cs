
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
    public sealed partial class MainForm : Form
    {

// Clears search, imported .txt items, and checked state, then rebuilds from the chosen folder
private void ClearListmakerData()
{
    try
    {
        _lmBulkChange = true;
        _lmFilterDebounce?.Stop(); // prevent auto-refresh from typing
        _lmFolder = string.Empty;  // forget chosen folder for true reset
        if (txtFilter != null) txtFilter.Text = string.Empty;
        _lmTxtItems.Clear();
        _lmChecked.Clear();
        lvList.BeginUpdate();
        lvList.Items.Clear();
        try { SetCounters(0, 0); } catch { /* best-effort */ }
    }
    finally
    {
        try { lvList.EndUpdate(); } catch { /* ignore */ }
        _lmBulkChange = false;
    }
    try { lblStatusLM.Text = "Choose a folder or open a .txt file to begin."; } catch { /* ignore */ }
}

        const string AppVersion = "v2.31";

        const int XsStatusColWidth = 160;
        const int GcStatusColWidth = 160;
        string _xsFolder = string.Empty;
        bool _xsBulkChange = false;
        // Saroo state
        string _srFolder = string.Empty;
        bool _srBulkChange = false;
        CancellationTokenSource? _srScanCts;
        CancellationTokenSource? _xsScanCts;
        // Gamecube state
        string _gcFolder = string.Empty;
        bool _gcBulkChange = false;
        CancellationTokenSource? _gcScanCts;
        System.Windows.Forms.Timer? _glPoll;
        string _glLastSig = string.Empty;
        FileSystemWatcher? _glWatcher;
        System.Windows.Forms.Timer? _glDebounce;

        // ---- URLs
        const string X_FW_Url = "https://github.com/x-station/xstation-releases/releases/download/2.0.2/update202.zip";
        const string S_Url = "https://github.com/tpunix/SAROO/releases/download/v0.7/firm_v0.7.zip";
        const string G_Url = "https://github.com/emukidid/swiss-gc/releases/download/v0.6r1913/swiss_r1913.7z";
        const string GC_CHEATS_Url = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/GC.Swiss.cheats.zip";
        const string G_Owner = "emukidid";
        const string G_Repo = "swiss-gc";
        const string G_AssetPattern = @"^swiss_r\d+\.(?:7z|zip)$";
        const string G_7zr_Url = "https://www.7-zip.org/a/7zr.exe";
        const string SC64_Url = "https://github.com/Polprzewodnikowy/N64FlashcartMenu/releases/download/rolling_release/sc64menu.n64";
        const string SC64_64DD_Url = "https://64dd.org/download/sc64_64ddipl.zip";

        // ---- Summercart64 Emulator URLs
        const string SC64_EMU_GB = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/gb.v64";
        const string SC64_EMU_GBC = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/gbc.v64";
        const string SC64_EMU_NEON = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/neon64bu.rom";
        const string SC64_EMU_PF = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/Press-F.z64";
        const string SC64_EMU_SMS = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/smsPlus64.z64";
        const string SC64_EMU_SOD = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/sodium64.z64";

        // ---- Paths
        readonly string ScriptDir;
        string TempRoot => Path.Combine(ScriptDir, "temp");
        string ConfigFile => Path.Combine(ScriptDir, "settings.json");
        string GameListsDir => Path.Combine(ScriptDir, "GameLists");

        // Xstation temp
        string X_FW_Zip => Path.Combine(TempRoot, "xstation_fw.zip");
        string X_FW_Extract => Path.Combine(TempRoot, "extracted");

        // Saroo temp
        string S_Work => Path.Combine(TempRoot, "saroo");
        string S_Zip => Path.Combine(S_Work, "saroo_fw.zip");
        string S_Extract => Path.Combine(S_Work, "extracted");

        // Swiss temp
        string G_Work => Path.Combine(TempRoot, "gamecube");
        string G_7z => Path.Combine(G_Work, "swiss.7z");
        string G_Extract => Path.Combine(G_Work, "extracted");
        string G_7zr => Path.Combine(G_Work, "7zr.exe");

        // Robocopy flags
        readonly string[] RoboFlagsDir = new[] { "/E", "/IS", "/IT", "/COPY:DAT", "/R:1", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/Z", "/FFT" };
        readonly string[] RoboFlagsFile = new[] { "/IS", "/IT", "/COPY:DAT", "/R:1", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/FFT" };

        // Settings (defaults)
        string _lmFolder = string.Empty;

        // Persist .txt imports across searches
        private readonly HashSet<string> _lmTxtItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _lmChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int CopyTimeout = 20;
        int OverwriteTimeout = 10;
        string OverwriteAutoAction = "Yes"; // Yes|No
        bool ShowOnlyRemovable = true;
        bool EjectPromptEnabled = false;

        CancellationTokenSource? _cts;
        System.Windows.Forms.Timer? _hotplugTimer;


        // Async Listmaker size scans
        CancellationTokenSource? _lmScanCts;

        // Listmaker UI performance
        System.Windows.Forms.Timer? _lmDebounce;
        System.Windows.Forms.Timer? _lmFilterDebounce;
        bool _lmBulkChange;
        enum Platform { Xstation, Saroo, Gamecube, Summercart64 }
        enum ReplaceDecision { Yes, No, Cancel }


        public MainForm()
        {
            InitializeComponent();

            // Wire Xstation controls
            WireXstationListmaker();

            // Wire Saroo controls
            WireSarooListmaker();

            // Wire Gamecube controls (listmaker)
            WireGamecubeListmaker();

            // Icon & title
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = $"SD-Builder (GUI) {AppVersion}";
            lblVersion.Text = AppVersion;
            Info("Tip: place your .txt files in the GameLists folder next to this .exe.");
            ScriptDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.O)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                        InitialDirectory = Directory.Exists(GameListsDir) ? GameListsDir : ScriptDir
                    };
                    if (ofd.ShowDialog(this) == DialogResult.OK) OpenTxtFile(ofd.FileName);
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.S)
                {
                    SaveTxt();
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.A && !e.Shift)
                {
                    _lmBulkChange = true;
                    foreach (ListViewItem it in lvList.Items) it.Checked = true;
                    _lmBulkChange = false;
                    _lmDebounce?.Stop(); _lmDebounce?.Start();
                    e.Handled = true;
                }
                else if (e.Control && e.Shift && e.KeyCode == Keys.A)
                {
                    _lmBulkChange = true;
                    foreach (ListViewItem it in lvList.Items) it.Checked = false;
                    _lmBulkChange = false;
                    _lmDebounce?.Stop(); _lmDebounce?.Start();
                    e.Handled = true;
                }
            };

            // debounce for Listmaker recalculation
            _lmDebounce = new System.Windows.Forms.Timer { Interval = 250 };
            _lmDebounce.Tick += async (_, __) => { _lmDebounce!.Stop(); await RecalcCountersAsync(); };

            // debounce for Listmaker filter typing
            _lmFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _lmFilterDebounce.Tick += (_, __) => { _lmFilterDebounce!.Stop(); RefreshListmakerList(); };

            // Wire top bar
            btnRefresh.Click += (_, __) => RefreshDrives(preserveSelection: true);
            btnStop.Click += (_, __) => _cts?.Cancel();
            btnOpen.Click += (_, __) => OpenCurrentDriveInExplorer();
            btnEject.Click += async (_, __) => await EjectCurrentDriveAsync();
            cmbDrives.SelectedIndexChanged += (_, __) => UpdateDriveButtonsEnabled();
            Resize += (_, __) => PositionVersionLabel();

            // Wire platform tabs
            WirePlatformTab(
                Platform.Xstation,
                btnXstationFw, cmbXstationList, btnXstationRefreshLists, btnXstationOpenLists, btnXstationStart,
                null, null
            );
            WirePlatformTab(
                Platform.Saroo,
                btnSarooFw, cmbSarooList, btnSarooRefreshLists, btnSarooOpenLists, btnSarooStart,
                null, null
            );
            WirePlatformTab(
                Platform.Gamecube,
                btnGamecubeFw, cmbGamecubeList, btnGamecubeRefreshLists, btnGamecubeOpenLists, btnGamecubeStart,
                null, null
            );
            try { btnGamecubeCheats.Click += async (_, __) => await WithDriveJobAsync(InstallGamecubeCheats); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }

            WirePlatformTab(
                Platform.Summercart64,
                btnSC64Fw, cmbSC64List, btnSC64RefreshLists, btnSC64OpenLists, btnSC64Start,
                btnSC64Install64DD, btnSC64InstallEmulators
            );

            // Wire Listmaker tab
            WireListmakerTab();

            // Live filter as you type
            txtFilter.TextChanged += (_, __) => { _lmFilterDebounce?.Stop(); _lmFilterDebounce?.Start(); };
            txtFilter.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; _lmFilterDebounce?.Stop(); RefreshListmakerList(); } };

            // Always include subfolders; hide the checkbox to simplify UI
            try { chkRecursive.Checked = true; chkRecursive.Visible = false; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            WireListContextMenu();

            // Drag & drop for Listmaker
            lvList.AllowDrop = true;
            lvList.DragEnter += (s, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                    e.Effect = DragDropEffects.Copy;
            };
            lvList.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        lblStatusLM.Text = "Loading...";
                        _lmFolder = path;
                        SaveSettings();
                        RefreshListmakerList();
                    }
                    else if (string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenTxtFile(path);
                    }
                }
            };

            // Wire settings
            chkOnlyRemovable.CheckedChanged += (_, __) =>
            {
                ShowOnlyRemovable = chkOnlyRemovable.Checked;
                SaveSettings();
                RefreshDrives(preserveSelection: true);
            };
            cmbAuto.Items.Clear();
            cmbAuto.Items.AddRange(new object[] { "Yes", "No" });
            cmbAuto.SelectedIndex = 0;
            btnSaveSettings.Click += (_, __) =>
            {
                CopyTimeout = (int)numCopy.Value;
                OverwriteTimeout = (int)numOW.Value;
                OverwriteAutoAction = (string)(cmbAuto.SelectedItem ?? "Yes");
                SaveSettings();
                Info("Settings saved.");
            };

            // Init
            Load += (_, __) =>
            {
                Directory.CreateDirectory(GameListsDir);
                LoadSettings();

                // Always start Listmaker empty on app start
                _lmFolder = string.Empty;
                if (lvList != null)
                {
                    try
                    {
                        lvList.Items.Clear();
                        lblStatusLM.Text = "Choose a folder or open a .txt file to begin.";
                    }
                    catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                }
                // Auto-refresh Listmaker on startup if last folder exists (minimal fix)
                try { if (!string.IsNullOrWhiteSpace(_lmFolder) && System.IO.Directory.Exists(_lmFolder)) RefreshListmakerList(); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }

                // Clear all ListBox controls on startup so previous session entries don't persist
                try { foreach (var lb in FindListBoxes(this)) lb.Items.Clear(); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                this.FormClosing += (_, __) => SaveSettings();
                if (!File.Exists("settings.json"))
                {
                    SaveSettings();
                }
                try { chkEjectPrompt.CheckedChanged += (_, __) => { EjectPromptEnabled = chkEjectPrompt.Checked; SaveSettings(); }; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                RefreshDrives();
                Info($"GameLists dir: {GameListsDir}");
                // Start GameLists poller (fallback if FileSystemWatcher misses events)
                try
                {
                    _glLastSig = GetGameListsSignature();
                    RefreshAllGameLists();
                    _glPoll?.Stop();
                    _glPoll?.Dispose();
                    _glPoll = new System.Windows.Forms.Timer { Interval = 1500 };
                    _glPoll.Tick += (_, __) =>
                    {
                        var sig = GetGameListsSignature();
                        if (!string.Equals(sig, _glLastSig, StringComparison.Ordinal))
                        {
                            _glLastSig = sig;
                            BeginInvoke(new Action(RefreshAllGameLists));
                        }
                    };
                    _glPoll.Start();
                }
                catch { /* ignore */ }

                // initial populate for all platform combos
                RefreshAllGameLists();

                // watch GameLists folder for .txt changes and refresh all combos (debounced)
                try
                {
                    _glWatcher?.Dispose();
                    _glWatcher = new FileSystemWatcher(GameListsDir, "*.txt")
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                    };
                    _glDebounce?.Dispose();
                    _glDebounce = new System.Windows.Forms.Timer { Interval = 400 };
                    _glDebounce.Tick += (_, __) => { _glDebounce!.Stop(); BeginInvoke(new Action(RefreshAllGameLists)); };
                    FileSystemEventHandler bump = (_, __) => { _glDebounce!.Stop(); _glDebounce!.Start(); };
                    RenamedEventHandler bumpRen = (_, __) => { _glDebounce!.Stop(); _glDebounce!.Start(); };
                    _glWatcher.Created += bump;
                    _glWatcher.Changed += bump;
                    _glWatcher.Deleted += bump;
                    _glWatcher.Renamed += bumpRen;
                    _glWatcher.EnableRaisingEvents = true;
                }
                catch (Exception ex) { Warn("GameLists watcher disabled: " + ex.Message); }

                UpdateDriveButtonsEnabled();
                PositionVersionLabel();

                // Disabled auto-load of last folder on startup (Option 2)
                // if (Directory.Exists(_lmFolder))
                {
                    lblStatusLM.Text = "Loading...";
                    RefreshListmakerList();
                }

                // Ensure Listmaker starts empty on launch (avoid repopulating from previous session)
                if (lvList != null)
                {
                    try
                    {
                        lvList.BeginUpdate();
                        lvList.Items.Clear();
                    }
                    finally
                    {
                        try { lvList.EndUpdate();
                _lmBulkChange = false; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                    }
                }
                try { lblStatusLM.Text = "Choose a folder or open a .txt file to begin."; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                try { SetCounters(0, 0); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            };

            FormClosed += (_, __) =>
            {
                if (_hotplugTimer != null)
                {
                    _hotplugTimer.Stop(); _hotplugTimer.Dispose(); try { _gcScanCts?.Cancel(); _gcScanCts?.Dispose(); _gcScanCts = null; } catch { }
                }
                _lmScanCts?.Cancel();
            };
        }
        void RefreshAllGameLists()
        {
            try { PopulateListCombo(cmbXstationList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            try { PopulateListCombo(cmbSarooList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            try { PopulateListCombo(cmbGamecubeList); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
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


        // ---------- Layout helpers that depend on Designer controls ----------

        void PositionVersionLabel()
        {
            // Keep lblVersion top-right of topPanel
            if (topPanel == null || lblVersion == null) return;
            lblVersion.Left = topPanel.ClientSize.Width - lblVersion.Width - 8;
            lblVersion.Top = 8;
            lblVersion.BringToFront();
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

        void WireListmakerTab()
        {
            // Autoresize columns on show/resize
            lvList.View = View.Details;
            lvList.FullRowSelect = true;
            lvList.HideSelection = false;
            if (lvList.Columns.Count == 0)
            {
                lvList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvList.Columns.Add("Status", 100, HorizontalAlignment.Left);
            }

            void AutoSizeColumns()
            {
                if (lvList.Columns.Count == 0) return;
                lvList.Columns[0].Width = Math.Max(120, lvList.ClientSize.Width - 120);
                lvList.Columns[1].Width = 100;
            }

            lvList.Resize += (_, __) => AutoSizeColumns();
            tabListmaker.Enter += (_, __) => AutoSizeColumns();// Handlers
            btnChooseFolder.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    lblStatusLM.Text = "Loading...";
                    _lmFolder = dlg.SelectedPath;
                    RefreshListmakerList();
                }
            };
            btnRefreshLM.Click += (_, __) => { txtFilter.Text = string.Empty; RefreshListmakerList(); };
            
btnOpenGameListsLM.Click += (_, __) =>
{
    try
    {
        var gl = System.IO.Path.Combine(AppContext.BaseDirectory, "GameLists");
        System.Diagnostics.Process.Start("explorer.exe", gl);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Could not open GameLists folder: {ex.Message}", "Open GameLists", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
};

            btnClearLM.Click += (_, __) => { ClearListmakerData(); };
            chkRecursive.CheckedChanged += (_, __) => RefreshListmakerList();
            rbFiles.CheckedChanged += (_, __) => { if (rbFiles.Checked && txtFilter.Text.Trim() == "*") txtFilter.Text = "*.*"; RefreshListmakerList(); };
            rbDirs.CheckedChanged += (_, __) => { if (rbDirs.Checked && (txtFilter.Text.Trim() == "*.*" || txtFilter.Text.Trim().Length == 0)) txtFilter.Text = "*"; RefreshListmakerList(); };

            btnCheckAll.Click += (_, __) => {
                _lmBulkChange = true;
                try {
                    lvList.BeginUpdate();
                    for (int i = 0; i < lvList.Items.Count; i++) {
                        var it = lvList.Items[i];
                        it.Checked = true;
                    }
                } finally {
                    lvList.EndUpdate();
                    _lmBulkChange = false;
                }
                try {
                    foreach (ListViewItem it in lvList.Items) {
                        var p = it.Text;
                        if (!string.IsNullOrWhiteSpace(p)) _lmChecked.Add(p);
                    }
                } catch { }
                _lmDebounce?.Stop(); _lmDebounce?.Start();
            };
                try { foreach (ListViewItem it in lvList.Items) { var p = it.Text; if (!string.IsNullOrWhiteSpace(p) && it.Checked) _lmChecked.Add(p); } } catch { }
            ;
            btnUncheckAll.Click += (_, __) => {
                _lmBulkChange = true;
                try {
                    lvList.BeginUpdate();
                    for (int i = 0; i < lvList.Items.Count; i++) {
                        var it = lvList.Items[i];
                        it.Checked = false;
                    }
                } finally {
                    lvList.EndUpdate();
                    _lmBulkChange = false;
                }
                try {
                    foreach (ListViewItem it in lvList.Items) {
                        var p = it.Text;
                        if (!string.IsNullOrWhiteSpace(p)) _lmChecked.Remove(p);
                    }
                } catch { }
                _lmDebounce?.Stop(); _lmDebounce?.Start();
            };
                try { foreach (ListViewItem it in lvList.Items) { var p = it.Text; if (!string.IsNullOrWhiteSpace(p)) _lmChecked.Remove(p); } } catch { }
            ;
            btnDeleteSelected.Click += (_, __) =>
            {
                if (lvList.SelectedItems.Count == 0) { MessageBox.Show(this, "No items selected to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                foreach (ListViewItem it in lvList.SelectedItems.Cast<ListViewItem>().ToList()) lvList.Items.Remove(it);
                lblStatusLM.Text = "Deleted selected items.";
                _ = RecalcCountersAsync();
            };

            btnOpenTxt.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog { Title = "Open text file with paths", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", InitialDirectory = Directory.Exists(GameListsDir) ? GameListsDir : ScriptDir };
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(ofd.FileName, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not read file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var existing = new HashSet<string>(lvList.Items.Cast<ListViewItem>().Select(it => NormalizePath(it.Text)), StringComparer.OrdinalIgnoreCase);

                int added = 0, dupes = 0;
                lvList.BeginUpdate();
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if ((line.StartsWith("\"") && line.EndsWith("\"")) || (line.StartsWith("'") && line.EndsWith("'"))) line = line[1..^1];
                    var norm = NormalizePath(line);
                    if (existing.Contains(norm)) { dupes++; continue; }
                    AddRowLM(norm);
                    _lmTxtItems.Add(norm);
                    existing.Add(norm);
                    added++;
                }
                lvList.EndUpdate();
                _lmBulkChange = false;

                lblStatusLM.Text = $"Loaded {added} new, skipped {dupes} duplicates from: {ofd.FileName}";
                if (chkAutoValidate.Checked) ValidatePaths();
                _ = RecalcCountersAsync();
            };

            btnValidateNow.Click += (_, __) => { ValidatePaths(); };
            btnSaveTxt.Click += (_, __) => SaveTxt();

            lvList.ItemChecked += (_, __) => { if (_lmBulkChange) return; _lmDebounce?.Stop(); _lmDebounce?.Start(); };
            // Persist checked state per item
            lvList.ItemChecked += (sender, e) =>
            {
                if (_lmBulkChange) return;
                if (e != null && e.Item != null)
                {
                    var p = e.Item.Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        if (e.Item.Checked) _lmChecked.Add(p);
                        else _lmChecked.Remove(p);
                    }
                }
            };

            // Defaults
            lblStatusLM.Text = "Choose a folder or open a .txt file to begin.";
            SetCounters(0, 0);
        }

        // ------------------- Xstation helpers -------------------
        void SetXsStatus(string text)
        {
            try { lblStatusXS.Text = text; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        void WireXstationListmaker()
        {
            // columns
            lvXsList.View = View.Details;
            lvXsList.FullRowSelect = true;
            lvXsList.HideSelection = false;
            if (lvXsList.Columns.Count < 2)
            {
                lvXsList.Columns.Clear();
                lvXsList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvXsList.Columns.Add("Status", XsStatusColWidth, HorizontalAlignment.Left);
            }

            void AutoSizeXs()
            {
                if (lvXsList.Columns.Count >= 2)
                {
                    int statusWidth = XsStatusColWidth;
                    lvXsList.Columns[0].Width = Math.Max(120, lvXsList.ClientSize.Width - statusWidth - 8);
                    lvXsList.Columns[1].Width = statusWidth;
                }
            }
            lvXsList.Resize += (_, __) => AutoSizeXs();
            tabXstation.Enter += (_, __) => AutoSizeXs();

            btnXsChoose.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _xsFolder = dlg.SelectedPath;
                    SetXsStatus($"Selected: {_xsFolder}");
                    RefreshXstationList();
                }
            };
            rbXsFiles.CheckedChanged += (_, __) => { if (rbXsFiles.Checked) { SetXsStatus("Mode: Files"); RefreshXstationList(); } };
            rbXsDirs.CheckedChanged += (_, __) => { if (rbXsDirs.Checked) { SetXsStatus("Mode: Directories"); RefreshXstationList(); } };

            lvXsList.ItemChecked += (_, __) => { if (!_xsBulkChange) UpdateXsCounters(); };

            btnXsCheckAll.Click += (_, __) =>
            {
                _xsBulkChange = true;
                foreach (ListViewItem it in lvXsList.Items) it.Checked = true;
                _xsBulkChange = false;
                UpdateXsCounters();
            };
            btnXsUncheckAll.Click += (_, __) =>
            {
                _xsBulkChange = true;
                foreach (ListViewItem it in lvXsList.Items) it.Checked = false;
                _xsBulkChange = false;
                UpdateXsCounters();
            };
            btnXsStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopyXstationCheckedAsync(d, token));
            };

        }


        void RefreshXstationList()
        {
            try
            {
                lvXsList.BeginUpdate();
                lvXsList.Items.Clear();
                if (!Directory.Exists(_xsFolder)) { SetXsStatus("Ready."); return; }

                SetXsStatus("Scanning...");
                // ensure columns
                if (lvXsList.Columns.Count < 2)
                {
                    lvXsList.Columns.Clear();
                    lvXsList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvXsList.Columns.Add("Status", XsStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbXsFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_xsFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = false;
                        lvXsList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_xsFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = false;
                        lvXsList.Items.Add(it);
                    }
                }
                UpdateXsCounters();
                _ = RecalcXsAsync();
            }
            catch (Exception ex)
            {
                Error("Xstation list error: " + ex.Message);
            }
            finally { lvXsList.EndUpdate(); }
        }

        void UpdateXsCounters()
        {
            long total = 0;
            int count = 0;
            foreach (ListViewItem it in lvXsList.Items)
            {
                if (!it.Checked) continue;
                count++;
                if (it.Tag is long sz) total += sz;
            }
            try
            {
                lblCountXS.Text = $"Checked: {count}";
                lblSizeXS.Text = $"Size: {FormatSize(total)}";
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        async Task RecalcXsAsync()
        {
            try
            {
                _xsScanCts?.Cancel();
                _xsScanCts = new CancellationTokenSource();
                var ct = _xsScanCts.Token;

                await Task.Run(() =>
                {
                    foreach (ListViewItem it in lvXsList.Items)
                    {
                        if (ct.IsCancellationRequested) break;
                        var path = it.Text;
                        long size = 0;
                        try
                        {
                            if (File.Exists(path))
                            {
                                size = new FileInfo(path).Length;
                            }
                            else if (Directory.Exists(path))
                            {
                                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                {
                                    if (ct.IsCancellationRequested) break;
                                    try { size += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                                }
                            }
                            var sizeStr = FormatSize(size);
                            BeginInvoke(new Action(() =>
                            {
                                if (it.SubItems.Count < 2) it.SubItems.Add(sizeStr);
                                else it.SubItems[1].Text = sizeStr;
                                it.Tag = size;
                                if (it.Checked) UpdateXsCounters();
                            }));
                        }
                        catch
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (it.SubItems.Count < 2) it.SubItems.Add("error");
                                else it.SubItems[1].Text = "error";
                            }));
                        }
                    }
                }, ct);

                BeginInvoke(new Action(() => SetXsStatus($"Ready. {lvXsList.Items.Count} items.")));
            }
            catch { /* cancelled */ }
        }


        void SetSrStatus(string text)
        {
            try { lblStatusSR.Text = text; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
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
                lvSrList.Columns.Add("Status", XsStatusColWidth, HorizontalAlignment.Left);
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

            lvSrList.ItemChecked += (_, __) => { if (!_srBulkChange) UpdateSrCounters(); };

            btnSrCheckAll.Click += (_, __) =>
            {
                _srBulkChange = true;
                foreach (ListViewItem it in lvSrList.Items) it.Checked = true;
                _srBulkChange = false;
                UpdateSrCounters();
            };
            btnSrUncheckAll.Click += (_, __) =>
            {
                _srBulkChange = true;
                foreach (ListViewItem it in lvSrList.Items) it.Checked = false;
                _srBulkChange = false;
                UpdateSrCounters();
            };
            btnSrStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopySarooCheckedAsync(d, token));
            };
        }

        // -------- Gamecube Listmaker (mirrors Xstation) --------
        void WireGamecubeListmaker()
        {
            // columns
            lvGcList.View = View.Details;
            lvGcList.FullRowSelect = true;
            lvGcList.HideSelection = false;
            if (lvGcList.Columns.Count < 2)
            {
                lvGcList.Columns.Clear();
                lvGcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvGcList.Columns.Add("Status", GcStatusColWidth, HorizontalAlignment.Left);
            }

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

            lvGcList.ItemChecked += (_, __) => { if (!_gcBulkChange) UpdateGcCounters(); };

            btnGcCheckAll.Click += (_, __) =>
            {
                _gcBulkChange = true;
                foreach (ListViewItem it in lvGcList.Items) it.Checked = true;
                _gcBulkChange = false;
                UpdateGcCounters();
            };
            btnGcUncheckAll.Click += (_, __) =>
            {
                _gcBulkChange = true;
                foreach (ListViewItem it in lvGcList.Items) it.Checked = false;
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
            try { lblStatusGC.Text = text; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
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
            try
            {
                lblCountGC.Text = $"Checked: {count}";
                lblSizeGC.Text = $"Size: {FormatSize(total)}";
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        void RefreshGamecubeList()
        {
            try
            {
                lvGcList.BeginUpdate();
                lvGcList.Items.Clear();
                if (!Directory.Exists(_gcFolder)) { SetGcStatus("Ready."); return; }

                SetGcStatus("Scanning...");
                // ensure columns
                if (lvGcList.Columns.Count < 2)
                {
                    lvGcList.Columns.Clear();
                    lvGcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvGcList.Columns.Add("Status", GcStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbGcFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_gcFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = false;
                        lvGcList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_gcFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = false;
                        lvGcList.Items.Add(it);
                    }
                }

                _ = RecalcGcAsync();
            }
            catch (Exception ex)
            {
                Error("Error refreshing list: " + ex.Message);
            }
            finally { lvGcList.EndUpdate(); }
        }

        async Task RecalcGcAsync()
        {
            try
            {
                _gcScanCts?.Cancel();
                _gcScanCts = new CancellationTokenSource();
                var ct = _gcScanCts.Token;

                await Task.Run(() =>
                {
                    foreach (ListViewItem it in lvGcList.Items)
                    {
                        if (ct.IsCancellationRequested) break;
                        var path = it.Text;
                        long size = 0;
                        try
                        {
                            if (File.Exists(path))
                            {
                                size = new FileInfo(path).Length;
                            }
                            else if (Directory.Exists(path))
                            {
                                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                {
                                    if (ct.IsCancellationRequested) break;
                                    try { size += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                                }
                            }
                            var sizeStr = FormatSize(size);
                            BeginInvoke(new Action(() =>
                            {
                                if (it.SubItems.Count < 2) it.SubItems.Add(sizeStr);
                                else it.SubItems[1].Text = sizeStr;
                                it.Tag = size;
                                if (it.Checked) UpdateGcCounters();
                            }));
                        }
                        catch
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (it.SubItems.Count < 2) it.SubItems.Add("error");
                                else it.SubItems[1].Text = "error";
                            }));
                        }
                    }
                }, ct);

                BeginInvoke(new Action(() => SetGcStatus("Ready.")));
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        async Task CopyGamecubeCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\Games";
            Directory.CreateDirectory(destRoot);
            int copied = 0;

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
        }

        void RefreshSarooList()
        {
            try
            {
                lvSrList.BeginUpdate();
                lvSrList.Items.Clear();
                if (!Directory.Exists(_srFolder)) { SetSrStatus("Ready."); return; }

                SetSrStatus("Scanning...");
                if (lvSrList.Columns.Count < 2)
                {
                    lvSrList.Columns.Clear();
                    lvSrList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvSrList.Columns.Add("Status", XsStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbSrFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_srFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = false;
                        lvSrList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_srFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = false;
                        lvSrList.Items.Add(it);
                    }
                }
                UpdateSrCounters();
                _ = RecalcSrAsync();
            }
            catch (Exception ex)
            {
                Error("Saroo list error: " + ex.Message);
            }
            finally { lvSrList.EndUpdate(); }
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
            try
            {
                lblCountSR.Text = $"Checked: {count}";
                lblSizeSR.Text = $"Size: {FormatSize(total)}";
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        async Task RecalcSrAsync()
        {
            try
            {
                _srScanCts?.Cancel();
                _srScanCts = new CancellationTokenSource();
                var ct = _srScanCts.Token;

                await Task.Run(() =>
                {
                    foreach (ListViewItem it in lvSrList.Items)
                    {
                        if (ct.IsCancellationRequested) break;
                        var path = it.Text;
                        long size = 0;
                        try
                        {
                            if (File.Exists(path))
                            {
                                size = new FileInfo(path).Length;
                            }
                            else if (Directory.Exists(path))
                            {
                                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                {
                                    if (ct.IsCancellationRequested) break;
                                    try { size += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                                }
                            }
                            var sizeStr = FormatSize(size);
                            BeginInvoke(new Action(() =>
                            {
                                if (it.SubItems.Count < 2) it.SubItems.Add(sizeStr);
                                else it.SubItems[1].Text = sizeStr;
                                it.Tag = size;
                                if (it.Checked) UpdateSrCounters();
                            }));
                        }
                        catch
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (it.SubItems.Count < 2) it.SubItems.Add("error");
                                else it.SubItems[1].Text = "error";
                            }));
                        }
                    }
                }, ct);

                BeginInvoke(new Action(() => SetSrStatus($"Ready. {lvSrList.Items.Count} items.")));
            }
            catch { /* cancelled */ }
        }

        async Task CopySarooCheckedAsync(string destDrive, CancellationToken token)
        {
            string sarooRoot = $"{destDrive}:\\Saroo\\ISO";
            Directory.CreateDirectory(sarooRoot);
            int copied = 0;

            foreach (ListViewItem it in lvSrList.Items)
            {
                if (!it.Checked) continue;
                var path = it.Text;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Info($"[SKIP] Not found: \"{path}\"");
                    continue;
                }
                await CopyEntry(path, Platform.Saroo, sarooRoot, token);
                copied++;
            }

            if (copied == 0) Warn("No items checked.");
        }



        // ------------------- Logging -------------------
        void Info(string msg) => AppendLog(msg);
        void Warn(string msg) => AppendLog("[WARN] " + msg);
        void Error(string msg) => AppendLog("[ERROR] " + msg);
        void AppendLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            if (txtLog.InvokeRequired) txtLog.Invoke(new Action(() => Append(line)));
            else Append(line);
            void Append(string s) { txtLog.AppendText(s + Environment.NewLine); txtLog.SelectionStart = txtLog.TextLength; txtLog.ScrollToCaret(); }
        }
        void UpdateDriveButtonsEnabled()
        {
            bool canDriveActions = _cts == null && CurrentDriveLetter() != null;
            btnEject.Enabled = canDriveActions;
            btnOpen.Enabled = canDriveActions;
        }

        // ------------------- Device change -------------------
        protected override void WndProc(ref Message m)
        {
            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVICEARRIVAL = 0x8000;
            const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
            const int DBT_DEVNODES_CHANGED = 0x0007;

            if (m.Msg == WM_DEVICECHANGE)
            {
                int evt = m.WParam.ToInt32();
                if (evt == DBT_DEVICEARRIVAL || evt == DBT_DEVICEREMOVECOMPLETE || evt == DBT_DEVNODES_CHANGED) DebounceHotplugRefresh();
            }
            base.WndProc(ref m);
        }

        void DebounceHotplugRefresh()
        {
            if (_hotplugTimer == null)
            {
                _hotplugTimer = new System.Windows.Forms.Timer { Interval = 800 };
                _hotplugTimer.Tick += (_, __) =>
                {
                    _hotplugTimer!.Stop();
                    RefreshDrives(preserveSelection: true);
                    UpdateDriveButtonsEnabled();
                };
            }
            _hotplugTimer.Stop();
            _hotplugTimer.Start();
        }

        // ------------------- Drives -------------------
        void RefreshDrives(bool preserveSelection = false)
        {
            string? prev = preserveSelection ? CurrentDriveLetter() : null;

            cmbDrives.BeginUpdate();
            try
            {
                cmbDrives.Items.Clear();

                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Where(d => ShowOnlyRemovable ? d.DriveType == DriveType.Removable
                                                  : (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed));

                foreach (var di in drives) cmbDrives.Items.Add($"{di.Name.TrimEnd('\\')} ({di.VolumeLabel})");

                if (cmbDrives.Items.Count > 0)
                {
                    bool matched = false;
                    if (preserveSelection && prev != null)
                    {
                        for (int i = 0; i < cmbDrives.Items.Count; i++)
                        {
                            var s = cmbDrives.Items[i]!.ToString()!;
                            var letter = s.Split(':').FirstOrDefault();
                            if (!string.IsNullOrEmpty(letter) && string.Equals(letter, prev, StringComparison.OrdinalIgnoreCase))
                            {
                                cmbDrives.SelectedIndex = i;
                                matched = true;
                                break;
                            }
                        }
                    }
                    if (!matched) cmbDrives.SelectedIndex = 0;
                }
                else cmbDrives.SelectedIndex = -1;
            }
            finally { cmbDrives.EndUpdate(); }
        }

        string? CurrentDriveLetter()
        {
            if (cmbDrives.SelectedItem is null) return null;
            var s = cmbDrives.SelectedItem.ToString()!;
            var drive = s.Split(':').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(drive)) return null;
            var path = drive.ToUpper() + ":\\";
            return Directory.Exists(path) ? drive.ToUpper() : null;
        }

        // ------------------- Post-run dialog -------------------
        async Task WithDriveJobAsync(Func<string, CancellationToken, Task> action)
        {
            var d = CurrentDriveLetter();
            if (d == null) { Error("Please select a valid drive."); return; }
            if (_cts != null) { Warn("A task is already running."); return; }

            using var cts = new CancellationTokenSource();
            _cts = cts;
            btnStop.Enabled = true; btnEject.Enabled = false; btnOpen.Enabled = false;

            try { await action(d, cts.Token); Info("Done."); }
            catch (OperationCanceledException) { Warn("Cancelled."); }
            catch (Exception ex) { Error("Error: " + ex.Message); }
            finally
            {
                btnStop.Enabled = false; _cts = null; UpdateDriveButtonsEnabled();
                if (EjectPromptEnabled) await PostRunPromptAsync(d);
            }
        }

        async Task PostRunPromptAsync(string driveLetter)
        {
            using var dlg = new Form { Text = "Completed", Width = 420, Height = 180, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            dlg.Icon = Icon;
            var lbl = new Label { Left = 12, Top = 12, Width = 380, Height = 50, AutoSize = false, Text = $"All operations on {driveLetter}:\\ are complete.\nWhat would you like to do?" };
            var btnOpenX = new Button { Text = "Open in Explorer", Left = 12, Top = 80, Width = 130, DialogResult = DialogResult.Retry };
            var btnEjectX = new Button { Text = "Eject now", Left = 152, Top = 80, Width = 100, DialogResult = DialogResult.Yes };
            var btnCloseX = new Button { Text = "Close", Left = 262, Top = 80, Width = 100, DialogResult = DialogResult.Cancel };
            dlg.Controls.AddRange(new Control[] { lbl, btnOpenX, btnEjectX, btnCloseX });
            dlg.AcceptButton = btnOpenX;

            var res = dlg.ShowDialog(this);
            if (res == DialogResult.Retry) OpenDriveInExplorer(driveLetter);
            else if (res == DialogResult.Yes) await EjectDriveLetterAsync(driveLetter);
        }

        void OpenCurrentDriveInExplorer()
        {
            var d = CurrentDriveLetter();
            if (d == null) { Warn("No drive selected."); return; }
            OpenDriveInExplorer(d);
        }
        void OpenDriveInExplorer(string d)
        {
            string path = d.ToUpper() + ":\\";
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); Info($"Opened {path} in Explorer."); }
            catch (Exception ex) { Warn("Could not open Explorer: " + ex.Message); }
        }

        async Task EjectCurrentDriveAsync()
        {
            var d = CurrentDriveLetter();
            if (d == null) { Error("No drive selected."); return; }
            await EjectDriveLetterAsync(d);
        }
        async Task EjectDriveLetterAsync(string d)
        {
            if (_cts != null) { Warn("Stop the current task before ejecting."); return; }
            btnEject.Enabled = false;
            try
            {
                Info($"Ejecting {d}:");
                var ok = await Task.Run(() => TryEjectVolume(d));
                if (ok) { Info($"Safely ejected {d}:\\"); RefreshDrives(preserveSelection: false); }
                else { Warn($"Could not safely eject {d}:\\ (it may be in use by another process)."); }
            }
            finally { UpdateDriveButtonsEnabled(); }
        }

        bool TryEjectVolume(string driveLetter)
        {
            string volPath = @"\\.\\" + driveLetter.TrimEnd(':') + ":";
            const uint GENERIC_READ = 0x80000000;
            const uint GENERIC_WRITE = 0x40000000;
            const uint FILE_SHARE_READ = 0x00000001;
            const uint FILE_SHARE_WRITE = 0x00000002;
            const uint OPEN_EXISTING = 3;
            const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
            const uint FSCTL_LOCK_VOLUME = 0x00090018;
            const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
            const uint IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;
            const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;

            using SafeFileHandle h = CreateFileW(volPath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
            if (h.IsInvalid) return false;

            int br;
            for (int i = 0; i < 5; i++)
            {
                if (DeviceIoControl(h, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out br, IntPtr.Zero)) break;
                System.Threading.Thread.Sleep(300);
                if (i == 4) return false;
            }

            if (!DeviceIoControl(h, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out br, IntPtr.Zero)) return false;

            PREVENT_MEDIA_REMOVAL pmr = new PREVENT_MEDIA_REMOVAL { PreventMediaRemoval = 0 };
            IntPtr pmrPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PREVENT_MEDIA_REMOVAL>());
            try
            {
                Marshal.StructureToPtr(pmr, pmrPtr, false);
                DeviceIoControl(h, IOCTL_STORAGE_MEDIA_REMOVAL, pmrPtr, Marshal.SizeOf<PREVENT_MEDIA_REMOVAL>(), IntPtr.Zero, 0, out br, IntPtr.Zero);
            }
            finally { Marshal.FreeHGlobal(pmrPtr); }

            if (!DeviceIoControl(h, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out br, IntPtr.Zero))
            {
                return true; // unmounted; not all devices support explicit eject
            }
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PREVENT_MEDIA_REMOVAL { public byte PreventMediaRemoval; }
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
        static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        // ------------------- Firmware flows (unchanged) -------------------
        // Firmware
        async Task EnsureXstationFirmware(string destDrive, CancellationToken token)
        {
            var xstationDir = $@"{destDrive}:\00xstation";
            var loader = Path.Combine(xstationDir, "loader.bin");
            var update = Path.Combine(xstationDir, "update.bin");
            bool need = !(File.Exists(loader) && File.Exists(update));
            if (!need) { Info("Xstation firmware present."); return; }
            if (MessageBox.Show(this, $"Xstation firmware missing in:\n{xstationDir}\n\nDownload now?", "Xstation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(TempRoot); Directory.CreateDirectory(TempRoot);
            await DownloadFileAsync(X_FW_Url, X_FW_Zip, token);
            ExpandZip(X_FW_Zip, X_FW_Extract);
            Directory.CreateDirectory(xstationDir);

            var srcLoader = Directory.EnumerateFiles(X_FW_Extract, "loader.bin", SearchOption.AllDirectories).FirstOrDefault();
            var srcUpdate = Directory.EnumerateFiles(X_FW_Extract, "update.bin", SearchOption.AllDirectories).FirstOrDefault();

            if (srcLoader != null)
            {
                var d = ConfirmReplaceCountdown(loader, OverwriteTimeout, "Xstation loader.bin", token: token);
                if (d == ReplaceDecision.Yes) await RoboCopyFile(srcLoader, xstationDir, token);
                else if (d == ReplaceDecision.No) Info("[SKIP] loader.bin");
                else throw new OperationCanceledException();
            }
            if (srcUpdate != null)
            {
                var d = ConfirmReplaceCountdown(update, OverwriteTimeout, "Xstation update.bin", token: token);
                if (d == ReplaceDecision.Yes) await RoboCopyFile(srcUpdate, xstationDir, token);
                else if (d == ReplaceDecision.No) Info("[SKIP] update.bin");
                else throw new OperationCanceledException();
            }

            if (File.Exists(loader) && File.Exists(update)) Info($"Xstation firmware installed/updated → {xstationDir}");
            else Warn("Could not ensure both loader.bin and update.bin.");
            EnsureRemoved(TempRoot);
        }

        async Task EnsureSarooFirmware(string destDrive, CancellationToken token)
        {
            var sarooBase = $@"{destDrive}:\SAROO";
            var expected = new[] { "cover.bin", "mcuapp.bin", "saroocfg.txt", "ssfirm.bin" }.Select(f => Path.Combine(sarooBase, f)).ToArray();
            if (expected.All(File.Exists)) { Info("SAROO firmware present."); return; }
            if (MessageBox.Show(this, $"SAROO firmware missing in:\n{sarooBase}\n\nDownload now?", "SAROO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(S_Work); Directory.CreateDirectory(S_Work);
            await DownloadFileAsync(S_Url, S_Zip, token);
            ExpandZip(S_Zip, S_Extract);

            var srcSaroo = Path.Combine(S_Extract, @"firm_v0.7\SAROO");
            if (!Directory.Exists(srcSaroo)) throw new Exception($"Missing folder: {srcSaroo}");

            Directory.CreateDirectory(sarooBase);
            Info("Copying SAROO firmware...");
            foreach (var fn in new[] { "cover.bin", "mcuapp.bin", "saroocfg.txt", "ssfirm.bin" })
            {
                token.ThrowIfCancellationRequested();
                var src = Path.Combine(srcSaroo, fn);
                var dst = Path.Combine(sarooBase, fn);
                if (File.Exists(src))
                {
                    var d = ConfirmReplaceCountdown(dst, OverwriteTimeout, $"SAROO {fn}", token: token);
                    if (d == ReplaceDecision.Yes) await RoboCopyFile(src, sarooBase, token);
                    else if (d == ReplaceDecision.No) Info($"[SKIP] {fn}");
                    else throw new OperationCanceledException();
                }
            }

            if (expected.All(File.Exists))
            {
                Info($"SAROO firmware installed/updated → {sarooBase}");
                var cfg = Path.Combine(sarooBase, "saroocfg.txt");
                if (File.Exists(cfg))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(cfg);
                        var pat = Encoding.ASCII.GetBytes("lang_id = 0");
                        int idx = IndexOf(bytes, pat);
                        if (idx >= 0) { bytes[idx + pat.Length - 1] = 0x31; File.WriteAllBytes(cfg, bytes); Info("saroocfg.txt patched (lang_id = 1) without re-encoding."); }
                        else Info("'lang_id = 0' not found; no change made.");
                    }
                    catch (Exception ex) { Warn("Could not patch saroocfg.txt: " + ex.Message); }
                }
            }
            else Warn("Some SAROO files still missing.");
            EnsureRemoved(S_Work);
        }


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

        static async Task<string?> GetLatestAssetUrlAsync(string owner, string repo, string assetNamePattern, string? token = null)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SD-Builder/1.0");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                // Try /releases/latest first
                var latestUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                using (var latestResp = await http.GetAsync(latestUrl))
                {
                    if (latestResp.IsSuccessStatusCode)
                    {
                        var json = await latestResp.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("assets", out var assets))
                        {
                            foreach (var asset in assets.EnumerateArray())
                            {
                                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                                if (System.Text.RegularExpressions.Regex.IsMatch(name, assetNamePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    return asset.GetProperty("browser_download_url").GetString();
                            }
                        }
                    }
                }

                // Fallback: list recent releases
                var listUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=5";
                using (var listResp = await http.GetAsync(listUrl))
                {
                    if (listResp.IsSuccessStatusCode)
                    {
                        var json = await listResp.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        foreach (var rel in doc.RootElement.EnumerateArray())
                        {
                            if (!rel.TryGetProperty("assets", out var assets)) continue;
                            foreach (var asset in assets.EnumerateArray())
                            {
                                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                                if (System.Text.RegularExpressions.Regex.IsMatch(name, assetNamePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    return asset.GetProperty("browser_download_url").GetString();
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
        async Task EnsureGamecubeIPL(string destDrive, CancellationToken token)
        {
            var gcBase = $@"{destDrive}:\";
            Directory.CreateDirectory(gcBase);
            var ipl = Path.Combine(gcBase, "IPL.dol");
            if (File.Exists(ipl)) { Info("Swiss firmware present."); return; }
            if (MessageBox.Show(this, $"IPL.dol not found in:\n{gcBase}\n\nDownload Swiss now?", "Gamecube", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(G_Work); Directory.CreateDirectory(G_Work);
            var latest = await GetLatestAssetUrlAsync(G_Owner, G_Repo, G_AssetPattern);
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
                catch { /* ignore parse errors */ }
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
                try { Directory.Delete(dir, true); Info($"Removed Legacy directory: {dir}"); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }

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
                catch { File.Copy(copied, ipl, true); File.Delete(copied); }
            }

            var legacyOnSd = Path.Combine(gcBase, "Legacy");
            if (Directory.Exists(legacyOnSd)) try { Directory.Delete(legacyOnSd, true); Info("Removed Legacy directory from SD root."); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
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
                catch { /* best-effort hide */ }
                Info("Swiss marker written (.swiss.install.json).");
            }
            catch (Exception ex)
            {
                Info($"Warning: could not write Swiss marker: {ex.Message}");
            }
            EnsureRemoved(G_Work);
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
                _ => throw new InvalidOperationException()
            };
            Directory.CreateDirectory(destRoot);
            Info($"Destination path: {destRoot}");

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
                await CopyEntry(line, platform, destRoot, token);
            }
            Info("All entries processed.");
        }

        async Task CopyEntry(string source, Platform platform, string destRoot, CancellationToken token)
        {




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
                    Info($"Copying folder '{Path.GetFileName(source.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))}' → {GetPlatformCopyLabel(platform)}");
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

        // Settings I/O
        void LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var dto = JsonSerializer.Deserialize<SettingsDTO>(File.ReadAllText(ConfigFile, Encoding.UTF8));
                    if (dto != null)
                    {
                        CopyTimeout = dto.CopyTimeout;
                        OverwriteTimeout = dto.OverwriteTimeout;
                        OverwriteAutoAction = dto.OverwriteAutoAction ?? "Yes";
                        ShowOnlyRemovable = dto.ShowOnlyRemovable ?? true;
                        EjectPromptEnabled = dto.EjectPromptEnabled ?? false;
                        _lmFolder = dto.LmFolder ?? _lmFolder;

                        // Restore window placement (optional)
                        try
                        {
                            if (dto.WindowW.HasValue && dto.WindowH.HasValue && dto.WindowW > 200 && dto.WindowH > 200)
                            {
                                this.StartPosition = FormStartPosition.Manual;
                                var x = dto.WindowX ?? this.Left;
                                var y = dto.WindowY ?? this.Top;
                                var w = dto.WindowW.Value;
                                var h = dto.WindowH.Value;
                                this.SetBounds(x, y, w, h);
                            }
                            if (dto.Maximized == true)
                            {
                                this.WindowState = FormWindowState.Maximized;
                            }
                        }
                        catch { /* ignore bad persisted values */ }
                    }
                }
            }
            catch { Warn("Could not read settings.json, using defaults."); }

            numCopy.Value = Math.Clamp(CopyTimeout, (int)numCopy.Minimum, (int)numCopy.Maximum);
            numOW.Value = Math.Clamp(OverwriteTimeout, (int)numOW.Minimum, (int)numOW.Maximum);
            cmbAuto.SelectedItem = OverwriteAutoAction is "No" ? "No" : "Yes";
            chkOnlyRemovable.Checked = ShowOnlyRemovable;
            try { chkEjectPrompt.Checked = EjectPromptEnabled; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        void SaveSettings()
        {
            var dto = new SettingsDTO(
    (int)numCopy.Value,
    (int)numOW.Value,
    (string)(cmbAuto.SelectedItem ?? "Yes"),
    ShowOnlyRemovable,
    _lmFolder,
    this.WindowState == FormWindowState.Normal ? this.Bounds.X : this.RestoreBounds.X,
    this.WindowState == FormWindowState.Normal ? this.Bounds.Y : this.RestoreBounds.Y,
    this.WindowState == FormWindowState.Normal ? this.Bounds.Width : this.RestoreBounds.Width,
    this.WindowState == FormWindowState.Normal ? this.Bounds.Height : this.RestoreBounds.Height,
    this.WindowState == FormWindowState.Maximized,
        EjectPromptEnabled
);
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json, new UTF8Encoding(false));
            CopyTimeout = (int)numCopy.Value;
            OverwriteTimeout = (int)numOW.Value;
            OverwriteAutoAction = (string)(cmbAuto.SelectedItem ?? "Yes");
        }

        record SettingsDTO(int CopyTimeout, int OverwriteTimeout, string? OverwriteAutoAction, bool? ShowOnlyRemovable, string? LmFolder, int? WindowX, int? WindowY, int? WindowW, int? WindowH, bool? Maximized, bool? EjectPromptEnabled);

        // Countdown dialogs
        void WaitEnterOrTimeout(int seconds, CancellationToken token)
        {
            if (seconds <= 0) return;

            using var dlg = new Form { Text = "Continue", Width = 480, Height = 170, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
            dlg.Icon = Icon;

            var lbl = new Label { Left = 12, Top = 15, Width = 440, Height = 28, AutoSize = false };
            var btnContinue = new Button { Text = "Continue now", Left = 12, Top = 60, Width = 130, DialogResult = DialogResult.OK };
            var btnCancelAll = new Button { Text = "Cancel (stop all)", Left = 160, Top = 60, Width = 140, DialogResult = DialogResult.Cancel };

            int remaining = seconds;
            lbl.Text = $"Auto-continue in {remaining}s… (press Continue or Cancel)";

            using var t = new System.Windows.Forms.Timer { Interval = 1000 };
            t.Tick += (_, __) =>
            {
                remaining--;
                if (remaining <= 0) { t.Stop(); dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                else { lbl.Text = $"Auto-continue in {remaining}s… (press Continue or Cancel)"; }
            };
            dlg.Shown += (_, __) => t.Start();
            btnCancelAll.Click += (_, __) => { try { _cts?.Cancel(); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } };

            token.Register(() => { try { if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); })); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } });

            dlg.Controls.AddRange(new Control[] { lbl, btnContinue, btnCancelAll });
            dlg.AcceptButton = btnContinue;

            var res = dlg.ShowDialog(this);
            t.Stop();
            if (res == DialogResult.Cancel || token.IsCancellationRequested) throw new OperationCanceledException();
        }

        ReplaceDecision ConfirmReplaceCountdown(string targetPath, int seconds, string? itemLabel = null, bool folderPromptOnlyWhenNonEmpty = false, CancellationToken? token = null)
        {
            bool exists = File.Exists(targetPath) || Directory.Exists(targetPath);
            if (!exists) return ReplaceDecision.Yes;
            if (folderPromptOnlyWhenNonEmpty && Directory.Exists(targetPath) && !Directory.EnumerateFileSystemEntries(targetPath).Any()) return ReplaceDecision.Yes;

            bool autoNo = OverwriteAutoAction.Equals("No", StringComparison.OrdinalIgnoreCase);
            int remaining = Math.Max(0, seconds);
            string leaf = itemLabel ?? Path.GetFileName(targetPath);

            using var dlg = new Form { Text = "Confirm replace", Width = 560, Height = 190, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
            dlg.Icon = Icon;
            var lbl = new Label { Left = 12, Top = 12, Width = 520, Height = 40, AutoSize = false, Text = $"Replace existing '{leaf}'?\nDefault after timeout: {(autoNo ? "No" : "Yes")}" };
            var btnYes = new Button { Text = "Yes", Left = 120, Top = 80, Width = 90, DialogResult = DialogResult.Yes };
            var btnNo = new Button { Text = "No", Left = 220, Top = 80, Width = 90, DialogResult = DialogResult.No };
            var btnCancel = new Button { Text = "Cancel (stop all)", Left = 320, Top = 80, Width = 130, DialogResult = DialogResult.Cancel };

            using var t = new System.Windows.Forms.Timer { Interval = 1000 };
            t.Tick += (_, __) =>
            {
                remaining--;
                if (remaining <= 0) { t.Stop(); dlg.DialogResult = autoNo ? DialogResult.No : DialogResult.Yes; dlg.Close(); }
                else dlg.Text = $"Confirm replace – auto {(autoNo ? "No" : "Yes")} in {remaining}s";
            };
            dlg.Shown += (_, __) => t.Start();
            btnCancel.Click += (_, __) => { try { _cts?.Cancel(); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } };
            if (token.HasValue) token.Value.Register(() => { try { if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); })); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } });
            dlg.Controls.AddRange(new Control[] { lbl, btnYes, btnNo, btnCancel });
            var res = dlg.ShowDialog(this);
            t.Stop();
            if (res == DialogResult.Yes) return ReplaceDecision.Yes;
            if (res == DialogResult.No) return ReplaceDecision.No;
            return ReplaceDecision.Cancel;
        }


        // Helper: enumerate all ListBox controls for startup clear
        IEnumerable<ListBox> FindListBoxes(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is ListBox lb) yield return lb;
                foreach (var nested in FindListBoxes(c)) yield return nested;
            }
        }

        // Utilities
        static string Q(string path)
        {
            if (string.IsNullOrEmpty(path)) return "\"\"";
            if (path.EndsWith("\\")) path += ".";
            return $"\"{path}\"";
        }
        async Task DownloadFileAsync(string url, string outFile, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
            Info($"Downloading:\n  {url}\n  -> {outFile}");
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(outFile);
            await resp.Content.CopyToAsync(fs, token);
        }
        void ExpandZip(string zipPath, string dest)
        {
            if (Directory.Exists(dest)) { try { Directory.Delete(dest, true); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } }
            Directory.CreateDirectory(dest);
            Info($"Extracting ZIP → {dest}");
            ZipFile.ExtractToDirectory(zipPath, dest);
        }
        void EnsureRemoved(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } }
        async Task Ensure7zr(CancellationToken token)
        {
            if (!File.Exists(G_7zr)) { Info($"Downloading 7zr.exe → {G_7zr}"); await DownloadFileAsync(G_7zr_Url, G_7zr, token); }
        }
        async Task<int> RunProcess(string exe, string args, CancellationToken token)
        {
            var psi = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.Start();
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            try { await p.WaitForExitAsync(token); }
            catch (OperationCanceledException) { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } throw; }
            return p.ExitCode;
        }
        async Task RoboCopyDir(string source, string dest, CancellationToken token)
        {
            Directory.CreateDirectory(dest);
            var args = $"{Q(source)} {Q(dest)} {string.Join(" ", RoboFlagsDir)}";
            await RunProcess("robocopy.exe", args, token);
        }
        async Task RoboCopyFile(string srcFile, string destDir, CancellationToken token)
        {
            Directory.CreateDirectory(destDir);
            var leaf = Path.GetFileName(srcFile);
            var srcDir = Path.GetDirectoryName(srcFile)!;
            var args = $"{Q(srcDir)} {Q(destDir)} {Q(leaf)} {string.Join(" ", RoboFlagsFile)}";
            await RunProcess("robocopy.exe", args, token);
        }
        static int IndexOf(byte[] hay, byte[] needle)
        {
            if (needle == null || needle.Length == 0) return 0;
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                int k = 0; for (; k < needle.Length; k++) if (hay[i + k] != needle[k]) break;
                if (k == needle.Length) return i;
            }
            return -1;
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
        // ------------------- Listmaker helpers -------------------
        void UpdateLmTotalsFromItems()
        {
            long total = 0;
            int count = 0;
            foreach (ListViewItem it in lvList.Items)
            {
                if (!it.Checked) continue;
                count++;
                if (it.Tag is long sz) total += sz;
            }
            SetCounters(count, total);
        }

        void SetCounters(int checkedCount, long totalBytes)
        {
            if (lblCountLM == null || lblSizeLM == null) return;
            lblCountLM.Text = $"Checked: {checkedCount}";
            lblSizeLM.Text = $"Size: {FormatSize(totalBytes)}";
        }

        string FormatSize(long bytes)
        {
            const long KB = 1024, MB = KB * 1024, GB = MB * 1024, TB = GB * 1024;
            if (bytes >= TB) return $"{bytes / (double)TB:0.##} TB";
            if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
            return $"{bytes} B";
        }
        long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        long SafeDirLength(string root, CancellationToken token, bool recursive)
        {
            long total = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!recursive)
                {
                    foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        token.ThrowIfCancellationRequested();
                        try { total += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                    }
                    return total;
                }

                var stack = new Stack<string>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var dir = stack.Pop();
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(dir))
                        {
                            token.ThrowIfCancellationRequested();
                            try { total += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                        }
                        foreach (var d in Directory.EnumerateDirectories(dir))
                        {
                            token.ThrowIfCancellationRequested();
                            stack.Push(d);
                        }
                    }
                    catch { /* skip inaccessible subpaths */ }
                }
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            return total;
        }


        string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var p = path.Trim();
            if ((p.StartsWith("\"") && p.EndsWith("\"")) || (p.StartsWith("'") && p.EndsWith("'")))
                p = p[1..^1];
            return p.Replace('/', '\\').Trim();
        }

        void AddRowLM(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var it = new ListViewItem(path) { Checked = _lmChecked.Contains(path) };
            it.SubItems.Add(""); // status column
            lvList.Items.Add(it);
        }

        async Task RecalcCountersAsync()
        {
            // cancel any previous scan
            _lmScanCts?.Cancel();
            _lmScanCts = new CancellationTokenSource();
            var token = _lmScanCts.Token;

            // snapshot checked rows
            var rows = lvList.Items.Cast<ListViewItem>().Where(it => it.Checked).ToList();

            // prime: clear Tag and set status
            foreach (var it in rows)
            {
                var pth = it.Text;
                bool exists = rbFiles.Checked ? File.Exists(pth) : Directory.Exists(pth);
                it.Tag = null;
                it.SubItems[1].Text = exists ? "scanning…" : "Missing";
            }
            SetCounters(rows.Count, 0);

            // kick off per-item tasks (throttled)
            var tasks = new List<Task>();
            int parallel = Environment.ProcessorCount > 4 ? 4 : 2;
            using (var sem = new System.Threading.SemaphoreSlim(parallel, parallel))
            {
                foreach (var it in rows)
                {
                    await sem.WaitAsync(token).ConfigureAwait(false);
                    var itemRef = it;
                    var task = Task.Run(() =>
                    {
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            var p = itemRef.Text;
                            long sz = 0;
                            if (rbFiles.Checked)
                            {
                                if (File.Exists(p)) sz = SafeFileLength(p);
                            }
                            else
                            {
                                if (Directory.Exists(p)) sz = SafeDirLength(p, token, recursive: chkRecursive.Checked);
                            }
                            // post result
                            if (!IsDisposed && lvList.IsHandleCreated)
                            {
                                try
                                {
                                    lvList.BeginInvoke(new Action(() =>
                                    {
                                        if (itemRef.ListView == null) return;
                                        itemRef.Tag = sz;
                                        var existsNow = rbFiles.Checked ? File.Exists(p) : Directory.Exists(p);
                                        itemRef.SubItems[1].Text = existsNow ? FormatSize(sz) : "Missing";
                                        UpdateLmTotalsFromItems();
                                    }));
                                }
                                catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                            }
                        }
                        catch { /* ignore cancellations/errors per item */ }
                        finally
                        {
                            sem.Release();
                        }
                    }, token);
                    tasks.Add(task);
                }
                try { await Task.WhenAll(tasks); } catch { /* cancellation */ }
            }
        }

        void ValidatePaths()
        {
            int ok = 0, missing = 0;
            lvList.BeginUpdate();
            foreach (ListViewItem it in lvList.Items)
            {
                var p = it.Text;
                bool exists = rbFiles.Checked ? File.Exists(p) : Directory.Exists(p);
                it.SubItems[1].Text = exists ? "OK" : "Missing";
                if (exists) ok++; else missing++;
            }
            lvList.EndUpdate();
                _lmBulkChange = false;
            lblStatusLM.Text = $"Validated: {ok} OK, {missing} missing.";
        }

        void SaveTxt()
        {
            // Count checked items
            int checkedCount = 0;
            foreach (ListViewItem it in lvList.Items)
                if (it.Checked) checkedCount++;
            if (checkedCount == 0)
            {
                MessageBox.Show(this, "No checked items to save.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Save checked items",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(GameListsDir) ? GameListsDir : ScriptDir,
                FileName = "list.txt"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var lines = new List<string>();
            foreach (ListViewItem it in lvList.Items)
                if (it.Checked) lines.Add(it.Text);

            File.WriteAllLines(sfd.FileName, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            lblStatusLM.Text = $"Saved {lines.Count} items to: {sfd.FileName}";
        }
        void OpenTxtFile(string path)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not read file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var existing = new HashSet<string>(lvList.Items.Cast<ListViewItem>().Select(it => NormalizePath(it.Text)), StringComparer.OrdinalIgnoreCase);

            int added = 0, dupes = 0;
            lvList.BeginUpdate();
            foreach (var raw in lines)
            {
                var norm = NormalizePath(raw);
                if (string.IsNullOrWhiteSpace(norm)) continue;
                if (existing.Contains(norm)) { dupes++; continue; }
                AddRowLM(norm);
                _lmTxtItems.Add(norm);
                existing.Add(norm);
                added++;
            }
            lvList.EndUpdate();
                _lmBulkChange = false;

            lblStatusLM.Text = $"Loaded {added} new, skipped {dupes} duplicates from: {path}";
            if (chkAutoValidate.Checked) ValidatePaths();
            _ = RecalcCountersAsync();
        }
        void WireListContextMenu()
        {
            var cm = new ContextMenuStrip();
            cm.Items.Add("Open", null, (_, __) => OpenSelected());
            cm.Items.Add("Reveal in Explorer", null, (_, __) => RevealSelected());
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("Remove", null, (_, __) =>
            {
                foreach (ListViewItem it in lvList.SelectedItems) lvList.Items.Remove(it);
                _ = RecalcCountersAsync();
            });
            lvList.ContextMenuStrip = cm;
        }

        void OpenSelected()
        {
            foreach (ListViewItem it in lvList.SelectedItems)
                if (File.Exists(it.Text) || Directory.Exists(it.Text))
                    Process.Start(new ProcessStartInfo(it.Text) { UseShellExecute = true });
        }

        void RevealSelected()
        {
            foreach (ListViewItem it in lvList.SelectedItems)
            {
                if (File.Exists(it.Text))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{it.Text}\"") { UseShellExecute = true });
                else if (Directory.Exists(it.Text))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{it.Text}\"") { UseShellExecute = true });
            }




        }

        
void RefreshListmakerList()
        {
            lvList.BeginUpdate();
            _lmBulkChange = true;
            try
            {
                lvList.Items.Clear();

                var filter = (txtFilter.Text ?? "*").Trim();
                var patterns = filter.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .ToArray();
                if (patterns.Length == 0) patterns = new[] { rbFiles.Checked ? "*.*" : "*" };

                var opt = chkRecursive.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                // Gather folder items if a valid folder is selected; otherwise leave empty
                IEnumerable<string> folderItems = Enumerable.Empty<string>();
                bool hasFolder = !string.IsNullOrWhiteSpace(_lmFolder) && Directory.Exists(_lmFolder);
                if (hasFolder)
                {
                    if (rbFiles.Checked)
                    {
                        // Enumerate all files; filtering is applied later to the union
                        folderItems = Directory.EnumerateFiles(_lmFolder, "*", opt);
                    }
                    else
                    {
                        // Enumerate all directories; filtering is applied later
                        folderItems = Directory.EnumerateDirectories(_lmFolder, "*", opt);
                    }
                }
                else
                {
                    lblStatusLM.Text = "No folder selected.";
                }

                // Combine persisted .txt items with current folder items with current folder items
                var combined = new HashSet<string>(folderItems, StringComparer.OrdinalIgnoreCase);
                foreach (var p in _lmTxtItems) combined.Add(p);

                // Apply filter to combined set (case-insensitive, matches filename OR full path)
                IEnumerable<string> final = combined;
                var f = (txtFilter.Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(f) && f != "*" && f != "*.*")
                {
                    bool hasWild = f.IndexOfAny(new[] { '*', '?' }) >= 0;
                    if (hasWild)
                    {
                        string pattern = "^" + Regex.Escape(f).Replace("\\\\*", ".*").Replace("\\\\?", ".") + "$";
                        var rx = new Regex(pattern, RegexOptions.IgnoreCase);
                        final = final.Where(p => rx.IsMatch(p) || rx.IsMatch(Path.GetFileName(p)));
                    }
                    else
                    {
                        final = final.Where(p => p.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                                              || Path.GetFileName(p).IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                }

                                // Populate list
                foreach (var path in final)
                    AddRowLM(path);

                // Restore checked state
                try {
                    _lmBulkChange = true;
                    foreach (ListViewItem it in lvList.Items)
                    {
                        var p = it.Text;
                        it.Checked = !string.IsNullOrWhiteSpace(p) && _lmChecked.Contains(p);
                    }
                } finally { _lmBulkChange = false; }

                lblStatusLM.Text = $"Showing {final.Count()} items";

                _ = RecalcCountersAsync();
            }
            finally
            {
                lvList.EndUpdate();
                _lmBulkChange = false;
            }
        }


        async Task CopyXstationCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\";
            Directory.CreateDirectory(destRoot);
            int copied = 0;

            foreach (ListViewItem it in lvXsList.Items)
            {
                if (!it.Checked) continue;
                var path = it.Text;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Info($"[SKIP] Not found: \"{path}\"");
                    continue;
                }
                await CopyEntry(path, Platform.Xstation, destRoot, token);
                copied++;
            }

            if (copied == 0) Warn("No items checked.");
        }
        private static string GetPlatformCopyLabel(Platform p) => p switch
        {
            Platform.Xstation => "Root",
            Platform.Saroo => "\\Saroo\\ISO\\",
            Platform.Summercart64 => "Roms",
            Platform.Gamecube => "Games",
            _ => "Root"
        };


        // === Robust copy helpers for Saroo ===
        private async System.Threading.Tasks.Task CopyFileRobustAsync(string src, string dest, System.Threading.CancellationToken token)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
            const int BufferSize = 1024 * 1024; // 1 MB
            using (var inStream = new System.IO.FileStream(src, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, BufferSize, useAsync: true))
            using (var outStream = new System.IO.FileStream(dest, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await inStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                {
                    await outStream.WriteAsync(buffer.AsMemory(0, read), token);
                }
            }
            try
            {
                var ti = System.IO.File.GetLastWriteTime(src);
                System.IO.File.SetLastWriteTime(dest, ti);
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        private async System.Threading.Tasks.Task CopyDirectoryRobustAsync(string srcDir, string destDir, System.Threading.CancellationToken token)
        {
            foreach (var dir in System.IO.Directory.GetDirectories(srcDir, "*", System.IO.SearchOption.AllDirectories))
            {
                var rel = dir.Substring(srcDir.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(destDir, rel));
            }
            foreach (var file in System.IO.Directory.GetFiles(srcDir, "*", System.IO.SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                var rel = file.Substring(srcDir.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var dest = System.IO.Path.Combine(destDir, rel);
                await CopyFileRobustAsync(file, dest, token);
            }
        }

        private void btnGcCheckAll_Click(object sender, EventArgs e)
        {

        }
    }
}