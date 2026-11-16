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

        // Robocopy flags
        readonly string[] RoboFlagsDir = new[] { "/E", "/IS", "/IT", "/COPY:DAT", "/R:1", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/Z", "/FFT" };
        readonly string[] RoboFlagsFile = new[] { "/IS", "/IT", "/COPY:DAT", "/R:1", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/FFT" };


        // Simple wildcard matcher: * and ? (case-insensitive option)
        static bool WildcardIsMatch(string input, string pattern, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            string regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            var opts = ignoreCase ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None;
            return System.Text.RegularExpressions.Regex.IsMatch(input ?? string.Empty, regex, opts);
        }
        static bool MatchesAnyPattern(string pathOrName, string[] patterns)
        {
            if (patterns == null || patterns.Length == 0) return true;
            foreach (var p in patterns)
                if (WildcardIsMatch(pathOrName, p, ignoreCase: true))
                    return true;
            return false;
        }

        static async Task<string?> GetLatestAssetUrlAsync(string owner, string repo, string assetNamePattern, string? token = null)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SD-Builder/1.0");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                // Try /releases/latest first
                var latestUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                using (var latestResp = await http.GetAsync(latestUrl))
                {
                    if (latestResp.IsSuccessStatusCode)
                    {
                        var json = await latestResp.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("assets", out var assets))
                        {
                            foreach (var asset in assets.EnumerateArray())
                            {
                                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                                if (System.Text.RegularExpressions.Regex.IsMatch(name, assetNamePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    return asset.GetProperty("browser_download_url").GetString();
                            }
                        }
                    }
                }

                // Fallback: list recent releases
                var listUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=5";
                using (var listResp = await http.GetAsync(listUrl))
                {
                    if (listResp.IsSuccessStatusCode)
                    {
                        var json = await listResp.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        foreach (var rel in doc.RootElement.EnumerateArray())
                        {
                            if (!rel.TryGetProperty("assets", out var assets)) continue;
                            foreach (var asset in assets.EnumerateArray())
                            {
                                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                                if (System.Text.RegularExpressions.Regex.IsMatch(name, assetNamePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    return asset.GetProperty("browser_download_url").GetString();
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ===== SD cool-down between games =====
        // The pause is taken *before* the next write instead of after each game, so there is
        // no pointless wait after the last game, after skipped games, or twice in a row.
        bool _copyCooldownPending;

        /// <summary>Call after a game has been written: the next write will first wait CopyTimeout seconds.</summary>
        void CopyCooldown(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _copyCooldownPending = true;
            _dcGameFolderInProgress = null; // game complete, keep its folder
        }

        /// <summary>Called by the copy helpers right before they write; waits if a game was just written.</summary>
        void WaitForCopyCooldown(CancellationToken token)
        {
            if (!_copyCooldownPending) return;
            _copyCooldownPending = false;
            if (CopyTimeout <= 0) return;
            Info($"Cool-down: waiting {CopyTimeout}s before the next write…");
            WaitEnterOrTimeout(CopyTimeout, token);
            Info("Cool-down done, continuing.");
        }

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
            btnCancelAll.Click += (_, __) => { try { _cts?.Cancel(); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } };

            token.Register(() => { try { if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); })); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } });

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
            var btnNo = new Button { Text = "No", Left = 220, Top = 80, Width = 90, DialogResult = DialogResult.No };
            var btnCancel = new Button { Text = "Cancel (stop all)", Left = 320, Top = 80, Width = 130, DialogResult = DialogResult.Cancel };

            using var t = new System.Windows.Forms.Timer { Interval = 1000 };
            t.Tick += (_, __) =>
            {
                remaining--;
                if (remaining <= 0) { t.Stop(); dlg.DialogResult = autoNo ? DialogResult.No : DialogResult.Yes; dlg.Close(); }
                else dlg.Text = $"Confirm replace – auto {(autoNo ? "No" : "Yes")} in {remaining}s";
            };
            dlg.Shown += (_, __) => t.Start();
            btnCancel.Click += (_, __) => { try { _cts?.Cancel(); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } };
            if (token.HasValue) token.Value.Register(() => { try { if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); })); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } });
            dlg.Controls.AddRange(new Control[] { lbl, btnYes, btnNo, btnCancel });
            var res = dlg.ShowDialog(this);
            t.Stop();
            if (res == DialogResult.Yes) return ReplaceDecision.Yes;
            if (res == DialogResult.No) return ReplaceDecision.No;
            return ReplaceDecision.Cancel;
        }


        // Helper: enumerate all ListBox controls for startup clear
        IEnumerable<ListBox> FindListBoxes(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is ListBox lb) yield return lb;
                foreach (var nested in FindListBoxes(c)) yield return nested;
            }
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
        // ===== Helpers START =====
        void ExpandZip(string zipPath, string dest)
        {
            if (Directory.Exists(dest)) { try { Directory.Delete(dest, true); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } }
            Directory.CreateDirectory(dest);
            Info($"Extracting ZIP → {dest}");
            ZipFile.ExtractToDirectory(zipPath, dest);
        }
        void EnsureRemoved(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } }
        async Task<int> RunProcess(string exe, string args, CancellationToken token) =>
            (await RunProcessCapture(exe, args, token)).ExitCode;

        async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessCapture(string exe, string args, CancellationToken token)
        {
            var psi = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.Start();
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            try { await p.WaitForExitAsync(token); }
            catch (OperationCanceledException) { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); } throw; }
            return (p.ExitCode, await so, await se);
        }

        // ===== robocopy with result check =====
        // robocopy exit codes are bit flags: 0-7 = success (1 = copied, 2 = extra files, 4 = mismatches),
        // 8 = some files could not be copied, 16 = fatal error (no files copied). Anything >= 8 is a failure.

        /// <summary>A copy to the card failed (card full, card removed, source unreachable, ...).</summary>
        sealed class CopyFailedException : IOException
        {
            public CopyFailedException(string message) : base(message) { }
        }

        // Dreamcast game folder (\NN) created for the game being copied right now; removed again if that copy fails,
        // so a half-copied game never ends up in the menu. Cleared once the game is complete (CopyCooldown).
        string? _dcGameFolderInProgress;

        string CreateDcGameFolder(string destRoot)
        {
            var dir = NextDreamcastIndexFolder(destRoot);
            Directory.CreateDirectory(dir);
            _dcGameFolderInProgress = dir;
            return dir;
        }

        async Task RunRobocopy(string args, string what, string? partialFile, CancellationToken token)
        {
            var (code, stdout, stderr) = await RunProcessCapture("robocopy.exe", args, token);
            if (code < 8) return;

            // With /NJH /NJS /NFL /NDL only problems are printed; keep the ERROR lines and the reason line after each.
            var lines = (stdout + "\n" + stderr).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var details = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Contains("ERROR", StringComparison.OrdinalIgnoreCase)) continue;
                details.Add(lines[i]);
                if (i + 1 < lines.Count && !lines[i + 1].Contains("ERROR", StringComparison.OrdinalIgnoreCase)) details.Add(lines[i + 1]);
            }
            foreach (var d in details.Distinct().Take(6)) Error("  robocopy: " + d);

            if (partialFile != null) { try { if (File.Exists(partialFile)) File.Delete(partialFile); } catch (Exception ex) { Warn("Could not remove partial file: " + ex.Message); } }
            if (_dcGameFolderInProgress != null)
            {
                var dir = _dcGameFolderInProgress;
                _dcGameFolderInProgress = null;
                try { if (Directory.Exists(dir)) { Directory.Delete(dir, true); Warn($"Removed incomplete Dreamcast folder \\{Path.GetFileName(dir)}"); } }
                catch (Exception ex) { Warn($"Could not remove incomplete folder \\{Path.GetFileName(dir)}: " + ex.Message); }
            }

            var reason = (code & 16) != 0 ? "fatal error, nothing was copied" : "some files could not be copied";
            throw new CopyFailedException($"Copy failed for '{what}' (robocopy exit {code}: {reason}). Remaining items were not copied.");
        }

        // ===== Free space check before a copy job =====

        /// <summary>
        /// Adds up the selected sources and compares that with the free space on the card.
        /// Returns false when there is not enough room and the user chooses not to continue.
        /// The total is an upper bound: games that are skipped later (already on the card) are counted too.
        /// </summary>
        async Task<bool> ConfirmFreeSpaceAsync(string destDrive, IEnumerable<string> sources, CancellationToken token)
        {
            var list = sources.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count == 0) return true;

            long free;
            try { free = new DriveInfo(destDrive + @":\").AvailableFreeSpace; }
            catch (Exception ex) { Warn("Could not read free space: " + ex.Message); return true; }

            // Sizes come from the source (often a network share), so read them off the UI thread.
            long need = await Task.Run(() =>
            {
                // Count every file once, even when e.g. both a .cue and its .bin are checked.
                var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list) CollectSourceFiles(s, files, token);
                return files.Values.Sum();
            }, token);
            const long Margin = 16L * 1024 * 1024; // room for name.txt / info files / FAT overhead
            Info($"Selected: {FormatSize(need)} — free on {destDrive}: {FormatSize(free)}");
            if (need + Margin <= free) return true;

            var ans = MessageBox.Show(this,
                $"Not enough free space on {destDrive}:\n\n" +
                $"Selected: {FormatSize(need)}\nFree: {FormatSize(free)}\nShort: {FormatSize(need + Margin - free)}\n\n" +
                "Games that are already on the card are counted too, so it may still fit.\n" +
                "Copy anyway? It stops with an error as soon as the card is full.",
                "Not enough space", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (ans == DialogResult.Yes) return true;
            Warn("Copy not started: not enough free space.");
            return false;
        }

        static List<string> CheckedPaths(ListView lv) =>
            lv.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => i.Text).ToList();

        /// <summary>Paths in a game list .txt, parsed the same way ReadListAndCopy does (comments and quotes stripped).</summary>
        static List<string> ReadListEntries(string listPath)
        {
            var result = new List<string>();
            foreach (var raw in File.ReadAllLines(listPath))
            {
                var line = raw?.Trim() ?? "";
                if (line.Length == 0 || Regex.IsMatch(line, @"^\s*(#|;|//|::|REM\b)")) continue;
                if (line.StartsWith("\"") && line.EndsWith("\"") && line.Length >= 2) line = line[1..^1];
                result.Add(line.TrimEnd(' ', '.'));
            }
            return result;
        }

        static void CollectSourceFiles(string path, Dictionary<string, long> files, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                        files[f.FullName] = f.Length;
                    return;
                }
                if (!File.Exists(path)) return;
                files[Path.GetFullPath(path)] = new FileInfo(path).Length;

                // A .cue is copied together with the tracks it lists.
                if (path.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = Path.GetDirectoryName(path) ?? "";
                    foreach (Match m in Regex.Matches(File.ReadAllText(path), "^\\s*FILE\\s+\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    {
                        var track = Path.GetFullPath(Path.Combine(dir, m.Groups[1].Value));
                        if (File.Exists(track)) files[track] = new FileInfo(track).Length;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        async Task RoboCopyDir(string source, string dest, CancellationToken token)
        {
            WaitForCopyCooldown(token);
            Directory.CreateDirectory(dest);
            var args = $"{Q(source)} {Q(dest)} {string.Join(" ", RoboFlagsDir)}";
            await RunRobocopy(args, Path.GetFileName(source.TrimEnd('\\', '/')), partialFile: null, token);
        }
        async Task RoboCopyFile(string srcFile, string destDir, CancellationToken token)
        {
            WaitForCopyCooldown(token);
            Directory.CreateDirectory(destDir);
            var leaf = Path.GetFileName(srcFile);
            var srcDir = Path.GetDirectoryName(srcFile)!;
            var args = $"{Q(srcDir)} {Q(destDir)} {Q(leaf)} {string.Join(" ", RoboFlagsFile)}";
            // Only a file this copy created may be removed on failure; an existing copy on the card is never deleted.
            var target = Path.Combine(destDir, leaf);
            await RunRobocopy(args, leaf, partialFile: File.Exists(target) ? null : target, token);
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

        string FormatSize(long bytes)
        {
            const long KB = 1024, MB = KB * 1024, GB = MB * 1024, TB = GB * 1024;
            if (bytes >= TB) return $"{bytes / (double)TB:0.##} TB";
            if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
            return $"{bytes} B";
        }
        long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        long SafeDirLength(string root, CancellationToken token, bool recursive)
        {
            long total = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!recursive)
                {
                    foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        token.ThrowIfCancellationRequested();
                        try { total += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                    }
                    return total;
                }

                var stack = new Stack<string>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var dir = stack.Pop();
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(dir))
                        {
                            token.ThrowIfCancellationRequested();
                            try { total += new FileInfo(f).Length; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
                        }
                        foreach (var d in Directory.EnumerateDirectories(dir))
                        {
                            token.ThrowIfCancellationRequested();
                            stack.Push(d);
                        }
                    }
                    catch { /* skip inaccessible subpaths */ }
                }
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
            return total;
        }


        string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var p = path.Trim();
            if ((p.StartsWith("\"") && p.EndsWith("\"")) || (p.StartsWith("'") && p.EndsWith("'")))
                p = p[1..^1];
            return p.Replace('/', '\\').Trim();
        }

        private static string GetPlatformCopyLabel(Platform p) => p switch
        {
            Platform.Xstation => "Root",
            Platform.Saroo => "\\Saroo\\ISO\\",
            Platform.Summercart64 => "Roms",
            Platform.Gamecube => "Games",
            _ => "Root"
        };


        // === Robust copy helpers for Saroo ===
        private async System.Threading.Tasks.Task CopyFileRobustAsync(string src, string dest, System.Threading.CancellationToken token)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
            const int BufferSize = 1024 * 1024; // 1 MB
            using (var inStream = new System.IO.FileStream(src, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, BufferSize, useAsync: true))
            using (var outStream = new System.IO.FileStream(dest, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await inStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                {
                    await outStream.WriteAsync(buffer.AsMemory(0, read), token);
                }
            }
            try
            {
                var ti = System.IO.File.GetLastWriteTime(src);
                System.IO.File.SetLastWriteTime(dest, ti);
            }
            catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        private async System.Threading.Tasks.Task CopyDirectoryRobustAsync(string srcDir, string destDir, System.Threading.CancellationToken token)
        {
            foreach (var dir in System.IO.Directory.GetDirectories(srcDir, "*", System.IO.SearchOption.AllDirectories))
            {
                var rel = dir.Substring(srcDir.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(destDir, rel));
            }
            foreach (var file in System.IO.Directory.GetFiles(srcDir, "*", System.IO.SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                var rel = file.Substring(srcDir.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var dest = System.IO.Path.Combine(destDir, rel);
                await CopyFileRobustAsync(file, dest, token);
            }
        }
    }
}
