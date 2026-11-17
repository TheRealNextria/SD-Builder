using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#nullable enable

namespace SDBuilderWin
{
    // Shared list scanning for the platform tabs. The disk (often a network share) is only read on a
    // background thread; the ListView is only touched on the UI thread, so the window keeps responding.
    public sealed partial class MainForm
    {
        /// <summary>Cancels the previous scan of a tab and returns the token for the new one.</summary>
        static CancellationToken RestartScan(ref CancellationTokenSource? cts)
        {
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }
            cts = new CancellationTokenSource();
            return cts.Token;
        }

        /// <summary>Filter box text → wildcard patterns; a pattern without * or ? matches anywhere in the name.</summary>
        static string[] BuildFilterPatterns(string? filterText)
        {
            var patterns = (filterText ?? "*").Trim()
                .Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            if (patterns.Length == 0) patterns = new[] { "*" };
            return patterns.Select(p => (p.Contains('*') || p.Contains('?')) ? p : $"*{p}*").ToArray();
        }

        /// <summary>
        /// Top-level files (or folders) of <paramref name="folder"/> whose name or full path matches a pattern,
        /// read on a background thread. Null when the folder does not exist.
        /// </summary>
        static Task<List<string>?> ListFolderAsync(string folder, bool files, string[] patterns, CancellationToken ct) =>
            Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return (List<string>?)null;
                var entries = files
                    ? Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                    : Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly);
                var result = new List<string>();
                foreach (var p in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (MatchesAnyPattern(Path.GetFileName(p), patterns) || MatchesAnyPattern(p, patterns)) result.Add(p);
                }
                return result;
            }, ct);

        /// <summary>
        /// Replaces the rows of a tab's list with <paramref name="paths"/> (size column "scanning..."),
        /// restoring the check marks from <paramref name="checkedSet"/>. Runs on the UI thread.
        /// </summary>
        static void FillScanList(ListView lv, IEnumerable<string> paths, ISet<string> checkedSet, int sizeColWidth, Action<bool> setBulkChange)
        {
            lv.BeginUpdate();
            setBulkChange(true); // restoring check marks must not count as the user (un)checking
            try
            {
                lv.Items.Clear();
                if (lv.Columns.Count < 2)
                {
                    lv.Columns.Clear();
                    lv.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lv.Columns.Add("Size", sizeColWidth, HorizontalAlignment.Left);
                }
                var rows = paths.Select(p =>
                {
                    var it = new ListViewItem(p) { Tag = null, Checked = checkedSet.Contains(p) };
                    it.SubItems.Add("scanning...");
                    return it;
                }).ToArray();
                lv.Items.AddRange(rows);
            }
            finally
            {
                setBulkChange(false);
                lv.EndUpdate();
            }
        }

        /// <summary>Size of a file, or of everything below a folder. Null when the path is gone or unreadable.</summary>
        static long? PathSize(string path, CancellationToken ct)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return null;
                long size = 0;
                foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try { size += f.Length; }
                    catch (IOException) { } // file vanished while scanning: leave it out
                }
                return size;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>
        /// Works out the size of every row on a background thread and hands each result to
        /// <paramref name="apply"/> on the UI thread (null = missing or unreadable). Rows that were
        /// removed in the meantime, or results of a cancelled scan, are not applied.
        /// Returns false when the scan was cancelled.
        /// </summary>
        async Task<bool> ScanRowSizesAsync(ListView lv, CancellationToken ct, Action<ListViewItem, long?> apply)
        {
            // Read the rows here, on the UI thread; the background thread only gets plain strings.
            var rows = lv.Items.Cast<ListViewItem>().Select(it => (Item: it, Path: it.Text)).ToList();
            try
            {
                await Task.Run(() =>
                {
                    foreach (var (item, path) in rows)
                    {
                        ct.ThrowIfCancellationRequested();
                        var size = PathSize(path, ct);
                        PostToUi(() => { if (!ct.IsCancellationRequested && item.ListView == lv) apply(item, size); });
                    }
                }, ct);
            }
            catch (OperationCanceledException) { return false; }
            return !ct.IsCancellationRequested;
        }

        /// <summary>BeginInvoke that is silently skipped while the window is closing.</summary>
        void PostToUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { } // handle destroyed between the check and the call
        }

        /// <summary>The usual size-column text: a size, or "error" when the path could not be read.</summary>
        void ShowRowSize(ListViewItem it, long? size)
        {
            var text = size.HasValue ? FormatSize(size.Value) : "error";
            if (it.SubItems.Count < 2) it.SubItems.Add(text); else it.SubItems[1].Text = text;
            it.Tag = size ?? 0;
        }
    }
}
