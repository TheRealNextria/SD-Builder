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
        System.Windows.Forms.Timer? _hotplugTimer;
        void UpdateDriveButtonsEnabled()
        {
            bool canDriveActions = _cts == null && CurrentDriveLetter() != null;
            btnEject.Enabled = canDriveActions;
            btnOpen.Enabled = canDriveActions;
        }

        // ------------------- Device change -------------------
        protected override void WndProc(ref Message m)
        {
            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVICEARRIVAL = 0x8000;
            const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
            const int DBT_DEVNODES_CHANGED = 0x0007;

            if (m.Msg == WM_DEVICECHANGE)
            {
                int evt = m.WParam.ToInt32();
                if (evt == DBT_DEVICEARRIVAL || evt == DBT_DEVICEREMOVECOMPLETE || evt == DBT_DEVNODES_CHANGED) DebounceHotplugRefresh();
            }
            base.WndProc(ref m);
        }

        void DebounceHotplugRefresh()
        {
            if (_hotplugTimer == null)
            {
                _hotplugTimer = new System.Windows.Forms.Timer { Interval = 800 };
                _hotplugTimer.Tick += (_, __) =>
                {
                    _hotplugTimer!.Stop();
                    RefreshDrives(preserveSelection: true);
                    UpdateDriveButtonsEnabled();
                };
            }
            _hotplugTimer.Stop();
            _hotplugTimer.Start();
        }

        // ------------------- Drives -------------------
        void RefreshDrives(bool preserveSelection = false)
        {
            string? prev = preserveSelection ? CurrentDriveLetter() : null;

            cmbDrives.BeginUpdate();
            try
            {
                cmbDrives.Items.Clear();

                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Where(d => ShowOnlyRemovable ? d.DriveType == DriveType.Removable
                                                  : (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed));

                foreach (var di in drives) cmbDrives.Items.Add($"{di.Name.TrimEnd('\\')} ({di.VolumeLabel})");

                if (cmbDrives.Items.Count > 0)
                {
                    bool matched = false;
                    if (preserveSelection && prev != null)
                    {
                        for (int i = 0; i < cmbDrives.Items.Count; i++)
                        {
                            var s = cmbDrives.Items[i]!.ToString()!;
                            var letter = s.Split(':').FirstOrDefault();
                            if (!string.IsNullOrEmpty(letter) && string.Equals(letter, prev, StringComparison.OrdinalIgnoreCase))
                            {
                                cmbDrives.SelectedIndex = i;
                                matched = true;
                                break;
                            }
                        }
                    }
                    if (!matched) cmbDrives.SelectedIndex = 0;
                }
                else cmbDrives.SelectedIndex = -1;
            }
            finally { cmbDrives.EndUpdate(); }
        }

        string? CurrentDriveLetter()
        {
            if (cmbDrives.SelectedItem is null) return null;
            var s = cmbDrives.SelectedItem.ToString()!;
            var drive = s.Split(':').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(drive)) return null;
            var path = drive.ToUpper() + ":\\";
            return Directory.Exists(path) ? drive.ToUpper() : null;
        }

        // ------------------- Post-run dialog -------------------
        async Task WithDriveJobAsync(Func<string, CancellationToken, Task> action)
        {
            var d = CurrentDriveLetter();
            if (d == null) { Error("Please select a valid drive."); return; }
            if (_cts != null) { Warn("A task is already running."); return; }

            using var cts = new CancellationTokenSource();
            _cts = cts;
            btnStop.Enabled = true; btnEject.Enabled = false; btnOpen.Enabled = false;

            try { await action(d, cts.Token); Info("Done."); }
            catch (OperationCanceledException) { Warn("Cancelled."); }
            catch (Exception ex) { Error("Error: " + ex.Message); }
            finally
            {
                btnStop.Enabled = false; _cts = null; UpdateDriveButtonsEnabled();
                if (EjectPromptEnabled) await PostRunPromptAsync(d);
            }
        }

        async Task PostRunPromptAsync(string driveLetter)
        {
            using var dlg = new Form { Text = "Completed", Width = 420, Height = 180, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            dlg.Icon = Icon;
            var lbl = new Label { Left = 12, Top = 12, Width = 380, Height = 50, AutoSize = false, Text = $"All operations on {driveLetter}:\\ are complete.\nWhat would you like to do?" };
            var btnOpenX = new Button { Text = "Open in Explorer", Left = 12, Top = 80, Width = 130, DialogResult = DialogResult.Retry };
            var btnEjectX = new Button { Text = "Eject now", Left = 152, Top = 80, Width = 100, DialogResult = DialogResult.Yes };
            var btnCloseX = new Button { Text = "Close", Left = 262, Top = 80, Width = 100, DialogResult = DialogResult.Cancel };
            dlg.Controls.AddRange(new Control[] { lbl, btnOpenX, btnEjectX, btnCloseX });
            dlg.AcceptButton = btnOpenX;

            var res = dlg.ShowDialog(this);
            if (res == DialogResult.Retry) OpenDriveInExplorer(driveLetter);
            else if (res == DialogResult.Yes) await EjectDriveLetterAsync(driveLetter);
        }

        void OpenCurrentDriveInExplorer()
        {
            var d = CurrentDriveLetter();
            if (d == null) { Warn("No drive selected."); return; }
            OpenDriveInExplorer(d);
        }
        void OpenDriveInExplorer(string d)
        {
            string path = d.ToUpper() + ":\\";
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); Info($"Opened {path} in Explorer."); }
            catch (Exception ex) { Warn("Could not open Explorer: " + ex.Message); }
        }

        async Task EjectCurrentDriveAsync()
        {
            var d = CurrentDriveLetter();
            if (d == null) { Error("No drive selected."); return; }
            await EjectDriveLetterAsync(d);
        }
        async Task EjectDriveLetterAsync(string d)
        {
            if (_cts != null) { Warn("Stop the current task before ejecting."); return; }
            btnEject.Enabled = false;
            try
            {
                Info($"Ejecting {d}:");
                var ok = await Task.Run(() => TryEjectVolume(d));
                if (ok) { Info($"Safely ejected {d}:\\"); RefreshDrives(preserveSelection: false); }
                else { Warn($"Could not safely eject {d}:\\ (it may be in use by another process)."); }
            }
            finally { UpdateDriveButtonsEnabled(); }
        }

        bool TryEjectVolume(string driveLetter)
        {
            string volPath = @"\\.\\" + driveLetter.TrimEnd(':') + ":";
            const uint GENERIC_READ = 0x80000000;
            const uint GENERIC_WRITE = 0x40000000;
            const uint FILE_SHARE_READ = 0x00000001;
            const uint FILE_SHARE_WRITE = 0x00000002;
            const uint OPEN_EXISTING = 3;
            const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
            const uint FSCTL_LOCK_VOLUME = 0x00090018;
            const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
            const uint IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;
            const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;

            using SafeFileHandle h = CreateFileW(volPath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
            if (h.IsInvalid) return false;

            int br;
            for (int i = 0; i < 5; i++)
            {
                if (DeviceIoControl(h, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out br, IntPtr.Zero)) break;
                System.Threading.Thread.Sleep(300);
                if (i == 4) return false;
            }

            if (!DeviceIoControl(h, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out br, IntPtr.Zero)) return false;

            PREVENT_MEDIA_REMOVAL pmr = new PREVENT_MEDIA_REMOVAL { PreventMediaRemoval = 0 };
            IntPtr pmrPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PREVENT_MEDIA_REMOVAL>());
            try
            {
                Marshal.StructureToPtr(pmr, pmrPtr, false);
                DeviceIoControl(h, IOCTL_STORAGE_MEDIA_REMOVAL, pmrPtr, Marshal.SizeOf<PREVENT_MEDIA_REMOVAL>(), IntPtr.Zero, 0, out br, IntPtr.Zero);
            }
            finally { Marshal.FreeHGlobal(pmrPtr); }

            if (!DeviceIoControl(h, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out br, IntPtr.Zero))
            {
                return true; // unmounted; not all devices support explicit eject
            }
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PREVENT_MEDIA_REMOVAL { public byte PreventMediaRemoval; }
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
        static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
    }
}
