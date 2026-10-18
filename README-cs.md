# CMS21 Save Editor (C#)

Desktopová aplikace pro úpravu souborů uložené hry **Car Mechanic Simulator 2021** (`.cms21b`), napsaná v **C# (.NET Framework 4.8, WinForms)**.

> **Vždy si zálohujte save soubory před jejich úpravou.**
> Výchozí umístění save souborů ve Windows:
> `%USERPROFILE%\AppData\LocalLow\Red Dot Games\Car Mechanic Simulator 2021\Save\`

---

## Funkce

| Záložka | Co lze upravovat |
|---|---|
| **Overview** | Statistiky hráče — peníze (kredity), úroveň, XP, dostupné body dovedností. Zobrazuje informace o profilu (jméno, datum uložení, verze hry). |
| **Garage** | Odemykání / zamykání všech 23 vylepšení garáže (lakovna, dyno, OBD skener, zvedák atd.) včetně úrovně (tier) jednotlivých položek, plus tlačítko „Max All Upgrades“. |
| **Parts** | Stav jednotlivých dílů (0–100 %), vyhledávání, filtry (All / Worn / Critical), oprava jednotlivých dílů nebo hromadná oprava „Repair All“. |
| **Skills** | Zakoupení / odemčení jednotlivých dovedností a jejich úrovní (tierů), plus tlačítko „Max All Skills“. |
| **Cars** | Detekovaná vozidla / hlavní sestavy s posuvníky pro stav jednotlivých komponent a akcí „Repair All Components“. |

---

## Jak používat

1. **Spusťte** `Cms21SaveEditor.exe`.
2. **Otevřete** svůj `.cms21b` save soubor (dialog pro otevření se zobrazí při spuštění, nebo použijte **Open another file**).
3. Upravte si, co potřebujete, v jednotlivých záložkách.
4. Klikněte na **Save .cms21b** a zvolte, kam se má upravený soubor uložit.
5. **Zkopírujte uložený soubor** zpět do složky saveů a nahraďte originál (nejdřív si ho zazálohujte!).

---

## Umístění save souborů

| Platforma | Cesta |
|---|---|
| Windows (Steam/Epic) | `%USERPROFILE%\AppData\LocalLow\Red Dot Games\Car Mechanic Simulator 2021\Save\` |
| Windows (Xbox Game Pass) | `%LOCALAPPDATA%\Packages\RedDotGames.CarMechanicSimulator2021_h6vh6...\SystemAppData\wgs\` |
| Linux (Steam) | `~/.steam/steam/userdata/<id>/1190000/remote/` |

Profily se jmenují `profile0.cms21b`, `profile1.cms21b` atd.

### Specifika Xbox Game Pass (PC)

Verze pro Game Pass používá systém Windows Game Save (WGS):

1. Soubory ve složce `wgs` nemají příponu a používají dlouhé hexadecimální názvy.
2. Svůj save poznáte podle velikosti (obvykle 8–30 KB) v naposledy upravené podsložce.
3. **Zachovejte původní hash název souboru** při jeho nahrazování.
4. Aby vám cloud přepsání nevrátil starý soubor: odpojte se od internetu, nahraďte soubor, spusťte hru, po načtení hlavního menu se znovu připojte, udělejte drobnou změnu ve hře a při výzvě zvolte **„Use Local Save“**.

---

## Sestavení projektu

Vyžaduje **Visual Studio 2022+** s balíčkem **.NET Framework 4.8 targeting pack** (nebo MSBuild + targeting pack).

```bash
MSBuild.exe src/Cms21SaveEditor/Cms21SaveEditor.csproj /p:Configuration=Release
```

Výstup: `src/Cms21SaveEditor/bin/Release/Cms21SaveEditor.exe`

### Spuštění codec testů

Konzolový harness `tools/CodecTest` ověřuje binární codec (dekódování, byte-exact round-trip a aplikaci úprav) na syntetickém save souboru:

```bash
csc /t:exe /out:codec_test.exe tools/CodecTest/CodecTest.cs src/Cms21SaveEditor/Core/SaveFile.cs src/Cms21SaveEditor/Core/SaveData.cs
```

---

## Struktura projektu

```
cms2021-save-editor/
└── src/Cms21SaveEditor/         # Celá C# WinForms aplikace
    ├── Core/
    │   ├── SaveFile.cs          # Binární codec (decode / encode / parse / apply edits)
    │   └── SaveData.cs          # Datový model (header, parts, stats, garage, skills, cars)
    ├── UI/
    │   ├── MainForm.cs          # Hlavní okno: otevírání / ukládání, záložky, sledování změn
    │   ├── OverviewPanel.cs     # Statistiky hráče + info o profilu
    │   ├── GaragePanel.cs       # Vylepšení garáže
    │   ├── PartsPanel.cs        # Díly v inventáři
    │   ├── SkillsPanel.cs       # Dovednosti
    │   └── CarsPanel.cs         # Vozidla / komponenty
    ├── Program.cs
    └── Properties/AssemblyInfo.cs
└── tools/CodecTest/             # Testovací harness codecu
```

---

## Poznámky k binárnímu formátu

Formát `.cms21b` používá little-endian celá čísla, IEEE-754 floaty a řetězce s délkovou předponou UTF-8 (1 bajt délky + data). Viz `src/Cms21SaveEditor/Core/SaveFile.cs` pro kompletní implementaci.

```
Magic         8 bajtů   "PJOOOTER"
Header block  proměnná   jméno profilu, datum, verze, 0xff padding
Parts section proměnná   záznamy dílů (47 / 63 / 64 bajtů každý)
Tail section  proměnná   dovednosti, stav garáže, záznamy aut, statistiky hráče
```

Dynamické offsety v tail sekci se hledají podle bajtových vzorů:

| Pole | Jak se najde |
|---|---|
| Stats block (money/level/XP) | Poslední výskyt `5c fe ff ff 00 00 f0 41`, pak +17 bajtů |
| Money | stats_offset + 0, uint32 LE |
| Level | stats_offset + 4, uint32 LE, uloženo jako `level - 1` |
| XP | stats_offset + 8, uint32 LE |
| Skill points | garage_offset − 4, uint32 LE |
| Garage section | První výskyt `17 00 00 00 09 paintshop`; 23 položek × 8 bajtů; `byte[0]` = tier/stav nákupu |
| Skills section | První výskyt `fast_movement`, zpět na předchozí uint32 count |

---

## Licence

MIT.
