using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SDBuilderWin.Dreamcast;

#nullable enable

namespace SDBuilderWin
{
    // Dreamcast tab: "Preview List" and "Rebuild Menu" buttons.
    // The actual work lives in Dreamcast\DreamcastMenuBuilder.cs (no UI code there).
    public sealed partial class MainForm
    {
        DreamcastMenuBuilder CreateDcMenuBuilder() =>
            new DreamcastMenuBuilder(Path.Combine(AppContext.BaseDirectory, "Tools"), msg => SafeLog(msg));

        // Logging may be called from the worker thread that builds the image.
        void SafeLog(string msg)
        {
            if (IsHandleCreated && InvokeRequired) BeginInvoke(new Action(() => Info(msg)));
            else Info(msg);
        }

        void WireDreamcastMenuButtons()
        {
            btnDcBuildTrack05.Click += async (_, __) => await WithDriveJobAsync(RebuildDreamcastMenuAsync);
        }

        DcMenuKind SelectedDcMenuKind() =>
            rbDcOpenMenu != null && rbDcOpenMenu.Checked ? DcMenuKind.OpenMenu : DcMenuKind.GdMenu;

        /// <summary>Menu on the card wins, unless the user picked the other one and confirms the switch.</summary>
        DcMenuKind? ResolveDcMenuKind(string sdRoot, bool askOnMismatch)
        {
            var selected = SelectedDcMenuKind();
            var installed = DreamcastMenuBuilder.DetectInstalledMenu(sdRoot);
            if (installed == null || installed == selected) return selected;
            if (!askOnMismatch) return installed;

            var ans = MessageBox.Show(this,
                $"The card has {installed} installed, but {selected} is selected.\n\n" +
                $"Yes = switch the card to {selected}\nNo = keep {installed}",
                "Rebuild Menu", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return ans switch
            {
                DialogResult.Yes => selected,
                DialogResult.No => installed,
                _ => null,
            };
        }

        // Designer-wired (btnDcRebuildList.Click). Read-only: shows the INI that "Rebuild Menu" would write.
        private async void btnRebuildList_Click(object? sender, EventArgs e)
        {
            await WithDriveJobAsync(async (d, token) =>
            {
                var sdRoot = $@"{d}:\";
                var kind = ResolveDcMenuKind(sdRoot, askOnMismatch: false) ?? SelectedDcMenuKind();
                var builder = CreateDcMenuBuilder();

                var (count, ini) = await Task.Run(() =>
                {
                    var games = builder.ScanCard(sdRoot, token);
                    return (games.Count, builder.BuildIni(kind, builder.ReadMenuIp(kind), games));
                }, token);

                Info($"[DC menu] Preview of {DreamcastMenuBuilder.IniFileName(kind)} ({count} games, nothing written):");
                foreach (var line in ini.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                    if (line.Length > 0) Info("    " + line);
                SetDcStatus($"{count} games found. Use 'Rebuild Menu' to write the menu to {d}:\\01.");
            });
        }

        async Task RebuildDreamcastMenuAsync(string d, CancellationToken token)
        {
            var sdRoot = $@"{d}:\";
            var kind = ResolveDcMenuKind(sdRoot, askOnMismatch: true);
            if (kind == null) { Info("[DC menu] Cancelled."); return; }

            var confirm = MessageBox.Show(this,
                $"Rebuild {kind} on {d}:\\01 from the games on the card?",
                "Rebuild Menu", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetDcStatus($"Building {kind}…");
            var builder = CreateDcMenuBuilder();
            var result = await Task.Run(() => builder.BuildAndInstall(sdRoot, kind.Value, token), token);

            SetDcStatus($"{result.Kind} rebuilt with {result.GameCount} games.");
            try { BeginInvoke(new Action(RefreshDreamcastList)); } catch { }
            MessageBox.Show(this,
                $"{result.Kind} rebuilt with {result.GameCount} games.\nWritten to {result.MenuFolder}",
                "Rebuild Menu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
