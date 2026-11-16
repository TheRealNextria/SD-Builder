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
    // "Start checked" for systems that store each game as a folder of .cue/.bin files
    // (Xstation and Saroo). Both tabs call CopyCueGroupsCheckedAsync with their own list and target.
    public sealed partial class MainForm
    {
        /// <summary>
        /// Files mode: every checked file is put in a group per game (its .cue plus the tracks the .cue lists,
        /// or the parent folder when there is no .cue) and each group is copied to &lt;destRoot&gt;\&lt;game&gt;.
        /// Checked folders, and everything in Folders mode, are copied as a whole with CopyEntry.
        /// </summary>
        async Task CopyCueGroupsCheckedAsync(string destDrive, string destRoot, string systemName, Platform platform,
                                             ListView list, bool filesMode, CancellationToken token)
        {
            Directory.CreateDirectory(destRoot);
            int copied = 0;

            if (!filesMode)
            {
                if (!await ConfirmFreeSpaceAsync(destDrive, CheckedPaths(list), token)) return;
                foreach (var path in CheckedPaths(list))
                {
                    if (!File.Exists(path) && !Directory.Exists(path)) { Info($"[SKIP] Not found: \"{path}\""); continue; }
                    await CopyEntry(path, platform, destRoot, token);
                    copied++;
                }
                if (copied == 0) Warn("No items checked.");
                return;
            }

            var selected = new List<string>();
            foreach (var p in CheckedPaths(list))
            {
                if (File.Exists(p) || Directory.Exists(p)) selected.Add(p);
                else Info($"[SKIP] Not found: \"{p}\"");
            }
            if (selected.Count == 0) { Warn("No items checked."); return; }

            // Grouping reads the .cue files (often on a network share), so do it off the UI thread.
            var groups = await Task.Run(() => GroupDiscFiles(selected.Where(File.Exists)), token);
            var dirs = selected.Where(Directory.Exists).ToList();

            // Count what is really copied: one checked .bin brings its .cue and the other tracks along.
            if (!await ConfirmFreeSpaceAsync(destDrive, groups.Values.SelectMany(m => m).Concat(dirs), token)) return;
            var destLabel = DestinationLabel(destDrive, destRoot);

            foreach (var (groupName, members) in groups.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(kv => (kv.Key, kv.Value)))
            {
                WaitForCopyCooldown(token); // pause for the previous game before starting (and logging) this one
                Info($"Copying Game Files → '{groupName}' → {destLabel}");

                var dest = Path.Combine(destRoot, groupName);
                if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
                {
                    var d = ConfirmReplaceCountdown(dest, OverwriteTimeout, $"{systemName} folder '{groupName}'", folderPromptOnlyWhenNonEmpty: true, token: token);
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
                CopyCooldown(token);
            }

            foreach (var d in dirs)
            {
                await CopyEntry(d, platform, destRoot, token);
                copied++;
                CopyCooldown(token);
            }
        }

        /// <summary>"Root" for the card root, otherwise the folder below it (e.g. "Saroo\ISO").</summary>
        static string DestinationLabel(string destDrive, string destRoot)
        {
            var root = destDrive + @":\";
            var full = destRoot.TrimEnd('\\') + @"\";
            var rel = full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
            rel = rel.Trim('\\');
            return rel.Length == 0 ? "Root" : rel;
        }

        /// <summary>
        /// Groups loose disc files per game. A .cue brings the tracks it lists; another file joins the .cue in its
        /// folder (the only one, the one named after the folder, or the one that mentions the file); without a .cue
        /// the file is grouped under its parent folder name.
        /// </summary>
        static Dictionary<string, List<string>> GroupDiscFiles(IEnumerable<string> files)
        {
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            void Add(string key, IEnumerable<string> members)
            {
                if (!groups.TryGetValue(key, out var lst)) groups[key] = lst = new List<string>();
                foreach (var m in members) if (!lst.Contains(m, StringComparer.OrdinalIgnoreCase)) lst.Add(m);
            }

            foreach (var f in files)
            {
                if (Path.GetExtension(f).Equals(".cue", StringComparison.OrdinalIgnoreCase))
                {
                    Add(Path.GetFileNameWithoutExtension(f), ParseCueSet(f));
                    continue;
                }

                var dir = Path.GetDirectoryName(f) ?? "";
                var cue = FindCueForFile(f, dir);
                if (cue != null)
                {
                    Add(Path.GetFileNameWithoutExtension(cue), ParseCueSet(cue));
                }
                else
                {
                    var parent = string.IsNullOrEmpty(dir) ? "" : new DirectoryInfo(dir).Name;
                    Add(string.IsNullOrWhiteSpace(parent) ? Path.GetFileNameWithoutExtension(f) : parent, new[] { f });
                }
            }
            return groups;
        }

        static string? FindCueForFile(string file, string dir)
        {
            string[] cues;
            try { cues = Directory.GetFiles(dir, "*.cue"); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }

            if (cues.Length == 1) return cues[0];
            if (cues.Length == 0) return null;

            var parentName = string.IsNullOrEmpty(dir) ? "" : new DirectoryInfo(dir).Name;
            var named = cues.FirstOrDefault(c => string.Equals(Path.GetFileNameWithoutExtension(c), parentName, StringComparison.OrdinalIgnoreCase));
            if (named != null) return named;

            var fname = Path.GetFileName(file);
            foreach (var c in cues)
            {
                try
                {
                    if (File.ReadLines(c).Any(l => l.IndexOf(fname, StringComparison.OrdinalIgnoreCase) >= 0)) return c;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return null;
        }

        /// <summary>The .cue itself plus every existing file it references with FILE "...".</summary>
        static List<string> ParseCueSet(string cuePath)
        {
            var result = new List<string> { cuePath };
            try
            {
                var dir = Path.GetDirectoryName(cuePath) ?? "";
                foreach (var line in File.ReadAllLines(cuePath))
                {
                    var t = line.Trim();
                    if (!t.StartsWith("FILE", StringComparison.OrdinalIgnoreCase)) continue;

                    string? rel = null;
                    int q1 = t.IndexOf('"');
                    int q2 = q1 >= 0 ? t.IndexOf('"', q1 + 1) : -1;
                    if (q1 >= 0 && q2 > q1) rel = t.Substring(q1 + 1, q2 - q1 - 1);
                    else
                    {
                        var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) rel = parts[1];
                    }
                    if (string.IsNullOrWhiteSpace(rel)) continue;

                    var full = Path.GetFullPath(Path.Combine(dir, rel));
                    if (File.Exists(full) && !result.Contains(full, StringComparer.OrdinalIgnoreCase)) result.Add(full);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return result;
        }
    }
}
