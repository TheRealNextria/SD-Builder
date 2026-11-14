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

        // Settings I/O
        void LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var dto = JsonSerializer.Deserialize<SettingsDTO>(File.ReadAllText(ConfigFile, Encoding.UTF8));
                    if (dto != null)
                    {
                        CopyTimeout = dto.CopyTimeout;
                        OverwriteTimeout = dto.OverwriteTimeout;
                        OverwriteAutoAction = dto.OverwriteAutoAction ?? "Yes";
                        ShowOnlyRemovable = dto.ShowOnlyRemovable ?? true;
                        EjectPromptEnabled = dto.EjectPromptEnabled ?? false;
                        _lmFolder = dto.LmFolder ?? _lmFolder;

                        // Restore window placement (optional)
                        try
                        {
                            if (dto.WindowW.HasValue && dto.WindowH.HasValue && dto.WindowW > 200 && dto.WindowH > 200)
                            {
                                this.StartPosition = FormStartPosition.Manual;
                                var x = dto.WindowX ?? this.Left;
                                var y = dto.WindowY ?? this.Top;
                                var w = dto.WindowW.Value;
                                var h = dto.WindowH.Value;
                                this.SetBounds(x, y, w, h);
                            }
                            if (dto.Maximized == true)
                            {
                                this.WindowState = FormWindowState.Maximized;
                            }
                        }
                        catch { /* ignore bad persisted values */ }
                    }
                }
            }
            catch { Warn("Could not read settings.json, using defaults."); }

            numCopy.Value = Math.Clamp(CopyTimeout, (int)numCopy.Minimum, (int)numCopy.Maximum);
            numOW.Value = Math.Clamp(OverwriteTimeout, (int)numOW.Minimum, (int)numOW.Maximum);
            cmbAuto.SelectedItem = OverwriteAutoAction is "No" ? "No" : "Yes";
            chkOnlyRemovable.Checked = ShowOnlyRemovable;
            try { chkEjectPrompt.Checked = EjectPromptEnabled; } catch (Exception ex) { Warn("Exception suppressed: " + ex.Message); }
        }

        void SaveSettings()
        {
            var dto = new SettingsDTO(
    (int)numCopy.Value,
    (int)numOW.Value,
    (string)(cmbAuto.SelectedItem ?? "Yes"),
    ShowOnlyRemovable,
    _lmFolder,
    this.WindowState == FormWindowState.Normal ? this.Bounds.X : this.RestoreBounds.X,
    this.WindowState == FormWindowState.Normal ? this.Bounds.Y : this.RestoreBounds.Y,
    this.WindowState == FormWindowState.Normal ? this.Bounds.Width : this.RestoreBounds.Width,
    this.WindowState == FormWindowState.Normal ? this.Bounds.Height : this.RestoreBounds.Height,
    this.WindowState == FormWindowState.Maximized,
        EjectPromptEnabled
);
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json, new UTF8Encoding(false));
            CopyTimeout = (int)numCopy.Value;
            OverwriteTimeout = (int)numOW.Value;
            OverwriteAutoAction = (string)(cmbAuto.SelectedItem ?? "Yes");
        }

        record SettingsDTO(int CopyTimeout, int OverwriteTimeout, string? OverwriteAutoAction, bool? ShowOnlyRemovable, string? LmFolder, int? WindowX, int? WindowY, int? WindowW, int? WindowH, bool? Maximized, bool? EjectPromptEnabled);
    }
}
