# SoTF Editor

A savegame editor for Sons of the Forest. A WPF window hosts a `BlazorWebView` whose whole UI is MudBlazor, laid out like `WiFiWatch`. It reads a savegame folder, lets people edit it page by page, and writes back only what changed, after a verified copy of the whole savegame.

| Project | Holds |
|---|---|
| `SoTF.Editor.Data` (`net10.0`) | Savegame and game data models, view models, enums, helpers (`AppInfo`, `SaveJson`, `Teleporter`, coordinates), and `Resources`: the resx text, `GameNames.resx`, the embedded `GameData.json`, `Pois.json`, `ItemPois.json` and `ActorTemplate.txt` |
| `SoTF.Editor.Services` (`net10.0`) | `Savegames` (finding, loading, saving, backups, `MainViewModel`), `Pages` (one view model per page), `Actors`, `Map`, `Tools`, `Settings`, `Integration` (update check) |
| `SoTF.Editor.Desktop` | `App` (startup, logging, single instance, DI), `MainWindow` (WebView2 host), `Components`, `Theming/AppTheme`, `Interop` (`LocalFiles`, `WindowCaptionTheme`), `wwwroot` |

Packages: MudBlazor, `Microsoft.AspNetCore.Components.WebView.Wpf`, Newtonsoft.Json, NLog. No MVVM toolkit, no Bootstrap, no tray: the editor has no background work, so closing it exits.

## Build, Run And Publish

```bash
dotnet build SoTF.Editor.slnx
```

```bash
dotnet run --project SoTF.Editor.Desktop
```

```bash
dotnet publish SoTF.Editor.Desktop -c Release
```

The publish folder holds only `SoTF.Editor_v<Version>.exe`: `IncludeAllContentForSelfExtract` and the `BundleWebRoot` and `RemoveLooseWebRoot` targets pack `wwwroot` inside it.

### Releasing

A release is made by the workflow, not by hand, the way `WiFiWatch` does it. `.github/workflows/release.yml` publishes the exe on a `v*` tag and creates the release `SoTF Editor v<Version>` with an empty body and the exe as its only file.

1. `git pull`, the owner edits the README on GitHub.
2. Retake `Screenshots/Overview.png` (Player), `Storage.png` (a storage selected), `Npcs.png` (a group selected) and `Map.png` from a copy of a real savegame, copied with `cp -rp` and `touch -r` on the folder so the app bar shows the real save time. Over CDP: `Emulation.setDeviceMetricsOverride` with `1600 x 845` at scale `1.6` gives LetsWatch's `2560 x 1352`.
3. Update the README for what shipped, bump `<Version>` in `SoTF.Editor.Desktop.csproj` (`1.1`, never `1.1.0`), publish, and launch the exe alone from an empty folder.
4. Commit, push, then `git tag v<Version>` and `git push origin v<Version>`. The tag must match `<Version>`, the workflow looks for `SoTF.Editor_v<tag>.exe`.
5. Once the new release is live, delete the previous one with its tag: `gh release delete v1.0 --yes --cleanup-tag`.

## Where Data Lives

| What | Where |
|---|---|
| Settings (`UserSettings`) | `%AppData%\SoTF.Editor\settings.json` |
| WebView2 profile | `%AppData%\SoTF.Editor\WebView2` |
| Logs | `Documents\SoTF.Editor\Logs` |
| Backups | `Documents\SoTF.Editor\Backups\<steam id>\<SinglePlayer or Multiplayer>\<save id>\<yyyy-MM-dd HH-mm-ss>` |
| Unlocks backups | `Documents\SoTF.Editor\Backups\<steam id>\<file name>\<yyyy-MM-dd HH-mm-ss>` |
| Real savegames | `%UserProfile%\AppData\LocalLow\Endnight\SonsOfTheForest\Saves\<steam id>\<mode>\<save id>` |
| The game's own backups | `<save id>_backup_<yyyyMMddHHmmss>.zip` beside the save folder, one per in-game save; the editor ignores them |

