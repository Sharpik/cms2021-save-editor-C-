# CMS21 Save Editor (C#)

A desktop application for editing **Car Mechanic Simulator 2021** save files (`.cms21b`), written in **C# (.NET Framework 4.8, WinForms)**.

> **Always back up your save files before editing.**
> Default save location on Windows:
> `%USERPROFILE%\AppData\LocalLow\Red Dot Games\Car Mechanic Simulator 2021\Saves\`

---

## Features

| Tab | What you can edit |
|---|---|
| **Overview** | Player stats — money (credits), level, XP, available skill points. Shows profile info (name, save date, game version). |
| **Garage** | Lock / unlock all 23 garage upgrades (paint shop, dyno, OBD scanner, lifter, etc.) including the tier level of each item, plus a "Max All Upgrades" button. |
| **Parts** | Per-part condition (0–100%), search, filters (All / Worn / Critical), repair individual parts or bulk repair with "Repair All". |
| **Skills** | Individual skill purchased / unlocked state and per-tier flags, plus a "Max All Skills" button. |
| **Cars** | Detected vehicles / major assemblies with per-component condition sliders and a "Repair All Components" action. |

---

## How to use

1. **Run** `Cms21SaveEditor.exe`.
2. **Open** your `.cms21b` save file (the open dialog appears on startup, or use **Open another file**).
3. Edit what you need across the tabs.
4. Click **Save .cms21b** and choose where to write the modified file.
5. **Copy the saved file** back to your saves folder and replace the original (back it up first!).

---

## Save file location

| Platform | Path |
|---|---|
| Windows (Steam/Epic) | `%USERPROFILE%\AppData\LocalLow\Red Dot Games\Car Mechanic Simulator 2021\Saves\` |
| Windows (Xbox Game Pass) | `%LOCALAPPDATA%\Packages\RedDotGames.CarMechanicSimulator2021_h6vh6...\SystemAppData\wgs\` |
| Linux (Steam) | `~/.steam/steam/userdata/<id>/1190000/remote/` |

Profiles are named `profile0.cms21b`, `profile1.cms21b`, etc.

### Xbox Game Pass (PC) specifics

The Game Pass version uses the Windows Game Save (WGS) system:

1. Files in the `wgs` folder have no extension and use long hexadecimal names.
2. Identify your save by size (usually 8–30 KB) in the most recently modified subfolder.
3. **Keep the original hash filename** when replacing the file.
4. To avoid cloud sync overwriting your edit: disconnect from the internet, replace the file, launch the game, reconnect once at the main menu, make a small in-game change, and choose **"Use Local Save"** when prompted.

---

## Building

Requires **Visual Studio 2022+** with the **.NET Framework 4.8 targeting pack** (or MSBuild + the targeting pack).

```bash
MSBuild.exe src/Cms21SaveEditor/Cms21SaveEditor.csproj /p:Configuration=Release
```

Output: `src/Cms21SaveEditor/bin/Release/Cms21SaveEditor.exe`

### Running the codec tests

The `tools/CodecTest` console harness validates the binary codec (decode, byte-exact round-trip, and edit application) against a synthetic save file:

```bash
csc /t:exe /out:codec_test.exe tools/CodecTest/CodecTest.cs src/Cms21SaveEditor/Core/SaveFile.cs src/Cms21SaveEditor/Core/SaveData.cs
```

---

## Project structure

```
cms2021-save-editor/
└── src/Cms21SaveEditor/         # The entire C# WinForms application
    ├── Core/
    │   ├── SaveFile.cs          # Binary codec (decode / encode / parse / apply edits)
    │   └── SaveData.cs          # Data model (header, parts, stats, garage, skills, cars)
    ├── UI/
    │   ├── MainForm.cs          # Main window: open/save, tabs, dirty tracking
    │   ├── OverviewPanel.cs     # Player stats + profile info
    │   ├── GaragePanel.cs       # Garage upgrades
    │   ├── PartsPanel.cs        # Inventory parts
    │   ├── SkillsPanel.cs       # Skills
    │   └── CarsPanel.cs         # Vehicles / components
    ├── Program.cs
    └── Properties/AssemblyInfo.cs
└── tools/CodecTest/             # Codec test harness
```

---

## Binary format notes

The `.cms21b` format uses little-endian integers, IEEE-754 floats, and length-prefixed UTF-8 strings (1-byte length + data). See `src/Cms21SaveEditor/Core/SaveFile.cs` for the full annotated implementation.

```
Magic         8 bytes   "PJOOOTER"
Header block  variable  profile name, date, version, 0xff padding
Parts section variable  per-part binary records (47 / 63 / 64 bytes each)
Tail section  variable  skills, garage state, car records, player stats
```

Dynamic offsets in the tail section are located by byte-pattern search:

| Field | How it's found |
|---|---|
| Stats block (money/level/XP) | Last occurrence of `5c fe ff ff 00 00 f0 41`, then +17 bytes |
| Money | stats_offset + 0, uint32 LE |
| Level | stats_offset + 4, uint32 LE, stored as `level - 1` |
| XP | stats_offset + 8, uint32 LE |
| Skill points | garage_offset − 4, uint32 LE |
| Garage section | First occurrence of `17 00 00 00 09 paintshop`; 23 items × 8 bytes; `byte[0]` = tier/purchase state |
| Skills section | First occurrence of `fast_movement`, back up to the preceding uint32 count |

---

## License

MIT.
