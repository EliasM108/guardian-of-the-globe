<p align="center"><img src="docs/banner.png" alt="Guardian of the Globe" width="720"></p>

# Guardian of the Globe

Native PC recompilation tooling for **Ben 10: Protector of Earth** (PS2). You bring your own disc image; the app turns it into a program that runs natively on your PC. It has its own menu for building, playing and settings.

> Unofficial fan project. Not affiliated with, endorsed by or connected to the owners of Ben 10 or the game. **No game files, disc images or game code are included or ever uploaded.** You need your own legally obtained copy of the game.

## Status (v0.1.0)

| Version | Disc ID | Status |
|---|---|---|
| USA (NTSC) | SLUS_216.61 | works |
| EU (PAL) | SLES_549.51 | works (includes a fix for a startup race in the PS2 IOP emulation, see `docs/EU-region-fix.md`) |

The Graphics settings tab is not finished yet and is greyed out. Full screen and widescreen are not available from the menu yet.

## Quick start (Windows 10/11, 64-bit)

1. Download this repository as a ZIP from the green **Code** button and unzip it anywhere.
2. Download **`toolpack.zip`** from the [v0.1.0 release](https://github.com/EliasM108/guardian-of-the-globe/releases/tag/v0.1.0) and put it next to `Guardian of the Globe.exe` (do not unzip it).
3. Start `Guardian of the Globe.exe`.
4. Press **COMPILE GAME** and pick your `.iso` (USA or EU). The first build takes a while and needs a few GB of free disk space.
5. Press **PLAY**.

The app works fully offline. It never downloads anything and never checks for updates: the build tools come from `toolpack.zip`.

## Settings

- **General:** keep the menu open while playing, save folder, game files.
- **Sound:** volume and mute (also F3 / F4 / F6 in the game).
- **Controls:** keyboard, mouse and controller. Press a row, then press the key or button you want within about 5 seconds; with no input the row is left empty.
- Saves (memory card) are kept in the save folder, by default `Documents\Guardian of the Globe\Saves`.

In the game: F1 quick menu, F5 quick save, F8 quick load, F11 full screen.

## Legal and credit

- Built on [exoteric33/ps2-native-recomp](https://github.com/exoteric33/ps2-native-recomp) (MIT, with Samir Nuri). See `LICENSE` and `NOTICE.md`.
- This repository contains tooling, configs, patches and our own interface only. All artwork in the app is original and drawn in code.
- Ben 10 and related names belong to their respective owners.
