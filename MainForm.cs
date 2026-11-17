
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

// ===== START AUTO-FIX =====
#nullable enable

namespace SDBuilderWin
{
    public sealed partial class MainForm : Form
    {

        const string AppVersion = "v2.31";

        const int XsStatusColWidth = 160;

        // ---- Paths
        readonly string ScriptDir;
        string TempRoot => Path.Combine(ScriptDir, "temp");
        string ConfigFile => Path.Combine(ScriptDir, "settings.json");
        string GameListsDir => Path.Combine(ScriptDir, "GameLists");
        int CopyTimeout = 20;
        int OverwriteTimeout = 10;
        string OverwriteAutoAction = "Yes"; // Yes|No
        bool ShowOnlyRemovable = true;
        bool EjectPromptEnabled = false;

        CancellationTokenSource? _cts;
        enum Platform { Xstation, Saroo, Gamecube, Summercart64, Dreamcast }
        enum ReplaceDecision { Yes, No, Cancel }


        public MainForm()
        {
            InitializeComponent();
            // Dreamcast "Preview List" / "Rebuild Menu" buttons (Platforms\Dreamcast\MainForm.DreamcastMenu.cs)
            WireDreamcastMenuButtons();

            lvXsList.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvSrList.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvGcList.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvScList.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvList.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvDcList.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvXsList.ColumnClick += OnListViewColumnClick;
            lvSrList.ColumnClick += OnListViewColumnClick;
            lvGcList.ColumnClick += OnListViewColumnClick;
            lvScList.ColumnClick += OnListViewColumnClick;
            lvList.ColumnClick += OnListViewColumnClick;
            lvDcList.ColumnClick += OnListViewColumnClick;

            // Wire Xstation controls
            WireXstationListmaker();

            // Wire Saroo controls
            WireSarooListmaker();

            // Wire Gamecube controls (listmaker)
            WireGamecubeListmaker();

            // Wire Dreamcast controls (listmaker)
            WireDreamcastListmaker();

            // Gamecube search debounce + wiring
            _gcFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _gcFilterDebounce.Tick += (_, __) => { _gcFilterDebounce!.Stop(); RefreshGamecubeList(); };
            if (txtGcFilter != null)
            {
                txtGcFilter.TextChanged += (_, __) => { _gcFilterDebounce?.Stop(); _gcFilterDebounce?.Start(); };
                txtGcFilter.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        _gcFilterDebounce?.Stop();
                        RefreshGamecubeList();
                    }
                };
            }
            if (btnGcClearFilter != null && txtGcFilter != null)
            {
                btnGcClearFilter.Click += (_, __) =>
                {
                    txtGcFilter.Text = string.Empty;
                    _gcFilterDebounce?.Stop();
                    RefreshGamecubeList();
                };
            }

            // Dreamcast search debounce + wiring
            _dcFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _dcFilterDebounce.Tick += (_, __) => { _dcFilterDebounce!.Stop(); RefreshDreamcastList(); };
            if (txtDcFilter != null)
            {
                txtDcFilter.TextChanged += (_, __) => { _dcFilterDebounce?.Stop(); _dcFilterDebounce?.Start(); };
                txtDcFilter.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        _dcFilterDebounce?.Stop();
                        RefreshDreamcastList();
                    }
                };
            }
            if (btnDcClearFilter != null && txtDcFilter != null)
            {
                btnDcClearFilter.Click += (_, __) =>
                {
                    txtDcFilter.Text = string.Empty;
                    _dcFilterDebounce?.Stop();
                    RefreshDreamcastList();
                };
            }

            // Wire Summercart64 controls (listmaker)
            WireSummercartListmaker();

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

                    StartListmakerAutoScan();
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
            btnGamecubeCheats.Click += async (_, __) => await WithDriveJobAsync(InstallGamecubeCheats);

            WirePlatformTab(
                Platform.Dreamcast,
                btnDreamcastFw, cmbDreamcastList, btnDreamcastRefreshLists, btnDreamcastOpenLists, btnDreamcastStart,
                null, null
            );

            WirePlatformTab(
                Platform.Summercart64,
                btnSC64Fw, cmbSC64List, btnSC64RefreshLists, btnSC64OpenLists, btnSC64Start,
                btnSC64Install64DD, btnSC64InstallEmulators
            );

            // Wire Listmaker tab
            WireListmakerTab();

            // Xstation search debounce + wiring
            _xsFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _xsFilterDebounce.Tick += (_, __) => { _xsFilterDebounce!.Stop(); RefreshXstationList(); };
            if (txtXsFilter != null)
            {
                txtXsFilter.TextChanged += (_, __) => { _xsFilterDebounce?.Stop(); _xsFilterDebounce?.Start(); };
                txtXsFilter.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        _xsFilterDebounce?.Stop();
                        RefreshXstationList();
                    }
                };

                if (btnXsClearFilter != null && txtXsFilter != null)
                {
                    btnXsClearFilter.Click += (_, __) =>
                    {
                        txtXsFilter.Text = string.Empty;
                        _xsFilterDebounce?.Stop();
                        RefreshXstationList();
                    };
                }

            }


            // Live filter as you type
            txtFilter.TextChanged += (_, __) => { _lmFilterDebounce?.Stop(); _lmFilterDebounce?.Start(); };
            txtFilter.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; _lmFilterDebounce?.Stop(); RefreshListmakerList(); } };

            // Always include subfolders; hide the checkbox to simplify UI
            chkRecursive.Checked = true; chkRecursive.Visible = false;
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
                        SetLmStatus("Loading...");
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
                    lvList.Items.Clear();
                    SetLmStatus("Choose a folder or open a .txt file to begin.");
                }

                // Clear all ListBox controls on startup so previous session entries don't persist
                foreach (var lb in FindListBoxes(this)) lb.Items.Clear();
                this.FormClosing += (_, __) => SaveSettings();
                if (!File.Exists("settings.json"))
                {
                    SaveSettings();
                }
                chkEjectPrompt.CheckedChanged += (_, __) => { EjectPromptEnabled = chkEjectPrompt.Checked; SaveSettings(); };
                RefreshDrives();
                Info($"GameLists dir: {GameListsDir}");
                // Start GameLists poller (fallback if FileSystemWatcher misses events).
                // GetGameListsSignature / RefreshAllGameLists handle (and report) read errors themselves.
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
                        lvList.EndUpdate();
                        _lmBulkChange = false;
                    }
                }
                SetLmStatus("Choose a folder or open a .txt file to begin.");
                SetCounters(0, 0);
            };

            FormClosed += (_, __) =>
            {
                if (_hotplugTimer != null)
                {
                    _hotplugTimer.Stop(); _hotplugTimer.Dispose();
                }
                // Stop background list scans (they check IsDisposed before touching the window anyway).
                foreach (var cts in new[] { _xsScanCts, _srScanCts, _gcScanCts, _scScanCts, _dcScanCts, _lmScanCts, _lmListCts })
                    cts?.Cancel();
            };
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

    }
}