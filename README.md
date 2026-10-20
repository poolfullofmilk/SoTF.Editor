# SoTF Editor

A savegame editor for Sons of the Forest, with a verified backup before every save.

![Overview](Screenshots/Overview.png)

<table>
  <tr><th width="33%">Inventory</th><th width="33%">Storage</th><th width="33%">Followers</th></tr>
  <tr><td><img src="Screenshots/Inventory.png" alt="Inventory" width="100%"></td><td><img src="Screenshots/Storage.png" alt="Storage" width="100%"></td><td><img src="Screenshots/Followers.png" alt="Followers" width="100%"></td></tr>
  <tr><th width="33%">Game</th><th width="33%">NPCs</th><th width="33%">Structures</th></tr>
  <tr><td><img src="Screenshots/Game.png" alt="Game" width="100%"></td><td><img src="Screenshots/Npcs.png" alt="NPCs" width="100%"></td><td><img src="Screenshots/Structures.png" alt="Structures" width="100%"></td></tr>
  <tr><th width="33%">Map</th><th width="33%">Tools</th><th width="33%">Backups</th></tr>
  <tr><td><img src="Screenshots/Map.png" alt="Map" width="100%"></td><td><img src="Screenshots/Tools.png" alt="Tools" width="100%"></td><td><img src="Screenshots/Backups.png" alt="Backups" width="100%"></td></tr>
</table>

## Features
- Player stats, outfit, armour and one inventory list of every item
- Every storage filled, maxed or cleared in one click
- Kelvin and Virginia, spawned copies too: stats, outfit, items, revive, spawn more or remove the extra ones
- Game setup, game state, season and weather, NPCs and structures, with info buttons that explain each setting
- Game presets such as Hard that you can still tweak setting by setting
- A small yellow dot on every value you changed and on its page in the menu, and a summary of the changed pages before every save
- A map to teleport the player and followers or spawn actors, with a useful default selection you can reset to
- Tools on their own page, such as regrow trees, reset fires, traps and containers, and item plating
- Updates itself from inside the app and shows what changed
- English and Dutch

## Quick start
1. Download the exe from the [latest release](https://github.com/poolfullofmilk/SoTF.Editor/releases/latest)
2. Run it, there is nothing to install
3. Pick a savegame, edit, then Save (Ctrl S)

## ⚠️ Important
- Start the game and stay in the main menu while you edit, then load the save, so Steam Cloud cannot overwrite your changes
- Every save first copies the whole savegame to `Documents\SoTF.Editor\Backups`, and any backup can be restored or deleted from the app
- Past 500 MB of backups the oldest go, but every savegame keeps its first one
- A multiplayer client save only holds your own player, the world lives in the host's save
- Windows may warn on the first run because the app is not signed: click More info, then Run anyway

## Technical details
- .NET 10, WPF, Blazor and MudBlazor, one self-contained exe
- Every release first passes a save round trip test on a sample savegame
- GPL-3.0, builds on SOTFEdit and map point data that are both MIT, see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)
