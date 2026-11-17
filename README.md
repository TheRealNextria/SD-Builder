# SD-Builder

A Windows tool that puts your games on the SD card of an ODE or flash cart, in the folder layout each
device expects. Pick a drive, tick the games you want, press **Start** – SD-Builder copies them, installs
or updates the firmware/menu and keeps the card cool between games.

Supported devices:

| Tab | Device | Console |
|---|---|---|
| **Xstation** | xStation ODE | PlayStation |
| **Saroo** | SAROO | Sega Saturn |
| **Gamecube** | Swiss (SD2SP2 / SD Gecko) | GameCube |
| **Summercart64** | SummerCart64 + N64FlashcartMenu | Nintendo 64 (incl. 64DD) |
| **Dreamcast** | GDEMU (GDmenu / openMenu) | Dreamcast |

## Features

- **Copy games from a folder or a game list.** Tick games in a tab and press *Start (Checked)*, or
  pick a `.txt` list from the `GameLists` folder and press *Start*.
- **Whole games, also when you pick one file.** Picking one `.bin` of a multi-track game copies its `.cue`
  and all tracks; a Dreamcast `.gdi` brings its track files along.
- **Firmware and menus.** *Check/Download firmware* installs or updates:
  - Xstation `loader.bin` / `update.bin` (newest release from GitHub)
  - SAROO firmware
  - Swiss `IPL.dol` (newest release from GitHub) and Swiss cheat files
  - N64FlashcartMenu `sc64menu.n64`, 64DD IPL and emulators
  - Dreamcast **GDmenu** or **openMenu**, built from the templates in `Tools` and filled with the
    games on the card
- **Dreamcast card management** in the GDMENUCardManager style:
  - each game goes into the next free `\02`, `\03`, … folder as `disc.gdi` / `disc.cdi`
  - `name.txt`, `serial.txt` and `infoNN.txt` are written for every game
  - games already on the card (same serial and disc) are skipped and shown as *Installed*
  - *Rebuild Menu* regenerates the menu after you add or remove games by hand
- **Cool-down between games.** A configurable pause before the next game is written, so the card does
  not overheat during long copy jobs (no pause after the last game).
- **Safe copying.**
  - Checks the free space before it starts.
  - Asks before replacing a game that is already on the card, with an automatic answer after a
    countdown.
  - Stops on the first failed copy and removes the incomplete game.
- **Game list maker** (*Listmaker* tab). Browse a folder (recursive, filter with wildcards such as
  `*.cue`), tick games and save them as a game list.
- **Responsive.** Folders, sizes and card contents are read in the background, so the window keeps
  working on large network shares.

## Requirements

- Windows 10 or 11 (x64)
- Nothing else to install: the release is a self-contained single `.exe` (the .NET 8 runtime is
  included). Copying uses `robocopy`, which is part of Windows.
- Internet access only for downloading firmware.

## Installation

1. Download the latest release.
2. Unzip it to a folder of your choice. Keep the `Tools` folder next to `SD-Builder.exe`: it contains
   the Dreamcast menu templates.
3. Run `SD-Builder.exe`.

SD-Builder stores `settings.json`, the `GameLists` folder and a `temp` folder next to the `.exe`, so
pick a folder you can write to (not `C:\Program Files`).

## Usage

1. Insert the SD card and choose it under **Destination drive**. Tick *Only show removable drives* to
   hide your hard disks.
2. Open the tab for your device.
3. Press **Check/Download firmware** once to install or update the firmware/menu.
4. Copy games:
   - **From a folder:** *Choose…* a folder, pick *Files* or *Directories*, tick games and press
     **Start (Checked)**.
   - **From a game list:** choose a list under *List:* and press **Start**.
5. **Dreamcast only:** after copying, press **Install menu** (or **Rebuild Menu**) so the new games show
   up in GDmenu/openMenu.
6. Press **Eject safely** before you remove the card.

### Where games end up on the card

| Device | Games | Firmware / menu |
|---|---|---|
| Xstation | `\<game>\` | `\00xstation\` |
| Saroo | `\SAROO\ISO\<game>\` | `\SAROO\` |
| Gamecube | `\Games\` | `\IPL.dol`, cheats in `\Swiss\Cheats\` |
| Summercart64 | `\Roms\` | `\sc64menu.n64`, `\menu\64ddipl\`, `\menu\Emulators\` |
| Dreamcast | `\02\`, `\03\`, … | menu in `\01\`, `\GDEMU.ini` |

### Game lists

A game list is a plain `.txt` file in the `GameLists` folder with one path (file or folder) per line.

- Empty lines and lines starting with `#`, `;`, `//`, `::` or `REM` are ignored.
- Paths may be quoted.
- New or changed lists show up in the app automatically.

```text
# Saturn favourites
\\NAS\Games\Saturn\Panzer Dragoon Saga
"\\NAS\Games\Saturn\Radiant Silvergun\Radiant Silvergun.cue"
```

### Settings

| Setting | What it does |
|---|---|
| Copy timeout | Seconds of cool-down before the next game is written (0 = off) |
| Overwrite countdown | Seconds before the replace question is answered automatically |
| Overwrite auto action | The automatic answer: *Yes* (replace) or *No* (skip) |
| Show post-run prompt | When a job is done, offer to open the card in Explorer or eject it |

## Building from source

Requirements: .NET 8 SDK on Windows.

```bash
dotnet build
dotnet publish -c Release -r win-x64
```

The published `.exe` is in `bin\Release\net8.0-windows\win-x64\publish\`. Copy the `tools` folder
next to it.

Project layout:

- `Common/` – shared code: copying, cool-down, free-space check, list scanning, settings, logging
- `Platforms/<device>/` – one folder per tab
- `tools/` – GDmenu and openMenu templates for the Dreamcast menu

## Credits

- [GDMENUCardManager](https://github.com/sonik-br/GDMENUCardManager) by sonik-br – the Dreamcast card
  layout and menu building follow its approach.
- [DiscUtilsGD](https://www.nuget.org/packages/DiscUtilsGD) (Sappharad's DiscUtils fork, MIT) – builds the
  GD-ROM menu image.
- [Swiss](https://github.com/emukidid/swiss-gc), [xStation](https://github.com/x-station/xstation-releases),
  [SAROO](https://github.com/tpunix/SAROO) and [N64FlashcartMenu](https://github.com/n64brew/N64FlashcartMenu)
  – the firmware that SD-Builder downloads; all rights stay with their authors.

## Disclaimer

SD-Builder only copies files that you provide. Use it with games you own. Always keep a backup of your
SD card; the authors are not responsible for lost data.
