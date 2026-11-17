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
        // Dreamcast state
        string _dcFolder = string.Empty;
        bool _dcBulkChange = false;
        HashSet<string> _dcChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _dcFilterDebounce;
        CancellationTokenSource? _dcScanCts;




        // ------------------- Dreamcast helpers -------------------
        void SetDcStatus(string text)
        {
            lblStatusDC.Text = text;
        }

        void WireDreamcastListmaker()
        {
            // columns
            // ===== Dreamcast UI wiring START =====
            lvDcList.View = View.Details;
            lvDcList.FullRowSelect = true;
            lvDcList.HideSelection = false;
            if (lvDcList.Columns.Count < 2)
            {
                lvDcList.Columns.Clear();
                lvDcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvDcList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
            }


            // Guard: block checking of rows marked as Installed (uses DcMeta on ListViewItem.Tag)
            lvDcList.ItemCheck += (_, e) =>
            {
                if (_dcBulkChange) return; // allow programmatic changes
                var it = lvDcList.Items[e.Index];
                if (it?.Tag is DcMeta m && m.Installed)
                {
                    e.NewValue = CheckState.Unchecked;
                }
            };
            // ===== END Dreamcast UI wiring =====

            void AutoSizeDc()
            {
                if (lvDcList.Columns.Count >= 2)
                {
                    int statusWidth = XsStatusColWidth;
                    lvDcList.Columns[0].Width = Math.Max(120, lvDcList.ClientSize.Width - statusWidth - 8);
                    lvDcList.Columns[1].Width = statusWidth;
                }
            }
            lvDcList.Resize += (_, __) => AutoSizeDc();
            tabDreamcast.Enter += (_, __) => AutoSizeDc();

            btnDcChoose.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _dcFolder = dlg.SelectedPath;
                    SetDcStatus($"Selected: {_dcFolder}");
                    RefreshDreamcastList();
                }
            };
            rbDcFiles.CheckedChanged += (_, __) => { if (rbDcFiles.Checked) { SetDcStatus("Mode: Files"); RefreshDreamcastList(); } };
            rbDcDirs.CheckedChanged += (_, __) => { if (rbDcDirs.Checked) { SetDcStatus("Mode: Directories"); RefreshDreamcastList(); } };

            lvDcList.ItemChecked += (_, __) =>
            {
                if (_dcBulkChange) return;
                if (lvDcList.FocusedItem is ListViewItem it && it != null)
                {
                    var path = it.Text;
                    if (it.Checked) _dcChecked.Add(path); else _dcChecked.Remove(path);
                }
                UpdateDcCounters();
            };

            btnDcCheckAll.Click += (_, __) =>
            {
                _dcBulkChange = true;

                foreach (ListViewItem it in lvDcList.Items)
                {
                    if (!DcIsInstalled(it))
                    {
                        it.Checked = true;
                        _dcChecked.Add(it.Text);
                    }
                    else
                    {
                        it.Checked = false;
                        _dcChecked.Remove(it.Text);
                    }
                }

                _dcBulkChange = false;
                UpdateDcCounters();
            };
            btnDcUncheckAll.Click += (_, __) =>
            {
                _dcBulkChange = true;
                foreach (ListViewItem it in lvDcList.Items) { it.Checked = false; _dcChecked.Remove(it.Text); }
                _dcBulkChange = false;
                UpdateDcCounters();
            };
            btnDcStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopyDreamcastCheckedAsync(d, token));
                RefreshDreamcastList(); // also after a failed or cancelled job: show what is on the card now
            };
        }


        void UpdateDcCounters()
        {
            long total = 0;
            int count = 0;
            foreach (ListViewItem it in lvDcList.Items)
            {
                if (!it.Checked) continue;
                count++;

                if (it.Tag is DcMeta md) total += md.Size;
                else if (it.Tag is long sz) total += sz; // legacy fallback
            }
            lblCountDC.Text = $"Checked: {count}";
            lblSizeDC.Text = $"Size: {FormatSize(total)}";

        }


        void RefreshDreamcastList() => _ = RefreshDreamcastListAsync();

        /// <summary>Lists the folder on a background thread, fills the list, then marks installed games and sizes.</summary>
        async Task RefreshDreamcastListAsync()
        {
            var ct = RestartScan(ref _dcScanCts); // a newer refresh cancels this one
            var patterns = BuildFilterPatterns(txtDcFilter?.Text);
            try
            {
                SetDcStatus("Scanning...");
                var paths = await ListFolderAsync(_dcFolder, rbDcFiles.Checked, patterns, ct);
                if (ct.IsCancellationRequested) return;
                FillScanList(lvDcList, paths ?? new List<string>(), _dcChecked, XsStatusColWidth, b => _dcBulkChange = b);
                UpdateDcCounters();
                if (paths == null) { SetDcStatus("Ready."); return; }

                if (await RecalcDcAsync(ct)) SetDcStatus($"Ready. {lvDcList.Items.Count} items.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Error("Dreamcast list error: " + ex.Message); }
        }

        /// <summary>
        /// For every row, on a background thread: is the game already on the selected card (serial + disc),
        /// and how big is it. Installed rows are greyed out and unchecked. False when cancelled.
        /// </summary>
        async Task<bool> RecalcDcAsync(CancellationToken ct)
        {
            var dcDrive = CurrentDriveLetter(); // UI state: read it here, not on the background thread
            var rows = lvDcList.Items.Cast<ListViewItem>().Select(it => (Item: it, Path: it.Text)).ToList();
            try
            {
                await Task.Run(() =>
                {
                    // Reading the serials of all games on the card is many small files: keep it off the UI thread too.
                    var cardDiscs = dcDrive != null
                        ? IndexDcSerialDiscs($@"{dcDrive}:\")
                        : new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    var dirSerialCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                    foreach (var (item, path) in rows)
                    {
                        ct.ThrowIfCancellationRequested();
                        bool installed = cardDiscs.Count > 0 && IsDcSourceInstalled(path, cardDiscs, dirSerialCache);
                        var size = PathSize(path, ct);
                        PostToUi(() => { if (!ct.IsCancellationRequested && item.ListView == lvDcList) ShowDcRow(item, installed, size); });
                    }
                }, ct);
            }
            catch (OperationCanceledException) { return false; }
            return !ct.IsCancellationRequested;
        }

        /// <summary>
        /// True when the game at <paramref name="path"/> is on the card. The serial comes from the image itself,
        /// or else from a .gdi in the same folder (cached per folder). Runs on a background thread.
        /// </summary>
        static bool IsDcSourceInstalled(string path, Dictionary<string, HashSet<string>> cardDiscs, Dictionary<string, string?> dirSerialCache)
        {
            try
            {
                string? serial = null;
                if (TryExtractDcSerialFromSource(path, out var srcSerial) && !string.IsNullOrWhiteSpace(srcSerial))
                {
                    serial = srcSerial;
                }
                else
                {
                    var dir = Path.GetDirectoryName(path) ?? string.Empty;
                    if (dir.Length > 0)
                    {
                        if (!dirSerialCache.TryGetValue(dir, out serial))
                        {
                            var gdi = Directory.EnumerateFiles(dir, "*.gdi").FirstOrDefault();
                            if (!string.IsNullOrEmpty(gdi) && TryExtractDcSerialFromSource(gdi, out var gdiSerial) && !string.IsNullOrWhiteSpace(gdiSerial))
                                serial = gdiSerial;
                            dirSerialCache[dir] = serial;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(serial) || !cardDiscs.TryGetValue(serial, out var discs)) return false;

                // Disc-aware: disc 2 of a game is not "installed" because disc 1 is on the card.
                if (TryGuessDiscKey(path, path, out var dk))
                    return discs.Contains(dk) || discs.Any(v => v.StartsWith(dk.Split('/')[0] + "/", StringComparison.OrdinalIgnoreCase));
                // Disc number unknown: only count it as installed for a single-disc game.
                return discs.Contains("1/1");
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// <summary>UI thread: show the result of RecalcDcAsync for one row.</summary>
        void ShowDcRow(ListViewItem it, bool installed, long? size)
        {
            if (it.SubItems.Count < 2) it.SubItems.Add("");
            if (installed)
            {
                it.SubItems[1].Text = "Installed";
                it.ForeColor = SystemColors.GrayText;
                it.Font = new Font(it.Font, FontStyle.Italic);
                it.Tag = new DcMeta { Installed = true, Size = size ?? 0 };
                if (it.Checked)
                {
                    _dcBulkChange = true; // programmatic uncheck: skip the ItemCheck guard and the ItemChecked handler
                    try { it.Checked = false; } finally { _dcBulkChange = false; }
                }
                _dcChecked.Remove(it.Text);
                UpdateDcCounters();
            }
            else
            {
                it.SubItems[1].Text = size.HasValue ? FormatSize(size.Value) : "error";
                it.Tag = new DcMeta { Installed = false, Size = size ?? 0 };
                if (it.Checked) UpdateDcCounters();
            }
        }




        static string PadDcIndex(int n)
        {
            if (n < 100) return n.ToString("00");
            if (n < 1000) return n.ToString("000");
            return n.ToString("0000");
        }

        static string NextDreamcastIndexFolder(string root)
        {
            var used = new HashSet<int>();
            // No try/catch on purpose: if the card cannot be read we must not guess a "free" folder
            // (that could overwrite a game). The exception stops the copy job and is logged.
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (int.TryParse(name, out int num) && num >= 1 && num <= 9999)
                    used.Add(num);
            }

            for (int i = 2; i <= 9999; i++)
                if (!used.Contains(i))
                    return Path.Combine(root, PadDcIndex(i));

            throw new InvalidOperationException("No free Dreamcast index folder (01..9999).");
        }

        // ===== Dreamcast Copy routine START =====
        async Task CopyDreamcastCheckedAsync(string destDrive, CancellationToken token)
        {
            var destRoot = $@"{destDrive}:\";
            Directory.CreateDirectory(destRoot);

            // Build sources: group by parent folder in Files mode so one game = one index
            var sources = new System.Collections.Generic.List<string>();
            if (rbDcFiles.Checked)
            {
                var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (ListViewItem it in lvDcList.Items)
                {
                    token.ThrowIfCancellationRequested();
                    if (!it.Checked) continue;
                    var pth = it.Text;
                    string dir = System.IO.Directory.Exists(pth) ? pth : (System.IO.Path.GetDirectoryName(pth) ?? pth);
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    if (seen.Add(dir)) sources.Add(dir);
                }
            }
            else
            {
                foreach (ListViewItem it in lvDcList.Items)
                {
                    token.ThrowIfCancellationRequested();
                    if (!it.Checked) continue;
                    sources.Add(it.Text);
                }
            }

            if (!await ConfirmFreeSpaceAsync(destDrive, sources, token)) return;

            var dcIndex = await ReadDcCardIndexAsync(destRoot, token);
            foreach (var path in sources)
            {
                token.ThrowIfCancellationRequested();
                if (await IsDcGameAlreadyOnCardAsync(path, dcIndex, token)) continue;
                await CopyDreamcastGameAsync(path, destRoot, token);
            }

            Info("Dreamcast copy complete."); // btnDcStart refreshes the list after the job

        }        // ===== END Dreamcast Copy routine =====



        // Dreamcast item metadata for guard logic (avoids relying on column headers)
        private sealed class DcMeta
        {
            public bool Installed;
            public long Size;
        }


        // Helper: true if ListViewItem.Tag is DcMeta with Installed=true
        private static bool DcIsInstalled(ListViewItem it)
        {
            return it?.Tag is DcMeta m && m.Installed;
        }
    }
}