A Debug build uses `SoTF.Editor Debug` for all of these folders (`AppInfo.FolderName`), so a test run never mixes with the installed app.

## Saving

**Never edit a real savegame.** Copy the savegame with its parent folders into the scratchpad and point **Savegames → Change** at the copy.

- Every page view model has `Load(Savegame?)` and `Update(Savegame)`. `MainViewModel.SaveChanges` runs every `Update` in a fixed order (setup, state, weather, inventory, armour, followers, player, storage, structures); each marks the `SaveDataWrapper` it changed.
- `SavegameStore` writes only modified wrappers. `BackupManager.Create` first copies every file of the savegame and compares each copy byte for byte; if that fails, nothing is written. Loose files are written to a temp file and moved over; `SaveData.zip` is updated on a copy that then replaces it. The modified flags clear only after every file is written, so a failed save can be retried.
- A save with no edits says **No Changes** and writes nothing. A rewrite on a no-op save is a bug.
- `SaveDataWrapper` keeps each nested document as it was loaded and only counts a marked document that now differs (`JToken.DeepEquals`). So an edit undone, or a tool that finds nothing to change, writes nothing, and the close prompt only asks about real changes.
- Restore makes a backup of the current files first, then copies the chosen backup back and removes files the backup lacks (the game renames the `.name` file on every save). The app never deletes a backup. **Backups → Restore Backup** lists them newest first (`BackupManager.List`), with Newest and Oldest greyed on the right.
- Open, reload, save and restore run through `MainLayout.RunBusyAsync`: it shows the busy overlay and yields once, so the overlay paints before the blocking work.
- The Item Plater writes at once (`MainViewModel.SaveWrappers`), so it asks to discard unsaved edits and reloads before plating.
- Closing the window with unsaved edits asks first; Windows shutting down never waits for it.
- Anything that changes actors goes through `MainViewModel.ModifyActors`: it writes unsaved follower edits into the JSON first, runs the change, then reloads followers and NPCs.
- The game leaves out values that are `0` or `false`, so a missing field reads as its default, never as an error. The Game State and Weather tabs list every known field even when the save lacks it (a spring save has no `_currentSeason`), and `GenericSettingExtensions.MergeTo` adds a missing field only for a non-default value.
- The player's cave or bunker is the `player.areaMask` entry (`IntValue`); followers and NPCs carry a `GraphMask`. Moving a follower writes both its position and its graph mask.
- A storage keeps every slot the save holds, even past its definition: stone holders now hold two stacks, and dropping the extra one lost items on every save.
- A stored item writes its loaded `UniqueItems` back untouched (food timers, actor seeds); only a changed count or item builds new ones (`StoredItem.UniqueItemsToSave`). A food storage swaps a food module only for another food state (`IsEqualTo` on `CurrentState`); comparing whole records reset spoil timers on every save.
- The `Clothing` list holds the outfit plus other worn pieces such as the Rebreather. The outfit picker changes only the first outfit entry and keeps the rest, and no outfit listed means the starting Tactical Jacket (`495`). `PlayerPageViewModel.WornItemIds` feeds the map, so a worn Rebreather counts as owned.
- Tools only change the in-memory JSON and return a message for the snackbar; nothing reaches the disk until Save. Unlocks is the exception: it edits the profile file directly, after its own backup.
- Big actions ask first with one sentence of explanation: the tools menu, Fill All Storages, Apply To All Of Same Type, Remove All, Set All To Remove and Remove All Copies. Pages use `EditorPageBase.ConfirmAsync`.
- Teleport and spawning edit the save, so they work with the game closed.

## Text

