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
        // The newest update*.zip is looked up on GitHub; X_FW_Url is only used when that fails.
        const string X_FW_Url = "https://github.com/x-station/xstation-releases/releases/download/2.0.2/update202.zip";
        const string X_Owner = "x-station";
        const string X_Repo = "xstation-releases";
        const string X_AssetPattern = @"^update\d+\.zip$";

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
            var latest = await GetLatestAssetUrlAsync(X_Owner, X_Repo, X_AssetPattern, token);
            var chosen = latest ?? X_FW_Url;
            Info(latest != null ? $"Resolved Xstation URL: {chosen}" : $"Using fallback Xstation URL: {chosen}");
            await DownloadFileAsync(chosen, X_FW_Zip, token);
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
        Task CopyXstationCheckedAsync(string destDrive, CancellationToken token) =>
            CopyCueGroupsCheckedAsync(destDrive, $@"{destDrive}:\", "Xstation", Platform.Xstation, lvXsList, rbXsFiles.Checked, token); // Common\MainForm.CueGroupCopy.cs
    }
}
