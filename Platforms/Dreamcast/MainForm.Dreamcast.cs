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
            try { lblStatusDC.Text = text; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
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
                try
                {
                    if (_dcBulkChange) return; // allow programmatic changes
                    var it = lvDcList.Items[e.Index];
                    if (it?.Tag is DcMeta m && m.Installed)
                    {
                        e.NewValue = CheckState.Unchecked;
                    }
                }
                catch { /* ignore */ }
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
                try
                {
                    if (lvDcList.FocusedItem is ListViewItem it && it != null)
                    {
                        var path = it.Text;
                        if (it.Checked) _dcChecked.Add(path); else _dcChecked.Remove(path);
                    }
                }
                catch { /* ignore */ }
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
                try { BeginInvoke(new Action(() => RefreshDreamcastList())); } catch { }
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
            try
            {
                lblCountDC.Text = $"Checked: {count}";
                lblSizeDC.Text = $"Size: {FormatSize(total)}";
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }

        }


        async Task RecalcDcAsync()
        {
            try
            {
                _dcScanCts?.Cancel();
                _dcScanCts = new CancellationTokenSource();

                // Build index of installed Dreamcast serials on the selected drive (if any)
                var dcDrive = CurrentDriveLetter();

                // Cache to derive serials for non-.gdi files by inspecting sibling .gdi in the same folder
                var dcDirSerialCache = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

                var dcExistingSerials = (dcDrive != null)
                    ? IndexDcSerials($@"{dcDrive}:")
                    : new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

                var dcExistingSerialDiscs = (dcDrive != null)
                    ? IndexDcSerialDiscs($@"{dcDrive}:")
                    : new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);




                var ct = _dcScanCts.Token;

                await Task.Run(() =>
                {
                    foreach (ListViewItem it in lvDcList.Items)
                    {
                        if (ct.IsCancellationRequested) break;
                        var path = it.Text;


                        bool isInstalled = false;
                        try
                        {
                            // 1) Try direct serial extraction from this path (works well for .gdi)
                            if (TryExtractDcSerialFromSource(path, out var srcSerial) && !string.IsNullOrWhiteSpace(srcSerial))
                            {
                                if (dcExistingSerialDiscs.TryGetValue(srcSerial, out var discsSet))
                                {
                                    if (TryGuessDiscKey(it.Text, path, out var dk))
                                    {
                                        isInstalled = discsSet.Contains(dk) || System.Linq.Enumerable.Any(discsSet, v => v.StartsWith(dk.Split('/')[0] + "/", System.StringComparison.OrdinalIgnoreCase));
                                    }
                                    else
                                    {
                                        // If we cannot determine disc number, assume NOT installed unless it's clearly single-disc only.
                                        isInstalled = discsSet.Contains("1/1");
                                    }
                                }
                                else
                                {
                                    isInstalled = false;
                                }
                            }
                            else
                            {
                                // 2) Fallback: look for a sibling .gdi in the same folder and use that serial
                                var dir = Path.GetDirectoryName(path) ?? string.Empty;
                                if (!string.IsNullOrEmpty(dir))
                                {
                                    if (!dcDirSerialCache.TryGetValue(dir, out var dirSerial) || string.IsNullOrWhiteSpace(dirSerial))
                                    {
                                        try
                                        {
                                            var gdi = System.Linq.Enumerable.FirstOrDefault(Directory.EnumerateFiles(dir, "*.gdi"));
                                            if (!string.IsNullOrEmpty(gdi) && TryExtractDcSerialFromSource(gdi, out var gdiSerial) && !string.IsNullOrWhiteSpace(gdiSerial))
                                            {
                                                dirSerial = gdiSerial;
                                            }
                                            dcDirSerialCache[dir] = dirSerial;
                                        }
                                        catch
                                        {
                                            // ignore directory access errors
                                        }
                                    }
                                    if (!string.IsNullOrWhiteSpace(dirSerial))
                                    {
                                        // Disc-aware check
                                        if (dcExistingSerialDiscs.TryGetValue(dirSerial, out var discsSet))
                                        {
                                            if (TryGuessDiscKey(it.Text, path, out var dk))
                                            {
                                                isInstalled = discsSet.Contains(dk) || System.Linq.Enumerable.Any(discsSet, v => v.StartsWith(dk.Split('/')[0] + "/", System.StringComparison.OrdinalIgnoreCase));
                                            }
                                            else
                                            {
                                                // If we cannot determine disc number, assume NOT installed unless it's clearly single-disc only.
                                                isInstalled = discsSet.Contains("1/1");
                                            }
                                        }
                                        else
                                        {
                                            isInstalled = false;
                                        }

                                    }
                                }
                            }
                        }
                        catch { /* ignore */ }


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
                                if (isInstalled)
                                {
                                    if (it.SubItems.Count < 2) it.SubItems.Add("Installed");
                                    else it.SubItems[1].Text = "Installed";
                                    try
                                    {
                                        it.ForeColor = SystemColors.GrayText;
                                        it.Font = new Font(it.Font, FontStyle.Italic);
                                    }
                                    catch { /* ignore styling issues */ }
                                    it.Tag = new DcMeta { Installed = true, Size = size }; it.Checked = false;
                                    _dcChecked.Remove(it.Text);
                                }
                                else
                                {
                                    if (it.SubItems.Count < 2) it.SubItems.Add(sizeStr);
                                    else it.SubItems[1].Text = sizeStr;
                                    it.Tag = new DcMeta { Installed = false, Size = size };
                                    if (it.Checked) UpdateDcCounters();
                                }
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

                BeginInvoke(new Action(() => SetDcStatus($"Ready. {lvDcList.Items.Count} items.")));
            }
            catch { /* cancelled */ }
        }

        void RefreshDreamcastList()
        {
            try
            {
                var dcFilterText = (txtDcFilter?.Text ?? "*").Trim();
                var dcPatterns = dcFilterText.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => s.Trim())
                                             .ToArray();
                if (dcPatterns.Length == 0) dcPatterns = new[] { "*" };
                dcPatterns = dcPatterns.Select(p => (p.Contains('*') || p.Contains('?')) ? p : ($"*{p}*")).ToArray();
                lvDcList.BeginUpdate();
                lvDcList.Items.Clear();
                if (!Directory.Exists(_dcFolder)) { SetDcStatus("Ready."); lvDcList.EndUpdate(); return; }

                SetDcStatus("Scanning...");
                if (lvDcList.Columns.Count < 2)
                {
                    lvDcList.Columns.Clear();
                    lvDcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvDcList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbDcFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_dcFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(f);
                        if (!MatchesAnyPattern(name, dcPatterns) && !MatchesAnyPattern(f, dcPatterns)) continue;
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _dcChecked.Contains(f);
                        lvDcList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_dcFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(d);
                        if (!MatchesAnyPattern(name, dcPatterns) && !MatchesAnyPattern(d, dcPatterns)) continue;
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _dcChecked.Contains(d);
                        lvDcList.Items.Add(it);
                    }
                }
                UpdateDcCounters();
                _ = RecalcDcAsync();
            }
            catch (Exception ex)
            {
                Error("Dreamcast list error: " + ex.Message);
            }
            finally { try { lvDcList.EndUpdate(); } catch { } }
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
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var name = Path.GetFileName(dir);
                    if (int.TryParse(name, out int num) && num >= 1 && num <= 9999)
                        used.Add(num);
                }
            }
            catch { /* ignore */ }

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

            var dcIndex = DcCardIndex.Read(destRoot);
            foreach (var path in sources)
            {
                token.ThrowIfCancellationRequested();
                if (IsDcGameAlreadyOnCard(path, dcIndex)) continue;
                await CopyDreamcastGameAsync(path, destRoot, token);
            }

            Info("Dreamcast copy complete.");

            try
            {
                BeginInvoke(new Action(() => RefreshDreamcastList()));
            }
            catch { /* ignore refresh errors */ }

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
            try { return it?.Tag is DcMeta m && m.Installed; }
            catch { return false; }
        }
    }
}
