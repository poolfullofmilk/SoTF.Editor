# SoTF Editor

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
- Edit your stats, armour, outfit and inventory
- Fill every storage to the max in one click
- Edit, revive or spawn Kelvin and Virginia
- Edit NPCs one at a time or all at once
- Change the game settings, the weather and the season
- Finish or remove structures
- Teleport yourself or anyone else on the map, and search every point on it
- Tools such as regrowing trees and calming cannibals
- Copy a hosted multiplayer save to singleplayer
- See every change before you save, and undo them page by page
- English, Dutch, German, French and Turkish, and it updates itself

## Quick start
1. Download the exe from the [latest release](https://github.com/poolfullofmilk/SoTF.Editor/releases/latest)
2. Run it, there is nothing to install
3. Pick a savegame, edit, then Save

## ⚠️ Important
- Start the game and stay in the main menu while you edit, then load the save, so Steam Cloud cannot overwrite your changes
- Every save makes a backup first, which you can restore from the app
- Windows may warn on the first run because the app is not signed: click More info, then Run anyway

## Technical details
- .NET 10, WPF, Blazor and MudBlazor, one self-contained exe
- GPL-3.0, builds on SOTFEdit and map point data that are both MIT, see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)
