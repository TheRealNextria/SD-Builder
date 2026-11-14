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
        HashSet<string> _sarooChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Saroo state
        string _srFolder = string.Empty;
        bool _srBulkChange = false;
        HashSet<string> _srChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _srFilterDebounce;
        CancellationTokenSource? _srScanCts;
        const string S_Url = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/Saroo.latest.zip";

        // Saroo temp
        string S_Work => Path.Combine(TempRoot, "saroo");
        string S_Zip => Path.Combine(S_Work, "saroo_fw.zip");
        string S_Extract => Path.Combine(S_Work, "extracted");


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
                lvSrList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
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

            // Saroo search debounce + wiring
            _srFilterDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _srFilterDebounce.Tick += (_, __) => { _srFilterDebounce!.Stop(); RefreshSarooList(); };
            try
            {
                if (txtSarooFilter != null)
                {
                    txtSarooFilter.TextChanged += (_, __) => { _srFilterDebounce?.Stop(); _srFilterDebounce?.Start(); };
                    txtSarooFilter.KeyDown += (s, e) =>
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                            _srFilterDebounce?.Stop();
                            RefreshSarooList();
                        }
                    };
                }
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }

            // Saroo clear button
            try
            {
                if (btnSarooClearFilter != null && txtSarooFilter != null)
                {
                    btnSarooClearFilter.Click += (_, __) =>
                    {
                        txtSarooFilter.Text = string.Empty;
                        _srFilterDebounce?.Stop();
                        RefreshSarooList();
                    };
                }
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }


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

            lvSrList.ItemChecked += (s, e) =>
            {
                if (_srBulkChange) return;
                try
                {
                    var it = e?.Item as ListViewItem;
                    if (it != null)
                    {
                        var path = it.Text;
                        if (it.Checked) _srChecked.Add(path); else _srChecked.Remove(path);
                    }
                }
                catch { /* ignore */ }
                UpdateSrCounters();
            };

            btnSrCheckAll.Click += (_, __) =>
            {
                _srBulkChange = true;
                foreach (ListViewItem it in lvSrList.Items) { it.Checked = true; _srChecked.Add(it.Text); }
                _srBulkChange = false;
                UpdateSrCounters();
            };
            btnSrUncheckAll.Click += (_, __) =>
            {
                _srBulkChange = true;
                foreach (ListViewItem it in lvSrList.Items) { it.Checked = false; _srChecked.Remove(it.Text); }
                _srBulkChange = false;
                UpdateSrCounters();
            };
            btnSrStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopySarooCheckedAsync(d, token));
            };
        }


        void RefreshSarooList()
        {
            try
            {
                var srFilterText = (txtSarooFilter?.Text ?? "*").Trim();
                var srPatterns = srFilterText.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .ToArray();
                if (srPatterns.Length == 0) srPatterns = new[] { "*" };
                srPatterns = srPatterns.Select(p => (p.Contains('*') || p.Contains('?')) ? p : ($"*{p}*")).ToArray();
                lvSrList.BeginUpdate();
                _srBulkChange = true;
                lvSrList.Items.Clear();
                if (!Directory.Exists(_srFolder)) { SetSrStatus("Ready."); return; }

                SetSrStatus("Scanning...");
                if (lvSrList.Columns.Count < 2)
                {
                    lvSrList.Columns.Clear();
                    lvSrList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvSrList.Columns.Add("Size", XsStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbSrFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_srFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(f);
                        if (!MatchesAnyPattern(name, srPatterns) && !MatchesAnyPattern(f, srPatterns)) continue;
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _sarooChecked.Contains(f);
                        it.Checked = _scChecked.Contains(f);
                        it.Checked = _gcChecked.Contains(f);
                        it.Checked = _srChecked.Contains(f);
                        lvSrList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_srFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(d);
                        if (!MatchesAnyPattern(name, srPatterns) && !MatchesAnyPattern(d, srPatterns)) continue;
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _scChecked.Contains(d);
                        it.Checked = _gcChecked.Contains(d);
                        it.Checked = _srChecked.Contains(d);
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
            finally
            {
                _srBulkChange = false;
                lvSrList.EndUpdate();
            }
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

        // ===== Saroo Copy routine START =====
        async Task CopySarooCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\Saroo\ISO\";
            Directory.CreateDirectory(destRoot);
            int copied = 0;

            if (rbSrFiles.Checked)
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
                foreach (ListViewItem it in lvSrList.Items)
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
                        var d = ConfirmReplaceCountdown(dest, OverwriteTimeout, $"Saroo folder '{groupName}'", folderPromptOnlyWhenNonEmpty: true, token: token);
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
                    await CopyEntry(d, Platform.Saroo, destRoot, token);
                    copied++;
                    WaitEnterOrTimeout(CopyTimeout, token);
                }
            }
            else
            {
                foreach (ListViewItem it in lvSrList.Items)
                {
                    if (!it.Checked) continue;
                    var path = it.Text;
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        Info($"[SKIP] Not found: \"{path}\"");
                        continue;
                    }
                    await CopyEntry(path, Platform.Saroo, destRoot, token);
                    copied++;
                }
                if (copied == 0) Warn("No items checked.");
            }
        }        // ===== END Saroo Copy routine =====

        async Task EnsureSarooFirmware(string destDrive, CancellationToken token)
        {
            var sarooBase = $@"{destDrive}:\SAROO";
            var expected = new[] { "cover.bin", "mcuapp.bin", "saroocfg.txt", "ssfirm.bin" }
                .Select(f => Path.Combine(sarooBase, f))
                .ToArray();

            var mainFirmwarePresent = expected.All(File.Exists);
            var dstUpdate = Path.Combine(sarooBase, "update");
            var dstSsmaster = Path.Combine(dstUpdate, "SSMaster.rbf");

            if (mainFirmwarePresent && File.Exists(dstSsmaster))
            {
                Info("SAROO firmware present.");
                return;
            }

            if (mainFirmwarePresent)
            {
                if (MessageBox.Show(this,
                    $"SAROO firmware is present in:\n{sarooBase}\n\nOptional update file is missing:\nupdate\\SSMaster.rbf\n\nDownload now to repair/copy the update folder?",
                    "SAROO",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.No) return;
            }
            else
            {
                if (MessageBox.Show(this, $"SAROO firmware missing in:\n{sarooBase}\n\nDownload now?", "SAROO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;
            }

            EnsureRemoved(S_Work); Directory.CreateDirectory(S_Work);
            await DownloadFileAsync(S_Url, S_Zip, token);
            ExpandZip(S_Zip, S_Extract);

            string? FindFileIgnoreCase(string folder, string fileName)
            {
                try
                {
                    return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                }
                catch { return null; }
            }

            string? FindChildDirectoryIgnoreCase(string folder, string dirName)
            {
                try
                {
                    return Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(d => string.Equals(Path.GetFileName(d), dirName, StringComparison.OrdinalIgnoreCase));
                }
                catch { return null; }
            }

            bool ContainsMainSarooFiles(string folder)
            {
                return FindFileIgnoreCase(folder, "cover.bin") != null
                    && FindFileIgnoreCase(folder, "mcuapp.bin") != null
                    && FindFileIgnoreCase(folder, "saroocfg.txt") != null
                    && FindFileIgnoreCase(folder, "ssfirm.bin") != null;
            }

            void PatchSarooConfigLanguage()
            {
                var cfg = Path.Combine(sarooBase, "saroocfg.txt");
                if (!File.Exists(cfg)) return;

                try
                {
                    var bytes = File.ReadAllBytes(cfg);
                    var pat = Encoding.ASCII.GetBytes("lang_id = 0");
                    int idx = IndexOf(bytes, pat);
                    if (idx >= 0)
                    {
                        bytes[idx + pat.Length - 1] = 0x31;
                        File.WriteAllBytes(cfg, bytes);
                        Info("saroocfg.txt patched (lang_id = 1) without re-encoding.");
                    }
                    else Info("'lang_id = 0' not found; no change made.");
                }
                catch (Exception ex) { Warn("Could not patch saroocfg.txt: " + ex.Message); }
            }

            // Locate the SAROO firmware folder. Supports firm/SAROO, firm/saroo, or nested archive layouts.
            var srcSaroo = Directory.GetDirectories(S_Extract, "*", SearchOption.AllDirectories)
                .FirstOrDefault(d =>
                    string.Equals(Path.GetFileName(d), "SAROO", StringComparison.OrdinalIgnoreCase) &&
                    ContainsMainSarooFiles(d));

            if (srcSaroo == null)
                throw new Exception("Missing SAROO firmware folder in downloaded ZIP.");

            var srcUpdate = FindChildDirectoryIgnoreCase(srcSaroo, "update");
            var srcSsmaster = srcUpdate == null ? null : FindFileIgnoreCase(srcUpdate, "SSMaster.rbf");

            Directory.CreateDirectory(sarooBase);

            if (mainFirmwarePresent)
            {
                if (srcUpdate != null && srcSsmaster != null && !File.Exists(dstSsmaster))
                {
                    Directory.CreateDirectory(dstUpdate);
                    Info("SAROO firmware present, copying missing update folder...");
                    token.ThrowIfCancellationRequested();
                    await RoboCopyDir(srcUpdate, dstUpdate, token);

                    if (File.Exists(dstSsmaster)) Info("SAROO update folder installed.");
                    else Warn("SAROO update folder was copied, but SSMaster.rbf is still missing.");
                }
                else if (srcSsmaster == null)
                {
                    Info("SAROO firmware present. No update\\SSMaster.rbf found in downloaded ZIP.");
                }
                else
                {
                    Info("SAROO firmware present.");
                }

                EnsureRemoved(S_Work);
                return;
            }

            var decision = ConfirmReplaceCountdown(sarooBase, OverwriteTimeout, "SAROO folder", folderPromptOnlyWhenNonEmpty: true, token: token);
            if (decision == ReplaceDecision.No) { Info("[SKIP] SAROO folder"); EnsureRemoved(S_Work); return; }
            if (decision == ReplaceDecision.Cancel) throw new OperationCanceledException();

            Info("Copying SAROO firmware...");
            token.ThrowIfCancellationRequested();
            await RoboCopyDir(srcSaroo, sarooBase, token);

            if (expected.All(File.Exists))
            {
                Info($"SAROO firmware installed/updated → {sarooBase}");
                PatchSarooConfigLanguage();
            }
            else Warn("Some SAROO files still missing.");

            EnsureRemoved(S_Work);
        }
    }
}
