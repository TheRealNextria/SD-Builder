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
        CancellationTokenSource? _lmScanCts;  // size scan
        CancellationTokenSource? _lmListCts;  // building the list

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
                StartListmakerAutoScan(reportValidation: chkAutoValidate.Checked);
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

        // (Un)checking a row only changes the totals: every row already gets its size from the list scan
        // (RecalcListmakerAllAsync). Called by the _lmDebounce timer in MainForm.cs.
        Task RecalcCountersAsync()
        {
            UpdateLmTotalsFromItems();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Works out the size of every row on a background thread (a few rows at a time) and shows it on the
        /// UI thread. With <paramref name="reportValidation"/> the status line ends with the OK / missing count.
        /// </summary>
        private async Task RecalcListmakerAllAsync(CancellationToken token, bool reportValidation)
        {
            // UI thread: read the rows and the option once; the background part only gets plain values.
            bool recursive = chkRecursive.Checked;
            var rows = lvList.Items.Cast<ListViewItem>().Select(it => (Item: it, Path: it.Text)).ToList();
            foreach (var (it, _) in rows)
            {
                it.Tag = null;
                if (it.SubItems.Count > 1) it.SubItems[1].Text = "scanning…";
            }
            UpdateLmTotalsFromItems();

            int missing = 0;
            try
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount > 4 ? 4 : 2, CancellationToken = token };
                await Task.Run(() => Parallel.ForEach(rows, options, row =>
                {
                    long? size = null;
                    try
                    {
                        if (File.Exists(row.Path)) size = SafeFileLength(row.Path);
                        else if (Directory.Exists(row.Path)) size = SafeDirLength(row.Path, token, recursive);
                    }
                    catch (OperationCanceledException) { return; }
                    if (size == null) Interlocked.Increment(ref missing);

                    PostToUi(() =>
                    {
                        if (token.IsCancellationRequested || row.Item.ListView != lvList) return;
                        row.Item.Tag = size ?? 0;
                        if (row.Item.SubItems.Count > 1) row.Item.SubItems[1].Text = size.HasValue ? FormatSize(size.Value) : "Missing";
                        UpdateLmTotalsFromItems();
                    });
                }), token);
            }
            catch (OperationCanceledException) { return; } // a newer scan took over
            if (token.IsCancellationRequested) return;

            if (reportValidation) SetLmStatus($"Validated: {rows.Count - missing} OK, {missing} missing.");
            else SetLmStatus(missing == 0 ? "Ready." : $"Ready. {missing} missing.");
        }


        void ValidatePaths() => _ = ValidatePathsAsync();

        /// <summary>Marks every row OK / Missing; the existence checks run on a background thread.</summary>
        async Task ValidatePathsAsync()
        {
            var rows = lvList.Items.Cast<ListViewItem>().Select(it => (Item: it, Path: it.Text)).ToList();
            SetLmStatus("Validating...");
            bool[] exists;
            try { exists = await Task.Run(() => rows.Select(r => File.Exists(r.Path) || Directory.Exists(r.Path)).ToArray()); }
            catch (Exception ex) { Error("Validate failed: " + ex.Message); return; }

            int ok = 0, missing = 0;
            lvList.BeginUpdate();
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (exists[i]) ok++; else missing++;
                    var it = rows[i].Item;
                    if (it.ListView == lvList && it.SubItems.Count > 1) it.SubItems[1].Text = exists[i] ? "OK" : "Missing";
                }
            }
            finally { lvList.EndUpdate(); }
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
            StartListmakerAutoScan(reportValidation: chkAutoValidate.Checked);
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


        void RefreshListmakerList() => _ = RefreshListmakerListAsync();

        /// <summary>
        /// Folder entries (plus the rows loaded from .txt files), filtered. The folder is read on a background
        /// thread; a newer refresh cancels this one.
        /// </summary>
        async Task RefreshListmakerListAsync()
        {
            var ct = RestartScan(ref _lmListCts);
            // UI thread: read the options once.
            var folder = _lmFolder;
            bool mixed = rbMixed.Checked, files = rbFiles.Checked, recursive = chkRecursive.Checked;
            var filter = (txtFilter.Text ?? string.Empty).Trim();
            var txtItems = _lmTxtItems.ToList();
            if (!string.IsNullOrWhiteSpace(folder)) SetLmStatus("Loading...");

            List<string> final;
            try
            {
                final = await Task.Run(() => BuildListmakerItems(folder, mixed, files, recursive, filter, txtItems, ct), ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { Error("Game list: could not read folder: " + ex.Message); SetLmStatus("Could not read folder."); return; }
            if (ct.IsCancellationRequested) return;

            lvList.BeginUpdate();
            _lmBulkChange = true; // AddRowLM restores the check marks from _lmChecked
            try
            {
                lvList.Items.Clear();
                foreach (var path in final) AddRowLM(path);
            }
            finally
            {
                _lmBulkChange = false;
                lvList.EndUpdate();
            }

            SetLmStatus($"Showing {final.Count} items");
            StartListmakerAutoScan();
        }

        /// <summary>Background thread: the rows for the list (no UI access).</summary>
        static List<string> BuildListmakerItems(string folder, bool mixed, bool files, bool recursive, string filter,
                                                List<string> txtItems, CancellationToken ct)
        {
            var combined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                IEnumerable<string> folderItems;
                if (mixed)
                {
                    // Mixed: top-level files + folders only
                    var found = new List<string>();
                    try { found.AddRange(Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    try { found.AddRange(Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    folderItems = found;
                }
                else if (files) folderItems = Directory.EnumerateFiles(folder, "*", opt);
                else folderItems = Directory.EnumerateDirectories(folder, "*", opt);

                foreach (var p in folderItems)
                {
                    ct.ThrowIfCancellationRequested();
                    combined.Add(p);
                }
            }
            foreach (var p in txtItems) combined.Add(p); // rows loaded from .txt files stay in the list

            // Filter (case-insensitive) on the file name OR the full path: wildcards, or else "contains".
            IEnumerable<string> final = combined;
            if (!string.IsNullOrEmpty(filter) && filter != "*" && filter != "*.*")
            {
                if (filter.IndexOfAny(new[] { '*', '?' }) >= 0)
                {
                    var rx = new Regex("^" + Regex.Escape(filter).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase);
                    final = final.Where(p => rx.IsMatch(p) || rx.IsMatch(Path.GetFileName(p)));
                }
                else
                {
                    final = final.Where(p => p.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                          || Path.GetFileName(p).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
            return final.ToList();
        }

        private void StartListmakerAutoScan(bool reportValidation = false)
        {
            SetLmStatus("Scanning...");
            var token = RestartScan(ref _lmScanCts); // cancels the previous size scan
            _ = RecalcListmakerAllAsync(token, reportValidation);
        }
    }
}