- Editor text is `Resources/SharedResource.resx` (English) and `SharedResource.nl.resx` (Dutch); both hold the same keys. Pages use `Localizer`, services `AppText`.
- `AppText.Culture` is the one source of the language. The WPF dispatcher keeps the startup thread culture, so setting `CultureInfo` does nothing; `AppLocalizer` reads `AppText.Culture` instead. English is the default, the `LanguagePicker` in the app bar switches and saves it.
- Item, creature and structure names stay English in every language, in `GameNames.resx`, because the game shows them in English.
- Labels are short Title Case. Longer sentences (hints, warnings, explanations, snackbar messages) are sentence case, still without ending punctuation.
- Keys built at runtime (`Item12`, `ActorType9`, `StructureCategoryTreeShelter`) come only from `TextKeys`. A Debug start checks every one against the game data and asserts on any missing key, so a new item or enum value needs its key in both files.
- Numbers follow the Windows number format; dates are `yyyy-MM-dd`.

## Layout

Pages with lists (Storage, NPCs, Inventory, Structures, Map, the Game state tab) fill the window and end `--page-bottom` above it, through the `page-fill`, `fill`, `fill-column` and `fit` classes in `SoTF.Editor.css`. Never size a panel with a `%` or `vh` height; it misses the bottom margin. A `MudTabs` fills the page while the open tab's root carries `fill`; its other tabs scroll with the page.

The last table row has no bottom line. A table whose rows virtualize keeps its columns with `fixed-columns` and a `ColGroup`, or the columns shift while scrolling.

## Updates

`UpdateChecker` reads the `releases/latest` redirect. On start (with Check For Updates On Start on) and from **Help → Check For Updates**, a newer version opens `UpdateDialog`. Update Now asks to discard unsaved edits, `Updater.DownloadAsync` saves `SoTF.Editor_v<new>.exe` beside the running exe (through a `.download` file), `Updater.Launch` starts it with `--after-update <pid>`, and the window closes without asking (`MainWindow.ClosesWithoutAsking`). The new exe calls `Updater.FinishPreviousVersion` before the single-instance check: it waits up to 15 s for the old process and deletes the other `SoTF.Editor_v*.exe` beside it. A Debug build or a renamed exe (`Updater.CanInstall` false) opens the release page instead. Build with `-p:Version=0.9` and use Check For Updates to see the dialog.

## Looks Wrong, Is Not

