
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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SDBuilderWin
{
    public sealed partial class MainForm : Form
    {
        const string AppVersion = "v2.31";

        // ---- URLs
        const string X_FW_Url  = "https://github.com/x-station/xstation-releases/releases/download/2.0.2/update202.zip";
        const string S_Url     = "https://github.com/tpunix/SAROO/releases/download/v0.7/firm_v0.7.zip";
        const string G_Url     = "https://github.com/emukidid/swiss-gc/releases/download/v0.6r1913/swiss_r1913.7z";
        const string G_7zr_Url = "https://www.7-zip.org/a/7zr.exe";
        const string SC64_Url  = "https://github.com/Polprzewodnikowy/N64FlashcartMenu/releases/download/rolling_release/sc64menu.n64";
        const string SC64_64DD_Url = "https://64dd.org/download/sc64_64ddipl.zip";

        // ---- Summercart64 Emulator URLs
        const string SC64_EMU_GB   = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/gb.v64";
        const string SC64_EMU_GBC  = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/gbc.v64";
        const string SC64_EMU_NEON = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/neon64bu.rom";
        const string SC64_EMU_PF   = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/Press-F.z64";
        const string SC64_EMU_SMS  = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/smsPlus64.z64";
        const string SC64_EMU_SOD  = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/sodium64.z64";

        // ---- Paths
        readonly string ScriptDir;
        string TempRoot     => Path.Combine(ScriptDir, "temp");
        string ConfigFile   => Path.Combine(ScriptDir, "settings.json");
        string GameListsDir => Path.Combine(ScriptDir, "GameLists");

        // Xstation temp
        string X_FW_Zip     => Path.Combine(TempRoot, "xstation_fw.zip");
        string X_FW_Extract => Path.Combine(TempRoot, "extracted");

        // Saroo temp
        string S_Work    => Path.Combine(TempRoot, "saroo");
        string S_Zip     => Path.Combine(S_Work, "saroo_fw.zip");
        string S_Extract => Path.Combine(S_Work, "extracted");

        // Swiss temp
        string G_Work    => Path.Combine(TempRoot, "gamecube");
        string G_7z      => Path.Combine(G_Work, "swiss.7z");
        string G_Extract => Path.Combine(G_Work, "extracted");
        string G_7zr     => Path.Combine(G_Work, "7zr.exe");

        // Robocopy flags
        readonly string[] RoboFlagsDir  = new[] { "/E","/IS","/IT","/COPY:DAT","/R:1","/W:1","/NFL","/NDL","/NJH","/NJS","/NP","/Z","/FFT" };
        readonly string[] RoboFlagsFile = new[] { "/IS","/IT","/COPY:DAT","/R:1","/W:1","/NFL","/NDL","/NJH","/NJS","/NP","/FFT" };

        // Settings (defaults)
        string _lmFolder = string.Empty;
        int CopyTimeout = 20;
        int OverwriteTimeout = 10;
        string OverwriteAutoAction = "Yes"; // Yes|No
        bool ShowOnlyRemovable = true;

        CancellationTokenSource? _cts;
        System.Windows.Forms.Timer? _hotplugTimer;

        
        // Async Listmaker size scans
        CancellationTokenSource? _lmScanCts;

        // Listmaker UI performance
        System.Windows.Forms.Timer? _lmDebounce;
        bool _lmBulkChange;
enum Platform { Xstation, Saroo, Gamecube, Summercart64 }
        enum ReplaceDecision { Yes, No, Cancel }

        public MainForm()
        {
            InitializeComponent();
			
			this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            
            // debounce for Listmaker recalculation
            _lmDebounce = new System.Windows.Forms.Timer { Interval = 250 };
            _lmDebounce.Tick += async (_, __) => { _lmDebounce!.Stop(); await RecalcCountersAsync(); };// Title/version
            Text = $"SD-Builder (GUI) {AppVersion}";
            lblVersion.Text = AppVersion;
            Info("Tip: place your .txt files in the GameLists folder next to this .exe.");
            ScriptDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Wire top bar
            btnRefresh.Click += (_, __) => RefreshDrives(preserveSelection: true);
            btnStop.Click    += (_, __) => _cts?.Cancel();
            btnOpen.Click    += (_, __) => OpenCurrentDriveInExplorer();
            btnEject.Click   += async (_, __) => await EjectCurrentDriveAsync();
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
            WirePlatformTab(
                Platform.Summercart64,
                btnSC64Fw, cmbSC64List, btnSC64RefreshLists, btnSC64OpenLists, btnSC64Start,
                btnSC64Install64DD, btnSC64InstallEmulators
            );

            // Wire Listmaker tab
            WireListmakerTab();

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
                RefreshDrives();
                Info($"GameLists dir: {GameListsDir}");
                UpdateDriveButtonsEnabled();
                PositionVersionLabel();
            };
            FormClosed += (_, __) => { if (_hotplugTimer != null) { _hotplugTimer.Stop(); _hotplugTimer.Dispose(); } _lmScanCts?.Cancel(); };
        }

        // ---------- Layout helpers that depend on Designer controls ----------

        void PositionVersionLabel()
        {
            // Keep lblVersion top-right of topPanel
            if (topPanel == null || lblVersion == null) return;
            lblVersion.Left = topPanel.ClientSize.Width - lblVersion.Width - 8;
            lblVersion.Top  = 8;
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
                    case Platform.Saroo:    await EnsureSarooFirmware(d, token);   break;
                    case Platform.Gamecube: await EnsureGamecubeIPL(d, token);     break;
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

            lvList.Resize   += (_, __) => AutoSizeColumns();
            tabListmaker.Enter += (_, __) => AutoSizeColumns();

            // Handlers
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
            btnRefreshLM.Click += (_, __) => RefreshListmakerList();
            chkRecursive.CheckedChanged += (_, __) => RefreshListmakerList();
            rbFiles.CheckedChanged += (_, __) => { if (rbFiles.Checked && txtFilter.Text.Trim() == "*") txtFilter.Text = "*.*"; RefreshListmakerList(); };
            rbDirs.CheckedChanged  += (_, __) => { if (rbDirs.Checked && (txtFilter.Text.Trim() == "*.*" || txtFilter.Text.Trim().Length == 0)) txtFilter.Text = "*"; RefreshListmakerList(); };

            btnCheckAll.Click += (_, __) => { _lmBulkChange = true; lvList.BeginUpdate(); try { for (int i=0;i<lvList.Items.Count;i++) lvList.Items[i].Checked = true; } finally { lvList.EndUpdate(); _lmBulkChange = false; } _lmDebounce?.Stop(); _lmDebounce?.Start(); };
            btnUncheckAll.Click += (_, __) => { _lmBulkChange = true; lvList.BeginUpdate(); try { for (int i=0;i<lvList.Items.Count;i++) lvList.Items[i].Checked = false; } finally { lvList.EndUpdate(); _lmBulkChange = false; } _lmDebounce?.Stop(); _lmDebounce?.Start(); };
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
                    existing.Add(norm);
                    added++;
                }
                lvList.EndUpdate();

                lblStatusLM.Text = $"Loaded {added} new, skipped {dupes} duplicates from: {ofd.FileName}";
                if (chkAutoValidate.Checked) ValidatePaths();
                _ = RecalcCountersAsync();
            };

            btnValidateNow.Click += (_, __) => { ValidatePaths(); };
            btnSaveTxt.Click += (_, __) => SaveTxt();

            lvList.ItemChecked += (_, __) => { if (_lmBulkChange) return; _lmDebounce?.Stop(); _lmDebounce?.Start();};

            // Defaults
            lblStatusLM.Text = "Choose a folder or open a .txt file to begin.";
            SetCounters(0, 0);
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
                await PostRunPromptAsync(d);
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
            const uint GENERIC_READ  = 0x80000000;
            const uint GENERIC_WRITE = 0x40000000;
            const uint FILE_SHARE_READ  = 0x00000001;
            const uint FILE_SHARE_WRITE = 0x00000002;
            const uint OPEN_EXISTING = 3;
            const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
            const uint FSCTL_LOCK_VOLUME       = 0x00090018;
            const uint FSCTL_DISMOUNT_VOLUME   = 0x00090020;
            const uint IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;
            const uint IOCTL_STORAGE_EJECT_MEDIA   = 0x002D4808;

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
            var expected = new[] { "cover.bin","mcuapp.bin","saroocfg.txt","ssfirm.bin" }.Select(f => Path.Combine(sarooBase, f)).ToArray();
            if (expected.All(File.Exists)) { Info("SAROO firmware present."); return; }
            if (MessageBox.Show(this, $"SAROO firmware missing in:\n{sarooBase}\n\nDownload now?", "SAROO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(S_Work); Directory.CreateDirectory(S_Work);
            await DownloadFileAsync(S_Url, S_Zip, token);
            ExpandZip(S_Zip, S_Extract);

            var srcSaroo = Path.Combine(S_Extract, @"firm_v0.7\SAROO");
            if (!Directory.Exists(srcSaroo)) throw new Exception($"Missing folder: {srcSaroo}");

            Directory.CreateDirectory(sarooBase);
            Info("Copying SAROO firmware...");
            foreach (var fn in new[] {"cover.bin","mcuapp.bin","saroocfg.txt","ssfirm.bin"})
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

        async Task EnsureGamecubeIPL(string destDrive, CancellationToken token)
        {
            var gcBase = $@"{destDrive}:\";
            Directory.CreateDirectory(gcBase);
            var ipl = Path.Combine(gcBase, "IPL.dol");
            if (File.Exists(ipl)) { Info("Swiss firmware present."); return; }
            if (MessageBox.Show(this, $"IPL.dol not found in:\n{gcBase}\n\nDownload Swiss now?", "Gamecube", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(G_Work); Directory.CreateDirectory(G_Work);
            await DownloadFileAsync(G_Url, G_7z, token);
            await Ensure7zr(token);
            Directory.CreateDirectory(G_Extract);
            Info($"Extracting 7z → {G_Extract}");
            var rc = await RunProcess(G_7zr, $"x -y -o\"{G_Extract}\" \"{G_7z}\"", token);
            if (rc != 0) throw new Exception($"7zr extraction failed (rc={rc})");

            foreach (var dir in Directory.EnumerateDirectories(G_Extract, "Legacy", SearchOption.AllDirectories))
                try { Directory.Delete(dir, true); Info($"Removed Legacy directory: {dir}"); } catch {}

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
            if (Directory.Exists(legacyOnSd)) try { Directory.Delete(legacyOnSd, true); Info("Removed Legacy directory from SD root."); } catch {}
            Info($"IPL.dol installed → {gcBase}");
            EnsureRemoved(G_Work);
        }
        async Task EnsureSummercart64Firmware(string destDrive, CancellationToken token)
        {
            var root = $@"{destDrive}:\";
            var fw   = Path.Combine(root, "sc64menu.n64");
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
			var ddipl    = System.IO.Path.Combine(destMenu, "64ddipl");

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
    var root     = destDrive + @":\";
    var destMenu = Path.Combine(root, "menu");
    var destEmu  = Path.Combine(destMenu, "Emulators");

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

    var work    = Path.Combine(TempRoot, "sc64_emulators");
    var dlDir   = Path.Combine(work, "dl");
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
                Platform.Saroo    => $@"{destDrive}:\SAROO\ISO",
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
    Info($"Copying folder → '{name}' → {(platform == Platform.Summercart64 ? "Roms" : "Games")}");
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
                    Info($"Copying folder → '{name}'");
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

                Info($"Copying file → '{leaf}'→ {(platform == Platform.Summercart64 ? "Roms" : "Games")}");
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
                    }
                }
            }
            catch { Warn("Could not read settings.json, using defaults."); }

            numCopy.Value = Math.Clamp(CopyTimeout, (int)numCopy.Minimum, (int)numCopy.Maximum);
            numOW.Value   = Math.Clamp(OverwriteTimeout, (int)numOW.Minimum, (int)numOW.Maximum);
            cmbAuto.SelectedItem = OverwriteAutoAction is "No" ? "No" : "Yes";
            chkOnlyRemovable.Checked = ShowOnlyRemovable;
        }

        void SaveSettings()
        {
            var dto = new SettingsDTO((int)numCopy.Value, (int)numOW.Value, (string)(cmbAuto.SelectedItem ?? "Yes"), ShowOnlyRemovable);
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json, new UTF8Encoding(false));
            CopyTimeout = (int)numCopy.Value;
            OverwriteTimeout = (int)numOW.Value;
            OverwriteAutoAction = (string)(cmbAuto.SelectedItem ?? "Yes");
        }

        record SettingsDTO(int CopyTimeout, int OverwriteTimeout, string OverwriteAutoAction, bool? ShowOnlyRemovable);

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
            btnCancelAll.Click += (_, __) => { try { _cts?.Cancel(); } catch { } };

            token.Register(() => { try { if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); })); } catch { } });

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
            var btnNo  = new Button { Text = "No",  Left = 220, Top = 80, Width = 90, DialogResult = DialogResult.No };
            var btnCancel = new Button { Text = "Cancel (stop all)", Left = 320, Top = 80, Width = 130, DialogResult = DialogResult.Cancel };

            using var t = new System.Windows.Forms.Timer { Interval = 1000 };
            t.Tick += (_, __) =>
            {
                remaining--;
                if (remaining <= 0) { t.Stop(); dlg.DialogResult = autoNo ? DialogResult.No : DialogResult.Yes; dlg.Close(); }
                else dlg.Text = $"Confirm replace – auto {(autoNo ? "No" : "Yes")} in {remaining}s";
            };
            dlg.Shown += (_, __) => t.Start();
            btnCancel.Click += (_, __) => { try { _cts?.Cancel(); } catch { } };
            if (token.HasValue) token.Value.Register(() => { try { if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); })); } catch { } });
            dlg.Controls.AddRange(new Control[] { lbl, btnYes, btnNo, btnCancel });
            var res = dlg.ShowDialog(this);
            t.Stop();
            if (res == DialogResult.Yes) return ReplaceDecision.Yes;
            if (res == DialogResult.No)  return ReplaceDecision.No;
            return ReplaceDecision.Cancel;
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
            if (Directory.Exists(dest)) { try { Directory.Delete(dest, true); } catch { } }
            Directory.CreateDirectory(dest);
            Info($"Extracting ZIP → {dest}");
            ZipFile.ExtractToDirectory(zipPath, dest);
        }
        void EnsureRemoved(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); } catch { } }
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
            catch (OperationCanceledException) { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { } throw; }
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
            var leaf   = Path.GetFileName(srcFile);
            var srcDir = Path.GetDirectoryName(srcFile)!;
            var args   = $"{Q(srcDir)} {Q(destDir)} {Q(leaf)} {string.Join(" ", RoboFlagsFile)}";
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
        sealed class ComboItem { public string Text { get; } public string FullPath { get; } public ComboItem(string text, string fullPath) { Text = text; FullPath = fullPath; } public override string ToString() => Text; 
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
            lblSizeLM.Text  = $"Size: {FormatSize(totalBytes)}";
        }

        string FormatSize(long bytes)
        {
            const long KB = 1024, MB = KB*1024, GB = MB*1024, TB = GB*1024;
            if (bytes >= TB) return $"{bytes/(double)TB:0.##} TB";
            if (bytes >= GB) return $"{bytes/(double)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes/(double)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes/(double)KB:0.##} KB";
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
                try { total += new FileInfo(f).Length; } catch { }
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
                    try { total += new FileInfo(f).Length; } catch { }
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
    catch { }
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
            var it = new ListViewItem(path) { Checked = false };
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
                        catch { }
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
            lblStatusLM.Text = $"Validated: {ok} OK, {missing} missing.";
        }

        void SaveTxt()
        {
                        // Block saving when nothing is checked
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

            File.WriteAllLines(sfd.FileName, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier:false));
            lblStatusLM.Text = $"Saved {lines.Count} items to: {sfd.FileName}";
        }

        void RefreshListmakerList()
        {
            lvList.BeginUpdate();
            try
            {
                lvList.Items.Clear();
                if (string.IsNullOrWhiteSpace(_lmFolder) || !Directory.Exists(_lmFolder))
                {
                    lblStatusLM.Text = "No folder selected.";
                    SetCounters(0, 0);
                    return;
                }

                var filter = (txtFilter.Text ?? "*").Trim();
                var patterns = filter.Split(new[]{';','|'}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                if (patterns.Length == 0) patterns = new[] { rbFiles.Checked ? "*.*" : "*" };

                IEnumerable<string> items = Enumerable.Empty<string>();
                var opt = chkRecursive.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                if (rbFiles.Checked)
                {
                    foreach (var pat in patterns)
                        items = items.Concat(Directory.EnumerateFiles(_lmFolder, pat, opt));
                }
                else
                {
                    // Directories: ignore pattern and just enumerate all, with optional name filter
                    items = Directory.EnumerateDirectories(_lmFolder, "*", opt);
                    if (filter != "*" && filter != "*.*")
                    {
                        var needle = filter.Trim('*').ToLowerInvariant();
                        items = items.Where(d => d.ToLowerInvariant().Contains(needle));
                    }
                }

                int added = 0;
                foreach (var it in items)
                {
                    AddRowLM(it);
                    added++;
                }

                lblStatusLM.Text = $"Loaded {added} item(s) from: {_lmFolder}";
            }
            catch (Exception ex)
            {
                lblStatusLM.Text = "Error: " + ex.Message;
            }
            finally
            {
                lvList.EndUpdate();
            }
            _ = RecalcCountersAsync();
        }

    }
}