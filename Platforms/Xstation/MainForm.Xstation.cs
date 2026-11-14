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
        string _xsFolder = string.Empty;
        bool _xsBulkChange = false;
        HashSet<string> _xsChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _xsFilterDebounce;
        CancellationTokenSource? _xsScanCts;

        // ---- URLs
        const string X_FW_Url = "https://github.com/x-station/xstation-releases/releases/download/2.0.2/update202.zip";

        // Xstation temp
        string X_FW_Zip => Path.Combine(TempRoot, "xstation_fw.zip");
        string X_FW_Extract => Path.Combine(TempRoot, "extracted");

        // ------------------- Xstation helpers -------------------
        void SetXsStatus(string text)
        {
            try { lblStatusXS.Text = text; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }


        void WireXstationListmaker()
        {
            // columns
            // ===== Xstation UI wiring START =====
            lvXsList.View = View.Details;
            lvXsList.FullRowSelect = true;
            lvXsList.HideSelection = false;
            if (lvXsList.Columns.Count < 2)
            {
                lvXsList.Columns.Clear();
                lvXsList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvXsList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
            }
            // ===== END Xstation UI wiring =====

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

            lvXsList.ItemChecked += (_, __) =>
            {
                if (_xsBulkChange) return;
                try
                {
                    if (lvXsList.FocusedItem is ListViewItem it && it != null)
                    {
                        var path = it.Text;
                        if (it.Checked) _xsChecked.Add(path); else _xsChecked.Remove(path);
                    }
                }
                catch { /* ignore */ }
                UpdateXsCounters();
            };

            btnXsCheckAll.Click += (_, __) =>
            {
                _xsBulkChange = true;
                foreach (ListViewItem it in lvXsList.Items) { it.Checked = true; _xsChecked.Add(it.Text); }
                _xsBulkChange = false;
                UpdateXsCounters();
            };
            btnXsUncheckAll.Click += (_, __) =>
            {
                _xsBulkChange = true;
                foreach (ListViewItem it in lvXsList.Items) { it.Checked = false; _xsChecked.Remove(it.Text); }
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
                var xsFilterText = (txtXsFilter?.Text ?? "*").Trim();
                var xsPatterns = xsFilterText.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => s.Trim())
                                             .ToArray();
                if (xsPatterns.Length == 0) xsPatterns = new[] { "*" };
                xsPatterns = xsPatterns.Select(p => (p.Contains('*') || p.Contains('?')) ? p : ($"*{p}*")).ToArray();
                lvXsList.BeginUpdate();
                lvXsList.Items.Clear();
                if (!Directory.Exists(_xsFolder)) { SetXsStatus("Ready."); return; }

                SetXsStatus("Scanning...");
                // ensure columns
                if (lvXsList.Columns.Count < 2)
                {
                    lvXsList.Columns.Clear();
                    lvXsList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvXsList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbXsFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_xsFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(f);
                        if (!MatchesAnyPattern(name, xsPatterns) && !MatchesAnyPattern(f, xsPatterns)) continue;
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _sarooChecked.Contains(f);
                        it.Checked = _scChecked.Contains(f);
                        it.Checked = _gcChecked.Contains(f);
                        it.Checked = _srChecked.Contains(f);
                        it.Checked = _xsChecked.Contains(f);
                        lvXsList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_xsFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(d);
                        if (!MatchesAnyPattern(name, xsPatterns) && !MatchesAnyPattern(d, xsPatterns)) continue;
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _scChecked.Contains(d);
                        it.Checked = _gcChecked.Contains(d);
                        it.Checked = _srChecked.Contains(d);
                        it.Checked = _xsChecked.Contains(d);
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





        // ===== END Helpers =====
        // ===== Xstation Copy routine START =====
        async Task CopyXstationCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\\";
            Directory.CreateDirectory(destRoot);
            int copied = 0;

            if (rbXsFiles.Checked)
            {
                static List<string> ParseCueSet(string cuePath)
                {
                    var result = new List<string>();
                    try
                    {
                        result.Add(cuePath);
                        var dir = Path.GetDirectoryName(cuePath) ?? "";
                        foreach (var line in System.IO.File.ReadAllLines(cuePath))
                        {
                            var t = line.Trim();
                            if (t.Length == 0) continue;
                            if (t.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                            {
                                int firstQuote = t.IndexOf('\"');
                                int lastQuote = (firstQuote >= 0) ? t.IndexOf('\"', firstQuote + 1) : -1;
                                string? rel = null;
                                if (firstQuote >= 0 && lastQuote > firstQuote)
                                    rel = t.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                                else
                                {
                                    var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 2) rel = parts[1];
                                }
                                if (!string.IsNullOrWhiteSpace(rel))
                                {
                                    var full = Path.GetFullPath(Path.Combine(dir, rel));
                                    if (File.Exists(full) && !result.Contains(full, StringComparer.OrdinalIgnoreCase))
                                        result.Add(full);
                                }
                            }
                        }
                    }
                    catch { }
                    return result;
                }

                var selected = new List<string>();
                foreach (ListViewItem it in lvXsList.Items)
                {
                    if (!it.Checked) continue;
                    var p = it.Text;
                    if (File.Exists(p) || Directory.Exists(p)) selected.Add(p);
                    else Info($"[SKIP] Not found: \"{p}\"");
                }
                if (selected.Count == 0) { Warn("No items checked."); return; }

                var files = selected.Where(p => File.Exists(p)).ToList();
                var dirs = selected.Where(p => Directory.Exists(p)).ToList();

                var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var f in files)
                {
                    var ext = Path.GetExtension(f);
                    if (ext.Equals(".cue", StringComparison.OrdinalIgnoreCase))
                    {
                        var set = ParseCueSet(f);
                        var cueBase = Path.GetFileNameWithoutExtension(f);
                        if (!groups.TryGetValue(cueBase, out var lst)) groups[cueBase] = lst = new List<string>();
                        foreach (var mbr in set) if (!lst.Contains(mbr, StringComparer.OrdinalIgnoreCase)) lst.Add(mbr);
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(f) ?? "";
                        var cues = Directory.GetFiles(dir, "*.cue");
                        string? cueToUse = null;

                        if (cues.Length == 1) cueToUse = cues[0];
                        else if (cues.Length > 1)
                        {
                            var parentName = string.IsNullOrEmpty(dir) ? "" : new DirectoryInfo(dir).Name;
                            cueToUse = cues.FirstOrDefault(c => string.Equals(Path.GetFileNameWithoutExtension(c), parentName, StringComparison.OrdinalIgnoreCase));
                            if (cueToUse == null)
                            {
                                var fname = Path.GetFileName(f);
                                foreach (var c in cues)
                                {
                                    try
                                    {
                                        var lines = File.ReadAllLines(c);
                                        if (lines.Any(l => l.IndexOf(fname, StringComparison.OrdinalIgnoreCase) >= 0))
                                        { cueToUse = c; break; }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(cueToUse) && File.Exists(cueToUse))
                        {
                            var set = ParseCueSet(cueToUse);
                            var cueBase = Path.GetFileNameWithoutExtension(cueToUse);
                            if (!groups.TryGetValue(cueBase, out var lst)) groups[cueBase] = lst = new List<string>();
                            foreach (var mbr in set) if (!lst.Contains(mbr, StringComparer.OrdinalIgnoreCase)) lst.Add(mbr);
                        }
                        else
                        {
                            var parent = string.IsNullOrEmpty(dir) ? "" : new DirectoryInfo(dir).Name;
                            var key = string.IsNullOrWhiteSpace(parent) ? Path.GetFileNameWithoutExtension(f) : parent;
                            if (!groups.TryGetValue(key, out var lst)) groups[key] = lst = new List<string>();
                            if (!lst.Contains(f, StringComparer.OrdinalIgnoreCase)) lst.Add(f);
                        }
                    }
                }

                foreach (var kv in groups.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {

                    // Compute a friendly destination label (e.g., "Root" or "Saroo\ISO")
                    string destLabel;
                    {
                        var rootPrefix = $@"{destDrive}:\\";
                        var labeled = destRoot;
                        if (!labeled.EndsWith(@"\")) labeled += @"\";
                        var rel = labeled.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                            ? labeled.Substring(rootPrefix.Length)
                            : labeled;
                        destLabel = string.IsNullOrEmpty(rel) ? "Root" : rel.TrimEnd('\\');
                    }
                    Info($"Copying Game Files \u2192 '{kv.Key}' \u2192 {destLabel}");

                    var groupName = kv.Key;
                    var members = kv.Value;

                    var dest = Path.Combine(destRoot, groupName);
                    bool needsPrompt = Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any();
                    if (needsPrompt)
                    {
                        var d = ConfirmReplaceCountdown(dest, OverwriteTimeout, $"Xstation folder '{groupName}'", folderPromptOnlyWhenNonEmpty: true, token: token);
                        if (d == ReplaceDecision.No) { Info($"[SKIP] Folder '{groupName}'"); continue; }
                        if (d == ReplaceDecision.Cancel) throw new OperationCanceledException();
                    }
                    Directory.CreateDirectory(dest);

                    foreach (var mbr in members)
                    {
                        if (!File.Exists(mbr)) { Info($"[SKIP] Missing: '{mbr}'"); continue; }
                        await RoboCopyFile(mbr, dest, token);
                        copied++;
                    }

                    WaitEnterOrTimeout(CopyTimeout, token);
                }

                foreach (var d in dirs)
                {
                    await CopyEntry(d, Platform.Xstation, destRoot, token);
                    copied++;
                    WaitEnterOrTimeout(CopyTimeout, token);
                }
            }
            else
            {
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
        }        // ===== END Xstation Copy routine =====
    }
}
