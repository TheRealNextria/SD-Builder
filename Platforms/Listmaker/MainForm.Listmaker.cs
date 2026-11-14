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
            try { SetLmStatus("Choose a folder or open a .txt file to begin."); } catch { /* ignore */ }
        }

        // Settings (defaults)
        string _lmFolder = string.Empty;

        // Persist .txt imports across searches
        private readonly HashSet<string> _lmTxtItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _lmChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        // Async Listmaker size scans
        CancellationTokenSource? _lmScanCts;

        // Listmaker UI performance
        System.Windows.Forms.Timer? _lmDebounce;
        System.Windows.Forms.Timer? _lmFilterDebounce;
        bool _lmBulkChange;

        void WireListmakerTab()
        {
            // Autoresize columns on show/resize
            lvList.View = View.Details;
            lvList.FullRowSelect = true;
            lvList.HideSelection = false;
            if (lvList.Columns.Count == 0)
            {
                lvList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvList.Columns.Add("Size", 100, HorizontalAlignment.Left);
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
                    SetLmStatus("Loading...");
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
            rbDirs.CheckedChanged += (_, __) => { if (rbDirs.Checked && (txtFilter.Text.Trim() == "*.*" || txtFilter.Text.Trim().Length == 0)) txtFilter.Text = "*"; RefreshListmakerList(); }; rbMixed.CheckedChanged += (_, __) => RefreshListmakerList();


            btnCheckAll.Click += (_, __) =>
            {
                _lmBulkChange = true;
                try
                {
                    lvList.BeginUpdate();
                    for (int i = 0; i < lvList.Items.Count; i++)
                    {
                        var it = lvList.Items[i];
                        it.Checked = true;
                    }
                }
                finally
                {
                    lvList.EndUpdate();
                    _lmBulkChange = false;
                }
                try
                {
                    foreach (ListViewItem it in lvList.Items)
                    {
                        var p = it.Text;
                        if (!string.IsNullOrWhiteSpace(p)) _lmChecked.Add(p);
                    }
                }
                catch { }
                _lmDebounce?.Stop(); _lmDebounce?.Start();
            };
            try { foreach (ListViewItem it in lvList.Items) { var p = it.Text; if (!string.IsNullOrWhiteSpace(p) && it.Checked) _lmChecked.Add(p); } } catch { }
            ;
            btnUncheckAll.Click += (_, __) =>
            {
                _lmBulkChange = true;
                try
                {
                    lvList.BeginUpdate();
                    for (int i = 0; i < lvList.Items.Count; i++)
                    {
                        var it = lvList.Items[i];
                        it.Checked = false;
                    }
                }
                finally
                {
                    lvList.EndUpdate();
                    _lmBulkChange = false;
                }
                try
                {
                    foreach (ListViewItem it in lvList.Items)
                    {
                        var p = it.Text;
                        if (!string.IsNullOrWhiteSpace(p)) _lmChecked.Remove(p);
                    }
                }
                catch { }
                _lmDebounce?.Stop(); _lmDebounce?.Start();
            };
            try { foreach (ListViewItem it in lvList.Items) { var p = it.Text; if (!string.IsNullOrWhiteSpace(p)) _lmChecked.Remove(p); } } catch { }
            ;
            btnDeleteSelected.Click += (_, __) =>
            {
                if (lvList.SelectedItems.Count == 0) { MessageBox.Show(this, "No items selected to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                foreach (ListViewItem it in lvList.SelectedItems.Cast<ListViewItem>().ToList()) lvList.Items.Remove(it);
                SetLmStatus("Deleted selected items.");
                StartListmakerAutoScan();
            };

            btnOpenTxt.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Open text file with paths",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    InitialDirectory = Directory.Exists(GameListsDir) ? GameListsDir : ScriptDir


                };
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

                SetLmStatus($"Loaded {added} new, skipped {dupes} duplicates from: {ofd.FileName}");
                if (chkAutoValidate.Checked) ValidatePaths();
                StartListmakerAutoScan();
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
            SetLmStatus("Choose a folder or open a .txt file to begin.");
            SetCounters(0, 0);
        }

        void SetLmStatus(object? textObj)
        {
            try
            {
                string text = textObj?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    lblStatusLM.Text = text;
            }
            catch (Exception ex)
            {
                Warn("Exception suppressed: " + ex.Message);
            }
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
                bool exists = File.Exists(pth) || Directory.Exists(pth);
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
                            if (File.Exists(p)) { sz = SafeFileLength(p); }
                            else if (Directory.Exists(p)) { sz = SafeDirLength(p, token, recursive: chkRecursive.Checked); }
                            // post result
                            if (!IsDisposed && lvList.IsHandleCreated)
                            {
                                try
                                {
                                    lvList.BeginInvoke(new Action(() =>
                                    {
                                        if (itemRef.ListView == null) return;
                                        itemRef.Tag = sz;
                                        var existsNow = File.Exists(p) || Directory.Exists(p);
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

        private async Task RecalcListmakerAllAsync()
        {
            try
            {
                // we don't want to interfere with the user's checkmarks;
                // just compute sizes/status for ALL rows currently listed.
                var token = _lmScanCts?.Token ?? System.Threading.CancellationToken.None;
                var rows = lvList.Items.Cast<ListViewItem>().ToList();

                // prime
                foreach (var it in rows)
                {
                    var pth = it.Text;
                    bool exists = System.IO.File.Exists(pth) || System.IO.Directory.Exists(pth);
                    it.Tag = null;
                    it.SubItems[1].Text = exists ? "scanning…" : "Missing";
                }

                // throttle parallelism
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
                                var p = itemRef.Text;
                                long sz = 0;
                                if (System.IO.File.Exists(p)) sz = SafeFileLength(p);
                                else if (System.IO.Directory.Exists(p)) sz = SafeDirLength(p, token, recursive: chkRecursive.Checked);

                                if (!IsDisposed && lvList.IsHandleCreated)
                                {
                                    try
                                    {
                                        lvList.BeginInvoke(new Action(() =>
                                        {
                                            if (itemRef.ListView == null) return;
                                            itemRef.Tag = sz;
                                            var existsNow = System.IO.File.Exists(p) || System.IO.Directory.Exists(p);
                                            itemRef.SubItems[1].Text = existsNow ? FormatSize(sz) : "Missing";
                                            UpdateLmTotalsFromItems();
                                        }));
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                            finally
                            {
                                sem.Release();
                            }
                        }, token);
                        tasks.Add(task);
                    }
                    try { await Task.WhenAll(tasks); } catch { }
                }
            }
            catch { /* ignore */ }

            // Final sweep for any lingering 'scanning…' statuses
            if (!IsDisposed && lvList.IsHandleCreated)
            {
                try
                {
                    lvList.BeginInvoke(new Action(() =>
                    {
                        foreach (ListViewItem it in lvList.Items)
                        {
                            if (it.SubItems.Count < 2) continue;
                            if (it.SubItems[1].Text.Contains("scanning"))
                            {
                                var p = it.Text;
                                long sz = 0;
                                bool exists = System.IO.File.Exists(p) || System.IO.Directory.Exists(p);
                                if (exists)
                                {
                                    if (System.IO.File.Exists(p)) sz = SafeFileLength(p);
                                    else sz = SafeDirLength(p, System.Threading.CancellationToken.None, recursive: chkRecursive.Checked);
                                    it.Tag = sz;
                                    it.SubItems[1].Text = FormatSize(sz);
                                }
                                else
                                {
                                    it.SubItems[1].Text = "Missing";
                                }
                            }
                        }
                        UpdateLmTotalsFromItems();
                    }));
                }
                catch { }
            }

            try { BeginInvoke(new Action(() => SetLmStatus("Ready."))); } catch { }
        }


        void ValidatePaths()
        {
            int ok = 0, missing = 0;
            lvList.BeginUpdate();
            foreach (ListViewItem it in lvList.Items)
            {
                var p = it.Text;
                bool exists = File.Exists(p) || Directory.Exists(p);
                it.SubItems[1].Text = exists ? "OK" : "Missing";
                if (exists) ok++; else missing++;
            }
            lvList.EndUpdate();
            _lmBulkChange = false;
            SetLmStatus($"Validated: {ok} OK, {missing} missing.");
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
            SetLmStatus($"Saved {lines.Count} items to: {sfd.FileName}");
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

            SetLmStatus($"Loaded {added} new, skipped {dupes} duplicates from: {path}");
            if (chkAutoValidate.Checked) ValidatePaths();
            StartListmakerAutoScan();
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
                StartListmakerAutoScan();
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

                    if (rbMixed.Checked)
                    {
                        // Mixed: top-level files + folders only
                        var root = _lmFolder;
                        IEnumerable<string> files = Enumerable.Empty<string>();
                        IEnumerable<string> dirs = Enumerable.Empty<string>();
                        try { files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly); } catch { }
                        try { dirs = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly); } catch { }
                        folderItems = files.Concat(dirs);
                    }
                    else if (rbFiles.Checked)
                    {
                        folderItems = Directory.EnumerateFiles(_lmFolder, "*", opt);
                    }
                    else
                    {
                        folderItems = Directory.EnumerateDirectories(_lmFolder, "*", opt);
                    }

                }
                else
                {
                    SetLmStatus("No folder selected.");
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
                try
                {
                    _lmBulkChange = true;
                    foreach (ListViewItem it in lvList.Items)
                    {
                        var p = it.Text;
                        it.Checked = !string.IsNullOrWhiteSpace(p) && _lmChecked.Contains(p);
                    }
                }
                finally { _lmBulkChange = false; }

                SetLmStatus($"Showing {final.Count()} items");

                StartListmakerAutoScan();
            }
            finally
            {
                lvList.EndUpdate();
                _lmBulkChange = false;
            }

            StartListmakerAutoScan();
        }

        private void StartListmakerAutoScan()
        {
            SetLmStatus("Scanning...");

            try
            {
                _lmScanCts?.Cancel();
                _lmScanCts?.Dispose();
            }
            catch { }

            _lmScanCts = new System.Threading.CancellationTokenSource();

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await RecalcListmakerAllAsync().ConfigureAwait(false);
                }
                catch (System.OperationCanceledException) { }
                catch { }
            });
        }
    }
}
