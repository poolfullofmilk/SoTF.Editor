# SoTF Editor

A savegame editor for Sons of the Forest, with a verified backup before every save.

![Overview](Screenshots/Overview.png)

| Storage | NPCs | Map |
|---|---|---|
| ![Storage](Screenshots/Storage.png) | ![NPCs](Screenshots/Npcs.png) | ![Map](Screenshots/Map.png) |

## Features
- Player stats, outfit, armour and inventory
- Every storage filled, maxed or cleared in one click
- Kelvin and Virginia, spawned copies too: stats, outfit, items, revive or spawn more
- Game setup, game state, season and weather, NPCs and structures
- A map to teleport the player and followers or spawn actors
- Updates itself from inside the app
- English and Dutch

## Quick start
1. Download the exe from the [latest release](https://github.com/poolfullofmilk/SoTF.Editor/releases/latest)
2. Run it, there is nothing to install
3. Pick a savegame, edit, then Save (Ctrl S)

## ⚠️ Important
- Start the game and stay in the main menu while you edit, then load the save, so Steam Cloud cannot overwrite your changes
- Every save first copies the whole savegame to `Documents\SoTF.Editor\Backups`, nothing there is ever deleted, and any backup can be restored from the app
- A multiplayer client save only holds your own player, the world lives in the host's save
- Windows may warn on the first run because the app is not signed: click More info, then Run anyway

## Technical details
- .NET 10, WPF, Blazor and MudBlazor, one self-contained exe
- GPL-3.0, the map point data is MIT, see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)
