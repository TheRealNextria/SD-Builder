// Version: v2.34 - Master + Summercart64_64DDIPL
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
    public sealed class MainForm : Form
    {
        const string AppVersion = "v2.0";

        // ---- URLs
        const string X_FW_Url  = "https://github.com/x-station/xstation-releases/releases/download/2.0.2/update202.zip";
        const string S_Url     = "https://github.com/tpunix/SAROO/releases/download/v0.7/firm_v0.7.zip";
        const string G_Url     = "https://github.com/emukidid/swiss-gc/releases/download/v0.6r1913/swiss_r1913.7z";
        const string G_7zr_Url = "https://www.7-zip.org/a/7zr.exe";
        const string SC64_Url  = "https://github.com/Polprzewodnikowy/N64FlashcartMenu/releases/download/rolling_release/sc64menu.n64";
        const string EMU_PressF_Url = "https://github.com/celerizer/Press-F-Ultra/releases/download/r5/Press-F.z64";
        const string EMU_SMS_Url   = "https://github.com/fhoedemakers/smsplus64/releases/download/v0.7/smsPlus64.z64";
        const string EMU_Sodium_Url= "https://github.com/Hydr8gon/sodium64/releases/download/release/sodium64.zip";
        const string EMU_Neon_Url  = "https://lambertjamesd.github.io/df88ffea-3a85-45bb-bc4f-d8a2e3282cab";
        const string EMU_Misc_Url  = "https://drive.usercontent.google.com/download?id=1zmKO4MRZm-U-10AyK7Z0kZwfihctjrDf&export=download&authuser=0";
        const string SC64_64DD_Url = "https://64dd.org/download/sc64_64ddipl.zip";

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
        int CopyTimeout = 20;
        int OverwriteTimeout = 10;
        string OverwriteAutoAction = "Yes"; // Yes|No
        bool ShowOnlyRemovable = true;

        // UI top
        ComboBox cmbDrives = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        Button btnRefresh  = new() { Text = "Refresh", Width = 80, Height = 28, Enabled = true };
        Button btnStop     = new() { Text = "Stop", Width = 80, Height = 28, Enabled = false };
        Button btnOpen     = new() { Text = "Open in Explorer", Width = 130, Height = 28, Enabled = true };
        Button btnEject    = new() { Text = "Eject safely", Width = 110, Height = 28, Enabled = true };
        TextBox txtLog     = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
        TabControl tabs    = new() { Dock = DockStyle.Fill };

        // Settings controls
        NumericUpDown numCopy = new() { Minimum = 0, Maximum = 999, Value = 20, Width = 80 };
        NumericUpDown numOW   = new() { Minimum = 0, Maximum = 999, Value = 10, Width = 80 };
        ComboBox cmbAuto      = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        CheckBox chkOnlyRemovable = new() { Text = "Only show removable drives (USB/SD)", AutoSize = true, Checked = true };

        CancellationTokenSource? _cts;
        System.Windows.Forms.Timer? _hotplugTimer;

        enum Platform { Xstation, Saroo, Gamecube, Summercart64 }
        enum ReplaceDecision { Yes, No, Cancel }

        public MainForm()
        {
            // v2.31: log tip once on startup instead of showing per-tab label
            Info("Tip: place your .txt files in the Gamelists folder next to this .exe.");

            ScriptDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Text = $"SD-Builder (GUI) {AppVersion}";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            ShowIcon = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Top bar
            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10, 8, 10, 8) };
            var driveRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0), Padding = new Padding(0) };
            var lblDrive = new Label { Text = "Destination drive:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
            driveRow.Controls.Add(lblDrive);
            driveRow.Controls.Add(cmbDrives);
            btnRefresh.Margin = new Padding(8, 0, 0, 0);
            btnRefresh.Click += (_, __) => RefreshDrives(preserveSelection: true);
            driveRow.Controls.Add(btnRefresh);
            btnStop.Margin = new Padding(12, 0, 0, 0);
            btnStop.Click += (_, __) => _cts?.Cancel();
            driveRow.Controls.Add(btnStop);
            btnOpen.Margin = new Padding(8, 0, 0, 0);
            btnOpen.Click += (_, __) => OpenCurrentDriveInExplorer();
            driveRow.Controls.Add(btnOpen);
            btnEject.Margin = new Padding(8, 0, 0, 0);
            btnEject.Click += (_, __) => _ = EjectCurrentDriveAsync();
            driveRow.Controls.Add(btnEject);
            top.Controls.Add(driveRow);

            // Version label (top-right)
            var lblVersion = new Label { Text = AppVersion, AutoSize = true, ForeColor = Color.DimGray };
            lblVersion.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            top.Controls.Add(lblVersion);
            void PositionVersionLabel()
            {
                lblVersion.Left = top.ClientSize.Width - lblVersion.Width - 8;
                lblVersion.Top  = 8;
            }
            lblVersion.BringToFront();
            PositionVersionLabel();
            top.Resize += (_, __) => PositionVersionLabel();
            Shown += (_, __) => PositionVersionLabel();

            // Tabs
            tabs.TabPages.Add(BuildPlatformTab("Xstation", Platform.Xstation));
            tabs.TabPages.Add(BuildPlatformTab("Saroo", Platform.Saroo));
            tabs.TabPages.Add(BuildPlatformTab("Gamecube", Platform.Gamecube));
            tabs.TabPages.Add(BuildPlatformTab("Summercart64", Platform.Summercart64));
            tabs.TabPages.Add(BuildListmakerTab());
            tabs.TabPages.Add(BuildSettingsTab());

            // Log
            var grpLog = new GroupBox { Dock = DockStyle.Bottom, Height = 260, Text = "Log" };
            grpLog.Controls.Add(txtLog);

            Controls.AddRange(new Control[] { tabs, grpLog, top });

            Load += (_, __) =>
            {
                Directory.CreateDirectory(GameListsDir);
                LoadSettings();
                RefreshDrives();
                Info($"GameLists dir: {GameListsDir}");
                UpdateDriveButtonsEnabled();
            };

            FormClosed += (_, __) => { if (_hotplugTimer != null) { _hotplugTimer.Stop(); _hotplugTimer.Dispose(); } };
            cmbDrives.SelectedIndexChanged += (_, __) => UpdateDriveButtonsEnabled();
        }

        // Logging
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

        // Platform tab
        TabPage BuildPlatformTab(string title, Platform p)
        {
            var page = new TabPage(title);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var row2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

            var btnFw = new Button { Text = "Check/Download firmware", Height = 34, Width = 240 };
            Button? btnInstall64DD = null;
            if (p == Platform.Summercart64) btnInstall64DD = new Button { Text = "Install 64DD IPL", Height = 34, Width = 160 };
            Button? btnInstallEmu = null;
            if (p == Platform.Summercart64) btnInstallEmu = new Button { Text = "Install Emulators", Height = 34, Width = 180 };
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

            if (p == Platform.Summercart64 && btnInstall64DD != null)
            {
                btnInstall64DD.Click += async (_, __) => await WithDriveJobAsync(async (d, token) =>
                {
                    await InstallSummercart64Ipl(d, token);
                });

                if (btnInstallEmu != null)
                {
                    btnInstallEmu.Click += async (_, __) => await WithDriveJobAsync(async (d, token) =>
                    {
                        await InstallSummercart64Emulators(d, token);
                    });
                }
            }

            var lblList = new Label { Text = "List:", AutoSize = true, Margin = new Padding(12, 8, 6, 0) };
            var cmbList = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
            var btnRefreshLists = new Button { Text = "Refresh lists", Height = 34, Width = 120 };
            var btnOpenLists    = new Button { Text = "Open GameLists Folder", Height = 34, Width = 180 };
            var pnlListButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(8, 0, 0, 0), Padding = new Padding(0) };
            pnlListButtons.Controls.Add(btnRefreshLists);
            pnlListButtons.Controls.Add(new Label { AutoSize = false, Height = 4, Width = 1, Margin = new Padding(0) });
            pnlListButtons.Controls.Add(btnOpenLists);
            var btnStart = new Button { Text = "Start", Height = 34, Width = 100, Enabled = false };

            // Wire list
            btnRefreshLists.Click += (_, __) => { PopulateListCombo(cmbList); btnStart.Enabled = cmbList.Items.Count > 0; };
            cmbList.SelectedIndexChanged += (_, __) => btnStart.Enabled = cmbList.SelectedIndex >= 0;
            btnOpenLists.Click += (_, __) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{GameListsDir}\"") { UseShellExecute = true });

            PopulateListCombo(cmbList);
            btnStart.Enabled = cmbList.Items.Count > 0;

            btnStart.Click += async (_, __) =>
            {
                var list = GetSelectedListPath(cmbList);
                if (list == null) { Warn("No list selected."); return; }
                await WithDriveJobAsync((d, token) => ReadListAndCopy(list, p, d, token));
            };

            // Layout
            row1.Controls.Add(btnFw);
            row1.Controls.Add(lblList);
            row1.Controls.Add(cmbList);
            row1.Controls.Add(pnlListButtons);
            row1.Controls.Add(btnStart);
            
            if (p == Platform.Summercart64 && btnInstall64DD != null) row2.Controls.Add(btnInstall64DD);
            if (p == Platform.Summercart64 && btnInstallEmu != null) row2.Controls.Add(btnInstallEmu);
            root.Controls.Add(row1, 0, 0);
            root.Controls.Add(row2, 0, 1);
            page.Controls.Add(root);
            return page;
        }

        // Settings tab
        TabPage BuildSettingsTab()
        {
            var page = new TabPage("Settings");
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10), AutoScroll = true };

            pnl.Controls.Add(new Label { Text = "Drive list:", AutoSize = true });
            pnl.Controls.Add(chkOnlyRemovable);
            chkOnlyRemovable.CheckedChanged += (_, __) =>
            {
                ShowOnlyRemovable = chkOnlyRemovable.Checked;
                SaveSettings();
                RefreshDrives(preserveSelection: true);
            };

            pnl.Controls.Add(new Label { Text = "Copy timeout (seconds):", AutoSize = true, Margin = new Padding(0, 12, 0, 0) });
            pnl.Controls.Add(numCopy);

            pnl.Controls.Add(new Label { Text = "Overwrite countdown (seconds):", AutoSize = true });
            pnl.Controls.Add(numOW);

            pnl.Controls.Add(new Label { Text = "Overwrite auto action:", AutoSize = true });
            cmbAuto.Items.AddRange(new object[] { "Yes", "No" });
            cmbAuto.SelectedIndex = 0;
            pnl.Controls.Add(cmbAuto);

            var btnSave = new Button { Text = "Save Settings", Width = 140, Height = 30, Margin = new Padding(0, 12, 0, 0) };
            btnSave.Click += (_, __) =>
            {
                CopyTimeout = (int)numCopy.Value;
                OverwriteTimeout = (int)numOW.Value;
                OverwriteAutoAction = (string)(cmbAuto.SelectedItem ?? "Yes");
                SaveSettings();
                Info("Settings saved.");
            };
            pnl.Controls.Add(btnSave);

            page.Controls.Add(pnl);
            return page;
        }

        // Listmaker tab
        
        TabPage BuildListmakerTab()
        {
            var page = new TabPage("Listmaker");

            var btnChooseFolder = new Button { Text = "Choose Folder..." };
            var txtFilter       = new TextBox { Text = "*.*", Width = 280 };
            var chkRecursive    = new CheckBox { Text = "Include subfolders" };
            var btnRefresh      = new Button { Text = "Refresh" };

            var top1 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(8) };
            top1.Controls.AddRange(new Control[] {
                btnChooseFolder,
                new Label{ Text="  Filter (e.g. *.iso;*.gcm or *Zelda*): ", AutoSize=true, Padding=new Padding(8,6,4,0)},
                txtFilter, chkRecursive, btnRefresh
            });

            var rbFiles = new RadioButton { Text = "Files", Checked = true };
            var rbDirs  = new RadioButton { Text = "Directories" };

            var top2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
            top2.Controls.AddRange(new Control[] { new Label{ Text="Mode:", AutoSize=true, Padding=new Padding(0,6,4,0)}, rbFiles, rbDirs });

            var lv = new ListView { CheckBoxes = true, View = View.Details, FullRowSelect = true, HideSelection = false, Dock = DockStyle.Fill };
            lv.Columns.Add("Path", -2, HorizontalAlignment.Left);
            lv.Columns.Add("Status", 100, HorizontalAlignment.Left);

            var btnCheckAll       = new Button { Text = "Check All" };
            var btnUncheckAll     = new Button { Text = "Uncheck All" };
            var btnDeleteSelected = new Button { Text = "Delete Selected" };
            var chkAutoValidate   = new CheckBox { Text = "Auto-validate paths" };
            var btnValidateNow    = new Button { Text = "Validate Now" };
            var btnOpenTxt        = new Button { Text = "Open .txt..." };

            var btnSaveTxt        = new Button { Text = "Save checked...", AutoSize = true };

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            bottom.Controls.AddRange(new Control[] { btnCheckAll, btnUncheckAll, btnDeleteSelected,
                new Label{ Text="   ", AutoSize = true, Width = 16 },
                chkAutoValidate, btnValidateNow,
                new Label{ Text="   ", AutoSize = true, Width = 16 },
                btnOpenTxt, btnSaveTxt
            });

            var status   = new StatusStrip();
            var lblStatus= new ToolStripStatusLabel();              // left
            var lblSep1  = new ToolStripStatusLabel("   |   ");
            var lblSpacer= new ToolStripStatusLabel(); lblSpacer.Spring = true;
            var lblCount = new ToolStripStatusLabel();              // "Checked: N"
            var lblSep2  = new ToolStripStatusLabel("   |   ");
            var lblSize  = new ToolStripStatusLabel();              // "Size: X"
            lblSize.TextAlign = ContentAlignment.MiddleRight;
            status.Items.AddRange(new ToolStripItem[] { lblStatus, lblSep1, lblSpacer, lblCount, lblSep2, lblSize });
            status.Dock = DockStyle.Bottom;

            page.Controls.AddRange(new Control[] { lv, top2, top1, bottom, status });

            string folder = string.Empty;

            // ---------- Helpers ----------
            static IEnumerable<string> SplitPatterns(string patternText) =>
                patternText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim())
                           .Where(s => s.Length > 0);

            static bool MatchesGlob(string text, string glob)
            {
                string rx = "^" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                return Regex.IsMatch(text, rx, RegexOptions.IgnoreCase);
            }

            static string NormalizePath(string p) => p.Replace('/', '\\').TrimEnd('\\');

            void AutoSizeColumns()
            {
                if (lv.Columns.Count == 0) return;
                lv.Columns[0].Width = Math.Max(120, lv.ClientSize.Width - 120);
                lv.Columns[1].Width = 100;
            }

            void AddRow(string path)
            {
                var item = new ListViewItem(path) { Checked = false };
                item.SubItems.Add("");
                lv.Items.Add(item);
            }

            // ---------- Live counters (local) ----------
            System.Threading.CancellationTokenSource? calcCts = null;

            void SetCounters(int checkedCount, long bytes)
            {
                lblCount.Text = $"Checked: {checkedCount}";
                lblSize.Text  = $"Size: {FormatBytes(bytes)}";
            }

            string FormatBytes(long b)
            {
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                double v = b;
                int u = 0;
                while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
                return u == 0 ? $"{b} {units[u]}" : $"{v:0.##} {units[u]}";
            }

            long SafeFileLength(string path)
            {
                try { var fi = new FileInfo(path); return fi.Exists ? fi.Length : 0L; }
                catch { return 0L; }
            }

            long SafeDirLength(string path, System.Threading.CancellationToken token)
            {
                long total = 0;
                try
                {
                    var stack = new Stack<string>();
                    stack.Push(path);
                    while (stack.Count > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        var dir = stack.Pop();
                        try
                        {
                            foreach (var f in Directory.EnumerateFiles(dir))
                            {
                                token.ThrowIfCancellationRequested();
                                total += SafeFileLength(f);
                            }
                            foreach (var d in Directory.EnumerateDirectories(dir))
                            {
                                token.ThrowIfCancellationRequested();
                                stack.Push(d);
                            }
                        }
                        catch { /* ignore inaccessible subpaths */ }
                    }
                }
                catch { /* ignore */ }
                return total;
            }

            async Task RecalcCountersAsync()
            {
                calcCts?.Cancel();
                calcCts = new System.Threading.CancellationTokenSource();
                var token = calcCts.Token;

                var items = lv.Items.Cast<ListViewItem>()
                    .Where(it => it.Checked)
                    .Select(it => it.Text)
                    .ToList();
                int checkedCount = items.Count;

                SetCounters(checkedCount, 0);

                long bytes = 0;
                try
                {
                    await Task.Run(() =>
                    {
                        foreach (var p in items)
                        {
                            token.ThrowIfCancellationRequested();
                            if (rbFiles.Checked)
                            {
                                bytes += SafeFileLength(p);
                            }
                            else
                            {
                                if (Directory.Exists(p)) bytes += SafeDirLength(p, token);
                            }
                        }
                    }, token);
                }
                catch (OperationCanceledException) { return; }

                if (!token.IsCancellationRequested) SetCounters(checkedCount, bytes);
            }

            void RefreshList()
            {
                lv.BeginUpdate();
                lv.Items.Clear();

                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    lblStatus.Text = "No folder selected.";
                    lv.EndUpdate();
                    SetCounters(0, 0);
                    return;
                }

                var rawPatterns = SplitPatterns(txtFilter.Text).ToList();
                List<string> patterns;
                if (rbDirs.Checked)
                {
                    if (rawPatterns.Count == 0 || rawPatterns.All(p => p.Contains('.'))) patterns = new List<string> { "*" };
                    else patterns = rawPatterns;
                }
                else
                {
                    patterns = rawPatterns.Count == 0 ? new List<string> { "*.*" } : rawPatterns;
                }

                int count = 0;
                try
                {
                    var opt = chkRecursive.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    if (rbFiles.Checked)
                    {
                        foreach (var file in Directory.EnumerateFiles(folder, "*", opt))
                            if (patterns.Any(p => MatchesGlob(Path.GetFileName(file), p))) { AddRow(NormalizePath(file)); count++; }
                    }
                    else
                    {
                        foreach (var dir in Directory.EnumerateDirectories(folder, "*", opt))
                            if (patterns.Any(p => MatchesGlob(Path.GetFileName(dir), p))) { AddRow(NormalizePath(dir)); count++; }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(page, "Could not read folder:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                lv.EndUpdate();
                lblStatus.Text = $"Loaded {count} {(rbDirs.Checked ? "directories" : "files")} from: {folder}";

                if (chkAutoValidate.Checked) ValidatePaths();
                _ = RecalcCountersAsync();
            }

            void ValidatePaths()
            {
                foreach (ListViewItem it in lv.Items)
                {
                    var p = NormalizePath(it.Text);
                    bool exists = rbDirs.Checked ? Directory.Exists(p) : File.Exists(p);
                    it.SubItems[1].Text = exists ? "" : "missing";
                    it.ForeColor = exists ? SystemColors.WindowText : Color.Red;
                }
                _ = RecalcCountersAsync();
            }

            List<string> GatherForSave()
            {
                var list = new List<string>();
                if (lv.CheckedItems == null || lv.CheckedItems.Count == 0)
                {
                    MessageBox.Show(page, "Check one or more items in the list to save.", "Nothing checked",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return list;
                }
                foreach (ListViewItem it in lv.CheckedItems)
                    list.Add(NormalizePath(it.Text));
                return list;
            }

void SaveTxt()
            {
                var paths = GatherForSave();
                if (paths.Count == 0) { return; }
                using var sfd = new SaveFileDialog { Title = "Save file list", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", DefaultExt = "txt", AddExtension = true };
                sfd.InitialDirectory = Directory.Exists(GameListsDir) ? GameListsDir : ScriptDir;
                if (sfd.ShowDialog(page) != DialogResult.OK) return;
                try
                {
                    File.WriteAllLines(sfd.FileName, paths, new UTF8Encoding(false));
                    MessageBox.Show(page, $"Saved {paths.Count} paths to:\n{sfd.FileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(page, "Could not save file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // ---------- Events ----------
            btnChooseFolder.Click += (_, __) => { using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false }; if (dlg.ShowDialog(page) == DialogResult.OK) { folder = dlg.SelectedPath; RefreshList(); } };
            btnRefresh.Click += (_, __) => RefreshList();
            chkRecursive.CheckedChanged += (_, __) => RefreshList();
            rbFiles.CheckedChanged += (_, __) => { if (rbFiles.Checked && txtFilter.Text.Trim() == "*") txtFilter.Text = "*.*"; RefreshList(); };
            rbDirs.CheckedChanged  += (_, __) => { if (rbDirs.Checked && (txtFilter.Text.Trim() == "*.*" || txtFilter.Text.Trim().Length == 0)) txtFilter.Text = "*"; RefreshList(); };

            btnCheckAll.Click += (_, __) => { foreach (ListViewItem it in lv.Items) it.Checked = true; _ = RecalcCountersAsync(); };
            btnUncheckAll.Click += (_, __) => { foreach (ListViewItem it in lv.Items) it.Checked = false; _ = RecalcCountersAsync(); };
            btnDeleteSelected.Click += (_, __) =>
            {
                if (lv.SelectedItems.Count == 0) { MessageBox.Show(page, "No items selected to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                foreach (ListViewItem it in lv.SelectedItems.Cast<ListViewItem>().ToList()) lv.Items.Remove(it);
                lblStatus.Text = "Deleted selected items.";
                _ = RecalcCountersAsync();
            };

            btnOpenTxt.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog { Title = "Open text file with paths", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", InitialDirectory = Directory.Exists(GameListsDir) ? GameListsDir : ScriptDir };
                if (ofd.ShowDialog(page) != DialogResult.OK) return;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(ofd.FileName, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(page, "Could not read file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var existing = new HashSet<string>(lv.Items.Cast<ListViewItem>().Select(it => NormalizePath(it.Text)), StringComparer.OrdinalIgnoreCase);

                int added = 0, dupes = 0;
                lv.BeginUpdate();
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if ((line.StartsWith("\"") && line.EndsWith("\"")) || (line.StartsWith("'") && line.EndsWith("'"))) line = line[1..^1];
                    var norm = NormalizePath(line);
                    if (existing.Contains(norm)) { dupes++; continue; }
                    AddRow(norm);
                    existing.Add(norm);
                    added++;
                }
                lv.EndUpdate();

                lblStatus.Text = $"Loaded {added} new, skipped {dupes} duplicates from: {ofd.FileName}";
                if (chkAutoValidate.Checked) ValidatePaths();
                _ = RecalcCountersAsync();
            };

            btnValidateNow.Click += (_, __) => { ValidatePaths(); };
            btnSaveTxt.Click += (_, __) => SaveTxt();

            lv.ItemChecked += (_, __) => { _ = RecalcCountersAsync(); };
            lv.Resize      += (_, __) => AutoSizeColumns();
            page.Enter     += (_, __) => AutoSizeColumns();

            lblStatus.Text = "Choose a folder or open a .txt file to begin.";
            SetCounters(0, 0);

            return page;
        }

        // Drive helpers
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

        // Job wrapper
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

        // Post-run prompt
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
            var gcBase = $@"{destDrive}:\\";
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
    var root = $@"{destDrive}:\";
    var destMenu = System.IO.Path.Combine(root, "menu");
    var destEmu  = System.IO.Path.Combine(destMenu, "Emulators");

    // Pre-check: required emulator files
    string[] required = { "Press-F.z64", "smsPlus64.z64", "sodium64.z64", "neon64bu.rom" };
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

    if (ExistsAll(destEmu))
    {
        Info("Emulators already installed.");
        return;
    }

    if (MessageBox.Show(this,
            $"Emulator files not found in:\n{destEmu}\n\nDownload and install now?",
            "Summercart64 – Emulators",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
        return;

    // Prepare work dir
    var work = System.IO.Path.Combine(TempRoot, "sc64_emulators");
    EnsureRemoved(work);
    System.IO.Directory.CreateDirectory(work);

    // Download all archives/binaries
    var d1 = System.IO.Path.Combine(work, "Press-F.z64");
    var d2 = System.IO.Path.Combine(work, "smsPlus64.z64");
    var d3 = System.IO.Path.Combine(work, "sodium64.zip");
    var d4 = System.IO.Path.Combine(work, "neon64bu.rom");
    var d5 = System.IO.Path.Combine(work, "misc.bin"); // Google Drive link

    Info("Downloading emulators...");
    await DownloadFileAsync(EMU_PressF_Url, d1, token);
    await DownloadFileAsync(EMU_SMS_Url,    d2, token);
    await DownloadFileAsync(EMU_Sodium_Url, d3, token);
    await DownloadFileAsync(EMU_Neon_Url,   d4, token);
    await DownloadFileAsync(EMU_Misc_Url,   d5, token);

    // Extract ZIPs
    var extractRoot = System.IO.Path.Combine(work, "extracted");
    System.IO.Directory.CreateDirectory(extractRoot);
    if (System.IO.File.Exists(d3)) ExpandZip(d3, System.IO.Path.Combine(extractRoot, "sodium64"));

    // Ensure destination exists
    System.IO.Directory.CreateDirectory(destEmu);

    // Helper to try copy a named file from work/extracted to dest
    async Task<bool> TryCopy(string fileName)
    {
        // direct hits in work
        var direct = System.IO.Path.Combine(work, fileName);
        if (System.IO.File.Exists(direct))
        {
            await RoboCopyFile(direct, destEmu, token);
            return true;
        }
        // search in extracted
        var found = System.IO.Directory.EnumerateFiles(extractRoot, fileName, System.IO.SearchOption.AllDirectories).FirstOrDefault();
        if (found != null)
        {
            await RoboCopyFile(found, destEmu, token);
            return true;
        }
        // search in work recursively
        found = System.IO.Directory.EnumerateFiles(work, fileName, System.IO.SearchOption.AllDirectories).FirstOrDefault();
        if (found != null)
        {
            await RoboCopyFile(found, destEmu, token);
            return true;
        }
        return false;
    }

    // Copy required files
    bool ok1 = await TryCopy("Press-F.z64");
    bool ok2 = await TryCopy("smsPlus64.z64");
    bool ok3 = await TryCopy("sodium64.z64");
    bool ok4 = await TryCopy("neon64bu.rom");

    if (!(ok1 && ok2 && ok3 && ok4))
    {
        Info("Some emulator files were not found after download. Please check sources.");
    }
    else
    {
        Info($"Emulators installed → {destEmu}");
    }

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
        sealed class ComboItem { public string Text { get; } public string FullPath { get; } public ComboItem(string text, string fullPath) { Text = text; FullPath = fullPath; } public override string ToString() => Text; }
    }
}