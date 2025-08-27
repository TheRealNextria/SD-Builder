# SD-Builder (Windows Forms)

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
![Windows](https://img.shields.io/badge/OS-Windows-blue?logo=windows)
![WinForms](https://img.shields.io/badge/UI-WinForms-0078D6)
![Status](https://img.shields.io/badge/state-actively_developed-brightgreen)

**SD-Builder** is a C# WinForms app for managing game lists and firmware files across multiple retro platforms — with a fast, responsive **Listmaker** for building `.txt` lists. The UI is fully designer-based (Visual Studio Designer friendly).

---

## Table of Contents
- [Highlights](#highlights)
- [Everyday Workflow](#everyday-workflow)
- [Shortcuts](#shortcuts)
- [Platforms Supported](#platforms-supported)
- [Build](#build)
- [Project Structure](#project-structure)
- [Settings](#settings)
- [Version](#version)
- [License](#license)

---

## Highlights

- **Listmaker (Files/Dirs)**
  - Async, cancellable scanning (UI stays responsive)
  - Live byte counting for files & directories
  - **Live filter** as you type (no `*` needed — `zelda` matches `*zelda*`)
  - Drag & drop: drop a **folder** to load, or a **.txt** to import
  - Context menu: **Open**, **Reveal in Explorer**, **Remove**
  - **Checked-only save** to `.txt`
  - **Bulk select/unselect** with debounce + bulk-guard (no stutter)

- **Firmware helpers**
  - Xstation, **SAROO**, **GameCube (Swiss IPL)**, **Summercart64**
  - SC64 extras: **Install 64DD IPL** and **Install Emulators**

- **Drives & Settings**
  - Drive picker with Refresh / Stop / Open / Eject
  - Settings load/save (Only removable, timeouts, overwrite auto-action)
  - Custom app icon (EXE + Form)

- **Designer-friendly**
  - All controls in `MainForm.Designer.cs`
  - Logic & event handlers in `MainForm.cs`
  - Cleaned Designer (reduced duplicate property churn)

---

## Everyday Workflow

1. Open the **Listmaker** tab.  
2. Choose **Files** or **Dirs** and pick a folder (or drag & drop one).  
3. Type to filter (e.g., `zelda` → matches `*zelda*` for files).  
4. Check the items you want, then **Save** to a `.txt`.  
5. Use the firmware tabs to check/download platform firmware as needed.

> Tip: Store your lists in a `GameLists` folder next to the EXE.

---

## Shortcuts

- **Ctrl + O** — Open `.txt` (Listmaker)  
- **Ctrl + S** — Save checked items to `.txt` (Listmaker)  
- **Ctrl + A** — Check all  
- **Ctrl + Shift + A** — Uncheck all  
- **Enter** (in filter) — Instant refresh  


---

## Platforms Supported

- **Xstation**  
- **Sega Saturn SAROO**  
- **GameCube** — Swiss IPL  
- **Summercart64 (SC64)** — **Install 64DD IPL** + **Install Emulators**

> Exact file locations/filenames are handled by each platform’s flow.

---

## Build

**Requirements:** Windows, .NET 8 SDK (or newer), Visual Studio 2022 (recommended)

**Open & build in VS:**
- Open `SDBuilder.Win.csproj` and **Build**.

**CLI:**
```bash
dotnet build SDBuilder.Win.csproj -c Release
