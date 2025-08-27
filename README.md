SD-Builder (Windows Forms)

SD-Builder is a C# WinForms app for managing game lists and firmware files for multiple retro platforms — with a fast, responsive “Listmaker” for building .txt lists and one-click firmware helpers. The UI is fully designer-based, so you can edit everything in Visual Studio’s Designer.

✨ Highlights

Listmaker (Files/Dirs)

Fast async scanning (UI stays responsive), cancellable

Live byte counting for files & directories

Live filter as you type (no * needed; typing zelda matches *zelda*)

Drag & drop: drop a folder to load it or a .txt to import a list

Context menu: Open, Reveal in Explorer, Remove

Checked-only save to .txt

Bulk select/unselect with debounce + bulk-guard (no UI stutter)

Firmware helpers

Xstation, SAROO, GameCube (Swiss IPL), Summercart64

SC64 extras: Install 64DD IPL and Install Emulators

Drives & Settings

Drive picker with Refresh/Stop/Open/Eject

Settings load/save (Only removable, timeouts, overwrite auto-action)

Custom app icon (EXE + Form)

Designer-friendly

All controls live in MainForm.Designer.cs

Logic & event handlers in MainForm.cs

Cleaned Designer (reduced duplicate property churn)

🖱️ Everyday workflow

Open the Listmaker tab.

Choose Files or Dirs, pick a folder (or just drag & drop one).

Type to filter (e.g., zelda → matches *zelda* for files).

Check the items you want, then Save to a .txt.

Use firmware tabs to check/download platform firmware as needed.

Tip: .txt lists are typically stored in a GameLists folder next to the EXE.

⌨️ Shortcuts

Ctrl + O — Open .txt (Listmaker)

Ctrl + S — Save checked items to .txt (Listmaker)

Ctrl + A — Check all (Listmaker)

Ctrl + Shift + A — Uncheck all (Listmaker)

Enter (in filter) — Instant refresh

F5 — Rescan current Listmaker folder

🧩 Platforms supported (firmware flows)

Xstation

Sega Saturn SAROO

GameCube — Swiss IPL

Summercart64 (SC64) — Install 64DD IPL + Install Emulators

Note: Exact locations/filenames are handled by the app’s platform flows; use the platform tabs’ “check/download/install” buttons.

🛠️ Build

Requirements: Windows, .NET 8 SDK (or newer), Visual Studio 2022 recommended.

Open & build:

Open SDBuilder.Win.csproj in Visual Studio and Build.

Or CLI:

dotnet build SDBuilder.Win.csproj -c Release


Run: Start the built EXE from bin\Release\net8.0-windows\.

📁 Project structure
SDBuilderWin/
├─ MainForm.cs                 // logic & event handlers
├─ MainForm.Designer.cs        // all UI elements (Designer-editable)
├─ MainForm.resx
├─ Program.cs
├─ SDBuilder.Win.csproj
├─ app.manifest
└─ Assets/
   └─ SDBuilder_Icon.ico

🔧 Settings (UI)

Only removable — limit drive list to removable devices.

Timeouts — copy/overwrite related timings.

Overwrite auto-action — Yes/No default for overwrite prompts.

Settings are saved and loaded automatically.

🧭 Version

Current baseline: v2.31 — Designer refactor + SC64 64DD IPL + Emulators + Listmaker QoL (drag&drop, context menu, shortcuts, checked-only save, live filter).
