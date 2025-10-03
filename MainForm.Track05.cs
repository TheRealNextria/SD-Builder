#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SDBuilderWin
{
    public sealed partial class MainForm : Form
    {
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                btnDcBuildTrack05.Click -= btnDcBuildTrack05_Click;
                btnDcBuildTrack05.Click += btnDcBuildTrack05_Click;
            }
            catch { }
        }

        private void btnDcBuildTrack05_Click(object? sender, EventArgs e)
        {
            try
            {
                _ = WithDriveJobAsync(BuildTrack05IsoAsync);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Track05.iso", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string? FindExe(params string[] candidates)
        {
            foreach (var c in candidates)
            {
                try { if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) return c; } catch { }
            }
            return null;
        }

        private static (int exitCode, string output) RunToolCapture(string exe, string args, string? workDir = null, int timeoutMs = 600000)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workDir) ? "" : workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = new System.Diagnostics.Process { StartInfo = psi };
            var sb = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                return (-1, "Timeout running " + exe);
            }
            return (p.ExitCode, sb.ToString());
        }

                private async Task BuildTrack05IsoAsync(string drive, System.Threading.CancellationToken token)
        {
            await Task.Yield();

            // Detect firmware format via serial.txt or radio button
            string sdRoot = drive.EndsWith(":") ? drive + "\\" : drive + ":\\";
            string fmt = DetectDreamcastMenuFormatFromSerialTxt(sdRoot); // "GDMenu" or "OpenMenu"
            if (fmt != "GDMenu" && fmt != "OpenMenu")
            {
                try { fmt = (rbDcOpenMenu != null && rbDcOpenMenu.Checked) ? "OpenMenu" : "GDMenu"; } catch { fmt = "GDMenu"; }
            }
            string volume = (fmt == "GDMenu") ? "GDMENU" : "NEODC_1";
            string sub = (fmt == "GDMenu") ? "gdMenu" : "openMenu";

            // Paths
            string appBase = AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            string toolsRoot = System.IO.Path.Combine(appBase, "tools");
            string dataDir   = System.IO.Path.Combine(toolsRoot, sub, "menu_data");
            string gdiDir    = System.IO.Path.Combine(toolsRoot, sub, "menu_gdi");
            string outDir    = System.IO.Path.Combine(toolsRoot, sub, "output");
            string gdiPath   = System.IO.Path.Combine(gdiDir, "disc.gdi");

            // Validate inputs
            if (!System.IO.Directory.Exists(dataDir))
            {
                MessageBox.Show(this, $"{fmt}: menu_data not found.\nExpected: tools\\{sub}\\menu_data", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!System.IO.Directory.Exists(gdiDir) || !System.IO.File.Exists(gdiPath))
            {
                MessageBox.Show(this, $"{fmt}: menu_gdi / disc.gdi not found.\nExpected: tools\\{sub}\\menu_gdi\\disc.gdi", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            bool haveTrack03 = System.IO.File.Exists(System.IO.Path.Combine(gdiDir, "track03.iso")) || System.IO.File.Exists(System.IO.Path.Combine(gdiDir, "track03.bin"));
            if (!haveTrack03)
            {
                MessageBox.Show(this, $"{fmt}: base track03 not found.\nPlace track03.iso (or .bin) in tools\\{sub}\\menu_gdi", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try { System.IO.Directory.CreateDirectory(outDir); } catch { }

            // Ensure menu INI exists
            string iniName = (fmt == "GDMenu") ? "LIST.INI" : "OPENMENU.INI";
            if (!System.IO.File.Exists(System.IO.Path.Combine(dataDir, iniName)))
            {
                MessageBox.Show(this, $"{iniName} not found in tools\\{sub}\\menu_data.\nUse 'Rebuild List' first.", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Tools
            string? buildgdi = FindExe(System.IO.Path.Combine(toolsRoot, "bin", "buildgdi.exe"),
                                       System.IO.Path.Combine(appBase, "buildgdi.exe"));
            string? mkisofs  = FindExe(System.IO.Path.Combine(toolsRoot, "bin", "mkisofs.exe"),
                                       System.IO.Path.Combine(appBase, "mkisofs.exe"));
            if (string.IsNullOrEmpty(buildgdi) || !System.IO.File.Exists(buildgdi))
            {
                MessageBox.Show(this, "buildgdi.exe not found. Place it in tools\\bin.", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Optional IP.BIN
            string? ipBin = null;
            try
            {
                string ip1 = System.IO.Path.Combine(toolsRoot, sub, "ip.bin");
                string ip2 = System.IO.Path.Combine(toolsRoot, sub, "IP.BIN");
                if (System.IO.File.Exists(ip1)) ipBin = ip1;
                else if (System.IO.File.Exists(ip2)) ipBin = ip2;
            } catch { }

            // Rebuild using buildgdi
            string args = $" -rebuild -gdi \"{gdiPath}\" -data \"{dataDir}\" -output \"{outDir}\" -iso -truncate -V \"{volume}\"";
            if (!string.IsNullOrEmpty(ipBin) && System.IO.File.Exists(ipBin))
                args += $" -ip \"{ipBin}\"";

            var res = RunToolCapture(buildgdi, args, workDir: toolsRoot, timeoutMs: 600000);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "builder_last.txt"),
                    $"buildgdi {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{buildgdi} {args}\r\n\r\n{res.output}\r\n");
            } catch { }

            // Fallback to mkisofs only if buildgdi failed
            if (res.exitCode != 0)
            {
                if (string.IsNullOrEmpty(mkisofs) || !System.IO.File.Exists(mkisofs))
                {
                    MessageBox.Show(this, "buildgdi failed and mkisofs.exe not found.\r\n\r\n" + res.output, "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string isoOut = System.IO.Path.Combine(outDir, "track05.iso");
                string mkArgs = string.IsNullOrEmpty(ipBin) || !System.IO.File.Exists(ipBin)
                    ? $"-l -r -J -V \"{volume}\" -no-pad -m IP.BIN -o \"{isoOut}\" \"{dataDir}\""
                    : $"-l -r -J -V \"{volume}\" -no-pad -G \"{ipBin}\" -m IP.BIN -o \"{isoOut}\" \"{dataDir}\"";

                var resIso = RunToolCapture(mkisofs, mkArgs, workDir: toolsRoot, timeoutMs: 300000);
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(outDir, "builder_last.txt"),
                        $"mkisofs {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{mkisofs} {mkArgs}\r\n\r\n{resIso.output}\r\n");
                } catch { }
                if (resIso.exitCode != 0 || !System.IO.File.Exists(isoOut))
                {
                    MessageBox.Show(this, "mkisofs failed or did not produce an ISO.\r\n\r\n" + resIso.output, "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // Try to resolve built track05 path (ISO or BIN), without renaming anything
            string outTrack = System.IO.Path.Combine(outDir, "track05.iso");
            if (!System.IO.File.Exists(outTrack))
            {
                string outBin = System.IO.Path.Combine(outDir, "track05.bin");
                if (System.IO.File.Exists(outBin)) outTrack = outBin;
                else
                {
                    // Search .gdi for data track >= 5
                    try
                    {
                        var gdis = System.IO.Directory.GetFiles(outDir, "*.gdi", System.IO.SearchOption.TopDirectoryOnly);
                        if (gdis.Length > 0)
                        {
                            foreach (var line in System.IO.File.ReadAllLines(gdis[0]))
                            {
                                var parts = System.Text.RegularExpressions.Regex.Matches(line.Trim(), @"""[^""]*""|\S+");
                                if (parts.Count >= 6 && int.TryParse(parts[0].Value.Trim('"'), out int tn) && tn >= 5)
                                {
                                    var fn = parts[5].Value.Trim('"');
                                    var cand = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(gdis[0])!, fn);
                                    if (System.IO.File.Exists(cand)) { outTrack = cand; break; }
                                }
                            }
                        }
                    } catch { }
                }
            }

            // Validate ISO only if present
            if (outTrack.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(outTrack))
            {
                try
                {
                    var fi = new System.IO.FileInfo(outTrack);
                    if (fi.Length <= 0 || (fi.Length % 2048) != 0)
                    {
                        MessageBox.Show(this, "Output track05 is not 2048-aligned.", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch { /* ignore validation exceptions */ }
            }
            else
            {
                // If no ISO exists, ensure we at least produced some files in outDir
                try
                {
                    if (!System.IO.Directory.Exists(outDir) || System.IO.Directory.GetFiles(outDir, "*", System.IO.SearchOption.TopDirectoryOnly).Length == 0)
                    {
                        MessageBox.Show(this, "No output files were produced.", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                } catch { }
            }

            // Copy everything from outDir to SD:\01, excluding builder_last.txt
            string d01 = System.IO.Path.Combine(sdRoot, "01");
            try { System.IO.Directory.CreateDirectory(d01); } catch { }

            try
            {
                foreach (var f in System.IO.Directory.GetFiles(outDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    var name = System.IO.Path.GetFileName(f);
                    if (name.Equals("builder_last.txt", StringComparison.OrdinalIgnoreCase)) continue;
                    try { System.IO.File.Copy(f, System.IO.Path.Combine(d01, name), true); } catch { }
                }
                void CopyDir(string s, string d2)
                {
                    try { System.IO.Directory.CreateDirectory(d2); } catch { }
                    foreach (var f2 in System.IO.Directory.GetFiles(s, "*", System.IO.SearchOption.TopDirectoryOnly))
                    {
                        var n2 = System.IO.Path.GetFileName(f2);
                        if (n2.Equals("builder_last.txt", StringComparison.OrdinalIgnoreCase)) continue;
                        try { System.IO.File.Copy(f2, System.IO.Path.Combine(d2, n2), true); } catch { }
                    }
                    foreach (var d3 in System.IO.Directory.GetDirectories(s, "*", System.IO.SearchOption.TopDirectoryOnly))
                    {
                        CopyDir(d3, System.IO.Path.Combine(d2, System.IO.Path.GetFileName(d3)));
                    }
                }
                foreach (var dir in System.IO.Directory.GetDirectories(outDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    CopyDir(dir, System.IO.Path.Combine(d01, System.IO.Path.GetFileName(dir)));
                }
            } catch { }

            // Archive output before cleanup
            try
            {
                string archiveRoot = System.IO.Path.Combine(toolsRoot, sub, "output_archive");
                string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string archiveDir = System.IO.Path.Combine(archiveRoot, stamp);
                try { System.IO.Directory.CreateDirectory(archiveDir); } catch { }
                try
                {
                    foreach (var f in System.IO.Directory.GetFiles(outDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                    {
                        try { System.IO.File.Copy(f, System.IO.Path.Combine(archiveDir, System.IO.Path.GetFileName(f)), true); } catch { }
                    }
                    foreach (var d in System.IO.Directory.GetDirectories(outDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                    {
                        void CopyDir(string s, string d2)
                        {
                            try { System.IO.Directory.CreateDirectory(d2); } catch { }
                            foreach (var f2 in System.IO.Directory.GetFiles(s, "*", System.IO.SearchOption.TopDirectoryOnly))
                            {
                                try { System.IO.File.Copy(f2, System.IO.Path.Combine(d2, System.IO.Path.GetFileName(f2)), true); } catch { }
                            }
                            foreach (var dd in System.IO.Directory.GetDirectories(s, "*", System.IO.SearchOption.TopDirectoryOnly))
                            {
                                CopyDir(dd, System.IO.Path.Combine(d2, System.IO.Path.GetFileName(dd)));
                            }
                        }
                        CopyDir(d, System.IO.Path.Combine(archiveDir, System.IO.Path.GetFileName(d)));
                    }
                } catch { }
            } catch { }

            // Cleanup: remove everything from the output folder
            try
            {
                foreach (var f in System.IO.Directory.GetFiles(outDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    try { System.IO.File.Delete(f); } catch { }
                }
                foreach (var d in System.IO.Directory.GetDirectories(outDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    try { System.IO.Directory.Delete(d, true); } catch { }
                }
            } catch { }

            // Success
            MessageBox.Show(this, "Build complete. Files copied to \\\\01.", "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}
