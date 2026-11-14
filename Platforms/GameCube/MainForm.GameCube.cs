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
        const int GcStatusColWidth = 160;

        // Gamecube state
        string _gcFolder = string.Empty;
        bool _gcBulkChange = false;
        HashSet<string> _gcChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        System.Windows.Forms.Timer? _gcFilterDebounce;

        CancellationTokenSource? _gcScanCts;
        const string G_Url = "https://github.com/emukidid/swiss-gc/releases/download/v0.6r1913/swiss_r1913.7z";
        const string GC_CHEATS_Url = "https://github.com/TheRealNextria/SD-Builder/releases/download/1.0/GC.Swiss.cheats.zip";
        const string G_Owner = "emukidid";
        const string G_Repo = "swiss-gc";
        const string G_AssetPattern = @"^swiss_r\d+\.(?:7z|zip)$";
        const string G_7zr_Url = "https://www.7-zip.org/a/7zr.exe";

        // Swiss temp
        string G_Work => Path.Combine(TempRoot, "gamecube");
        string G_7z => Path.Combine(G_Work, "swiss.7z");
        string G_Extract => Path.Combine(G_Work, "extracted");
        string G_7zr => Path.Combine(G_Work, "7zr.exe");

        // -------- Gamecube Listmaker (mirrors Xstation) --------
        void WireGamecubeListmaker()
        {
            // columns
            // ===== GameCube UI wiring START =====
            lvGcList.View = View.Details;
            lvGcList.FullRowSelect = true;
            lvGcList.HideSelection = false;
            if (lvGcList.Columns.Count < 2)
            {
                lvGcList.Columns.Clear();
                lvGcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                lvGcList.Columns.Add("Size", GcStatusColWidth, HorizontalAlignment.Left);
            }
            // ===== END GameCube UI wiring =====

            void AutoSizeGc()
            {
                if (lvGcList.Columns.Count >= 2)
                {
                    int statusWidth = GcStatusColWidth;
                    lvGcList.Columns[0].Width = Math.Max(120, lvGcList.ClientSize.Width - statusWidth - 8);
                    lvGcList.Columns[1].Width = statusWidth;
                }
            }
            lvGcList.Resize += (_, __) => AutoSizeGc();
            tabGamecube.Enter += (_, __) => AutoSizeGc();

            btnGcChoose.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog { ShowNewFolderButton = false };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _gcFolder = dlg.SelectedPath;
                    SetGcStatus($"Selected: {_gcFolder}");
                    RefreshGamecubeList();
                }
            };
            rbGcFiles.CheckedChanged += (_, __) => { if (rbGcFiles.Checked) { SetGcStatus("Mode: Files"); RefreshGamecubeList(); } };
            rbGcDirs.CheckedChanged += (_, __) => { if (rbGcDirs.Checked) { SetGcStatus("Mode: Directories"); RefreshGamecubeList(); } };

            lvGcList.ItemChecked += (s, e) =>
            {
                if (_gcBulkChange) return;
                try
                {
                    var it = e?.Item as ListViewItem;
                    if (it != null)
                    {
                        var path = it.Text;
                        if (it.Checked) _gcChecked.Add(path); else _gcChecked.Remove(path);
                    }
                }
                catch { /* ignore */ }
                UpdateGcCounters();
            };

            btnGcCheckAll.Click += (_, __) =>
            {
                _gcBulkChange = true;
                foreach (ListViewItem it in lvGcList.Items) { it.Checked = true; _gcChecked.Add(it.Text); }
                _gcBulkChange = false;
                UpdateGcCounters();
            };
            btnGcUncheckAll.Click += (_, __) =>
            {
                _gcBulkChange = true;
                foreach (ListViewItem it in lvGcList.Items) { it.Checked = false; _gcChecked.Remove(it.Text); }
                _gcBulkChange = false;
                UpdateGcCounters();
            };
            btnGcStart.Click += async (_, __) =>
            {
                await WithDriveJobAsync((d, token) => CopyGamecubeCheckedAsync(d, token));
            };
        }



        void SetGcStatus(string text)
        {
            try { lblStatusGC.Text = text; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        void UpdateGcCounters()
        {
            long total = 0;
            int count = 0;
            foreach (ListViewItem it in lvGcList.Items)
            {
                if (!it.Checked) continue;
                count++;
                if (it.Tag is long sz) total += sz;
            }
            try
            {
                lblCountGC.Text = $"Checked: {count}";
                lblSizeGC.Text = $"Size: {FormatSize(total)}";
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        void RefreshGamecubeList()
        {
            try
            {
                var gcFilterText = (txtGcFilter?.Text ?? "*").Trim();
                var gcPatterns = gcFilterText.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .ToArray();
                if (gcPatterns.Length == 0) gcPatterns = new[] { "*" };
                gcPatterns = gcPatterns.Select(p => (p.Contains('*') || p.Contains('?')) ? p : ($"*{p}*")).ToArray();
                lvGcList.BeginUpdate();
                _gcBulkChange = true;
                lvGcList.Items.Clear();
                if (!Directory.Exists(_gcFolder)) { SetGcStatus("Ready."); return; }

                SetGcStatus("Scanning...");
                // ensure columns
                if (lvGcList.Columns.Count < 2)
                {
                    lvGcList.Columns.Clear();
                    lvGcList.Columns.Add("Path", -2, HorizontalAlignment.Left);
                    lvGcList.Columns.Add("Size", GcStatusColWidth, HorizontalAlignment.Left);
                }

                if (rbGcFiles.Checked)
                {
                    foreach (var f in Directory.EnumerateFiles(_gcFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(f);
                        if (!MatchesAnyPattern(name, gcPatterns) && !MatchesAnyPattern(f, gcPatterns)) continue;
                        var it = new ListViewItem(f);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _sarooChecked.Contains(f);
                        it.Checked = _scChecked.Contains(f);
                        it.Checked = _gcChecked.Contains(f);
                        lvGcList.Items.Add(it);
                    }
                }
                else
                {
                    foreach (var d in Directory.EnumerateDirectories(_gcFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var name = System.IO.Path.GetFileName(d);
                        if (!MatchesAnyPattern(name, gcPatterns) && !MatchesAnyPattern(d, gcPatterns)) continue;
                        var it = new ListViewItem(d);
                        it.SubItems.Add("scanning...");
                        it.Tag = (long?)null;
                        it.Checked = _scChecked.Contains(d);
                        it.Checked = _gcChecked.Contains(d);
                        lvGcList.Items.Add(it);
                    }
                }

                UpdateGcCounters();
                _ = RecalcGcAsync();
            }
            catch (Exception ex)
            {
                Error("Error refreshing list: " + ex.Message);
            }
            finally
            {
                _gcBulkChange = false;
                lvGcList.EndUpdate();
            }
        }

        async Task RecalcGcAsync()
        {
            try
            {
                _gcScanCts?.Cancel();
                _gcScanCts = new CancellationTokenSource();
                var ct = _gcScanCts.Token;

                await Task.Run(() =>
                {
                    foreach (ListViewItem it in lvGcList.Items)
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
                                if (it.Checked) UpdateGcCounters();
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

                BeginInvoke(new Action(() => SetGcStatus($"Ready. {lvGcList.Items.Count} items.")));
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        // ===== GameCube Copy routine START =====
        async Task CopyGamecubeCheckedAsync(string destDrive, CancellationToken token)
        {
            string destRoot = $@"{destDrive}:\Games";
            Directory.CreateDirectory(destRoot);
            int copied = 0;

            foreach (ListViewItem it in lvGcList.Items)
            {
                if (!it.Checked) continue;
                var path = it.Text;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Info($"[SKIP] Not found: \"{path}\"");
                    continue;
                }
                await CopyEntry(path, Platform.Gamecube, destRoot, token);
                copied++;
            }

            if (copied == 0) Warn("No items checked.");
        }        // ===== END GameCube Copy routine =====


        async Task InstallGamecubeCheats(string destDrive, CancellationToken token)
        {
            var gcBase = $@"{destDrive}:\Swiss";
            Directory.CreateDirectory(gcBase);
            var cheatsDest = Path.Combine(gcBase, "Cheats");
            Directory.CreateDirectory(cheatsDest);

            EnsureRemoved(G_Work); Directory.CreateDirectory(G_Work);
            var zipPath = Path.Combine(G_Work, "GC.Swiss.cheats.zip");
            Info($"Downloading: {GC_CHEATS_Url}");
            await DownloadFileAsync(GC_CHEATS_Url, zipPath, token);

            var extractDir = Path.Combine(G_Work, "cheats_extracted");
            EnsureRemoved(extractDir); Directory.CreateDirectory(extractDir);
            Info($"Extracting Cheats → {extractDir}");
            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, true);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to extract cheats zip.", ex);
            }

            string? srcCheats = null;
            foreach (var dir in Directory.EnumerateDirectories(extractDir, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(dir), "cheats", StringComparison.OrdinalIgnoreCase))
                {
                    srcCheats = dir; break;
                }
            }
            if (srcCheats == null)
            {
                var maybe = Path.Combine(extractDir, "cheats");
                if (Directory.Exists(maybe)) srcCheats = maybe;
            }
            if (srcCheats == null) throw new Exception("Could not find 'cheats' folder inside the archive.");

            Info($"Copying Cheats → {cheatsDest}");
            await RoboCopyDir(srcCheats, cheatsDest, token);
            Info($"Cheat files installed → {cheatsDest}");
        }
        async Task EnsureGamecubeIPL(string destDrive, CancellationToken token)
        {
            var gcBase = $@"{destDrive}:\";
            Directory.CreateDirectory(gcBase);
            var ipl = Path.Combine(gcBase, "IPL.dol");
            if (File.Exists(ipl)) { Info("Swiss firmware present."); return; }
            if (MessageBox.Show(this, $"IPL.dol not found in:\n{gcBase}\n\nDownload Swiss now?", "Gamecube", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;

            EnsureRemoved(G_Work); Directory.CreateDirectory(G_Work);
            var latest = await GetLatestAssetUrlAsync(G_Owner, G_Repo, G_AssetPattern);
            var chosen = latest ?? G_Url;
            Info($"Resolved Swiss URL: {chosen}");

            // --- Swiss firmware update check (reads hidden marker if present)
            int ParseR(string s)
            {
                var m = Regex.Match(s ?? string.Empty, @"r(\d+)", RegexOptions.IgnoreCase);
                return m.Success ? int.Parse(m.Groups[1].Value) : -1;
            }
            string markerPath = Path.Combine(gcBase, ".swiss.install.json");
            int latestR = ParseR(chosen);
            int installedR = -1;
            if (File.Exists(markerPath))
            {
                try
                {
                    var json = File.ReadAllText(markerPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("version", out var v))
                    {
                        installedR = ParseR(v.GetString() ?? string.Empty);
                    }
                }
                catch { /* ignore parse errors */ }
            }
            if (installedR >= 0 && latestR >= 0)
            {
                if (installedR >= latestR) Info($"Swiss: up-to-date (r{installedR}).");
                else Info($"Swiss: update available r{installedR} → r{latestR}.");
            }
            else if (latestR >= 0)
            {
                Info($"Swiss: latest is r{latestR}. No installed version detected.");
            }
            await DownloadFileAsync(chosen, G_7z, token);
            await Ensure7zr(token);
            Directory.CreateDirectory(G_Extract);
            Info($"Extracting 7z → {G_Extract}");
            var rc = await RunProcess(G_7zr, $"x -y -o\"{G_Extract}\" \"{G_7z}\"", token);
            if (rc != 0) throw new Exception($"7zr extraction failed (rc={rc})");

            foreach (var dir in Directory.EnumerateDirectories(G_Extract, "Legacy", SearchOption.AllDirectories))
                try { Directory.Delete(dir, true); Info($"Removed Legacy directory: {dir}"); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }

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
            if (Directory.Exists(legacyOnSd)) try { Directory.Delete(legacyOnSd, true); Info("Removed Legacy directory from SD root."); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            Info($"IPL.dol installed → {gcBase}");

            // --- Write hidden marker with install metadata
            try
            {
                using var sha = SHA256.Create();
                using var fs = File.OpenRead(ipl);
                var hash = sha.ComputeHash(fs);
                var shaHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                var verR = Regex.Match(chosen ?? string.Empty, @"r(\d+)", RegexOptions.IgnoreCase);
                var verStr = verR.Success ? $"r{verR.Groups[1].Value}" : "unknown";

                var payload = new
                {
                    version = verStr,
                    url = chosen,
                    installedUtc = DateTime.UtcNow.ToString("o"),
                    sha256 = shaHex
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(Path.Combine(gcBase, ".swiss.install.json"), json);
                try
                {
                    var mp = Path.Combine(gcBase, ".swiss.install.json");
                    if (File.Exists(mp))
                    {
                        var attrs = File.GetAttributes(mp);
                        File.SetAttributes(mp, attrs | FileAttributes.Hidden);
                    }
                }
                catch { /* best-effort hide */ }
                Info("Swiss marker written (.swiss.install.json).");
            }
            catch (Exception ex)
            {
                Info($"Warning: could not write Swiss marker: {ex.Message}");
            }
            EnsureRemoved(G_Work);
        }
        async Task Ensure7zr(CancellationToken token)
        {
            if (!File.Exists(G_7zr)) { Info($"Downloading 7zr.exe → {G_7zr}"); await DownloadFileAsync(G_7zr_Url, G_7zr, token); }
        }

        private void btnGcCheckAll_Click(object sender, EventArgs e)
        {

        }
    }
}