- `WebResourceRequested` answers only `https://files.local/` (`LocalFiles`), and only for `.png` and `.jpg` files that exist. BlazorWebView routes its own requests through the same event; answering them breaks the page.
- `MainWindow` and WebView2 both use `#202020`, so the window never flashes white.
- The map points are plain `<img>` in one canvas and pan and zoom in JS (`window.sotfMap` in `AppShell.js`): a map shows thousands of them. `MapViewModel` rebuilds them whenever `MainViewModel.Revision` moves (a reload or an actor change), because old points hold JSON that is no longer saved. The JS blocks native image dragging and ends a drag on `pointercancel`, or the map follows the cursor after a press on a point.
- `PoiDetails` calls `OnMapChanged` after closing or removing, because a child's event only re-renders the child and the map page would keep the old overlay.
- `AppShell.js` blurs the focused field before Ctrl S, so a number typed but not yet committed is saved too.
- The static map points come from `lmachens/sons-of-the-forest-map` (MIT), credited in `THIRD-PARTY-NOTICES.txt`. Keep that file while `Pois.json` and `ItemPois.json` ship.
- Regrow Trees regrows the share asked for, so 30% regrows about 30% of the cut trees.
- Armour pieces keep the slot they were loaded in; only a new or clashing piece takes the next free slot (`PlayerArmourSystemModel.ArmorSlots`). Renumbering moved durabilities and made every save a change.
- `NamedIntDatas` compare as a set: the editor sorts them, the game does not.
- `GenericModule` compares its token with `JToken.DeepEquals`; a record compares a `JObject` by reference and made every storage look changed.
- The game setup merge writes each known setting as name, type and value only; settings the editor does not know stay untouched. Keys come from the game's own list in `global-metadata.dat`: Inventory Pause is `GameSetting.Survival.PausedInInventory`, Stat Regeneration Penalties is `GameSetting.Survival.StatRegenerationPenalty`. `SleepingDelay` and `InstantSleepSeasonSwitch` exist but their values are unknown, so the page leaves them out.
- Every Kelvin and Virginia in the save, spawned copies included, gets its own `FollowerState`; the panel picks one at a time and the merge writes each by unique id. A follower missing from the save is left alone.
- A MultiplayerClient save holds only the player: world pages show a hint and world tools are disabled.
- There is no Delete Backups action, on purpose.
- The update check reads the `releases/latest` redirect without following it, so it needs no token. A `v1.0` tag parses as `1.0`, which is lower than the assembly's `1.0.0.0`, so it never prompts falsely.
- The save holds no length per season. Only the custom game setting `SeasonLength` exists, and it applies to every season. The Weather tab's Season Day Offset (`_startingDayOffset`) shifts where the year stands, so lowering it keeps the current season longer. Tested in game: day 36 at offset 5 showed spring, offset 15 autumn. The season is `floor((days + offset) / days per season) mod 4` from Spring (`SeasonCalendar`: Default 5, Long 10, Realistic 90; Short is unknown, so it gets no picker). The Season row is computed, not saved: picking one moves the offset by whole seasons.
- `_startingDayOffset` is saved as `5.0`. It is a `Decimal` setting holding a `double`, so a no-op save writes the exact number back; an `int` made every save a change.
- Max Health is `100 + 2` per Strength Level above 1, so the page derives it and keeps the box read-only; in game, a save storing 100 at level 41 showed health at about half. `TargetHealth` is written only when Current Health changed. Moving the player clears `player.localToRaftUniqueId`, or the game puts them back on the raft (seen in game). Inventory Pause was checked in game too: with it on, the minimap markers stand still in the inventory.
- `PlayerState.CurrentHealth` reads capped by `MaxHealth` but keeps the value set, so lowering max and raising it again brings current health back. Both health sliders share one scale, so the current health thumb stays put while max moves.

## Verify

- `PlayerArmourSystemModel`, `GenericModule` and `Position` (every spawn pattern places enough finite points) carry `SelfTestPasses()`.
- After a change near saving, run a round trip on copies of the real savegames with a file-based app in the scratchpad: a no-op save leaves every file byte identical; an edit changes only what it should (compare after decoding the nested JSON strings); Restore Oldest brings every file back byte for byte.
- After a refactor, compare old and new behaviour: `git archive HEAD` into the scratchpad, run one harness (`#:project` plus `Microsoft.Extensions.DependencyInjection`, registered like `App`) against both Services projects on the same save copies, and diff what it writes: page state, every map point under each filter, and every decoded file after page edits, tools, actor edits and plating. Seeds and GUIDs are random, so normalize them first. The harness writes the Debug settings and backups, so it restores the settings file and removes only the backup folders it created.
- Before touching anything near the real saves, hash every file under `Saves` and keep a verified copy; compare the hashes afterwards. Only the game may change them.
- Look at the app over CDP: start it with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`, and `http://127.0.0.1:9333/json` lists the page. Check both languages.
- In the game, test with an edited copy in a new folder, `Saves\<steam id>\SinglePlayer\<new ten digit id>` with the id below 2147483647 (the game skips a larger one), edited with a file-based app on the Services; writes to `LocalLow` from a session land in the real folder: Single Player, Continue lists it with its `GameName` and day count, even when it came from a multiplayer save. Load it, check, and quit without saving. Tab opens the inventory, Escape the pause menu; the game runs borderless on the primary screen, so screen captures work. Delete the test folder while the game is still running, then exit: Steam Auto-Cloud uploads a new folder at exit and downloads it again if it was deleted while the game was closed.
- A Release exe run from another folder writes the real `%AppData%\SoTF.Editor` and `Documents\SoTF.Editor`; delete what a test run left there.
