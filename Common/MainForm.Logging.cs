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




        // ------------------- Logging -------------------
        void Info(string msg) => AppendLog(msg);
        void Warn(string msg) => AppendLog("[WARN] " + msg);
        void Error(string msg) => AppendLog("[ERROR] " + msg);
        void AppendLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            if (txtLog.InvokeRequired) txtLog.Invoke(new Action(() => Append(line)));
            else Append(line);
            void Append(string s) { txtLog.AppendText(s + Environment.NewLine); txtLog.SelectionStart = txtLog.TextLength; txtLog.ScrollToCaret(); }
        }
    }
}
