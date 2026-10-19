# SoTF Editor

A savegame editor for Sons of the Forest. A WPF window hosts a `BlazorWebView` whose whole UI is MudBlazor, laid out like `WiFiWatch`. It reads a savegame folder, lets people edit it page by page, and writes back only what changed, after a verified copy of the whole savegame.

| Project | Holds |
|---|---|
| `SoTF.Editor.Data` (`net10.0`) | Savegame and game data models, view models, enums, helpers (`AppInfo`, `SaveJson`, `Formatter`, `Teleporter`, coordinates), and `Resources`: the resx text, `TextKeys`, `GameNames.resx`, the embedded `GameData.json`, `Pois.json`, `ItemPois.json` and `ActorTemplate.txt` |
| `SoTF.Editor.Services` (`net10.0`) | `Savegames` (finding, loading, saving, backups, `MainViewModel`), `Pages` (one view model per page), `Actors`, `Map`, `Tools`, `Settings`, `Integration` (update check and updater) |
| `SoTF.Editor.Desktop` | `App` (startup, logging, single instance, DI), `MainWindow` (WebView2 host), `Components`, `Theming/AppTheme`, `Interop` (`LocalFiles`, `WindowCaptionTheme`), `wwwroot` |
| `Tests` | `SaveRoundTrip.cs`, a file-based app, and `Sample`, the savegame it edits |

Packages: MudBlazor, `Microsoft.AspNetCore.Components.WebView.Wpf`, Newtonsoft.Json, NLog. No MVVM toolkit, no Bootstrap, no tray: the editor has no background work, so closing it exits.

## Build, Run And Publish

```bash
dotnet build SoTF.Editor.slnx
```

```bash
dotnet run --project SoTF.Editor.Desktop
```

```bash
dotnet run Tests/SaveRoundTrip.cs
```

```bash
dotnet publish SoTF.Editor.Desktop -c Release
```

The publish folder holds only `SoTF.Editor_v<Version>.exe`: `IncludeAllContentForSelfExtract` and the `BundleWebRoot` and `RemoveLooseWebRoot` targets pack `wwwroot` inside it.

### Releasing

A release is made by the workflow, not by hand, the way `WiFiWatch` does it. `.github/workflows/release.yml` runs `Tests/SaveRoundTrip.cs`, publishes the exe on a `v*` tag and creates the release `SoTF Editor v<Version>` with the exe as its only file. Unlike other projects the body is not empty: it lists the commit titles since the previous tag (`fetch-depth: 0`, `git describe` on `<tag>^`), and the update dialog shows them under What Changed. So commit titles are what users read.

1. `git pull`, the owner edits the README on GitHub.
2. Retake `Screenshots/Overview.png` (Player), `Storage.png` (a storage selected), `Npcs.png` (a group selected) and `Map.png` from a copy of a real savegame, copied with `cp -rp` and `touch -r` on the folder so the app bar shows the real save time. Over CDP: `Emulation.setDeviceMetricsOverride` with `1600 x 845` at scale `1.6` gives LetsWatch's `2560 x 1352`.
3. Update the README for what shipped, bump `<Version>` in `SoTF.Editor.Desktop.csproj` (`1.1`, never `1.1.0`), publish, and launch the exe alone from an empty folder.
4. Commit, push, then `git tag v<Version>` and `git push origin v<Version>`. The tag must match `<Version>`, the workflow looks for `SoTF.Editor_v<tag>.exe`.
5. Once the new release is live, delete the previous one with its tag: `gh release delete v1.0 --yes --cleanup-tag`. Not before: the new run needs the previous tag for its notes.

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
| Test sample | `Tests\Sample\76561190000000000\Multiplayer\1000000001`, a real save with a made-up Steam id |

A Debug build uses `SoTF.Editor Debug` for all of these folders (`AppInfo.FolderName`), so a test run never mixes with the installed app.

`Multiplayer` saves are games the player hosted. A `MultiplayerClient` save is a game they joined on someone else's server: it holds only their own player, the world lives in the host's save. The Savegames page explains this with an info button on that group.

## Saving

**Never edit a real savegame.** Copy the savegame with its parent folders into the scratchpad and point **Savegames → Change** at the copy.

- Every page view model has `Load(Savegame?)` and `Update(Savegame)`. `MainViewModel.SaveChanges` runs every `Update` in a fixed order (setup, state, weather, inventory, armour, followers, player, storage, structures); each marks the `SaveDataWrapper` it changed.
- `SavegameStore` writes only modified wrappers. `BackupManager.Create` first copies every file of the savegame and compares each copy byte for byte; if that fails, nothing is written. Loose files are written to a temp file and moved over; `SaveData.zip` is updated on a copy that then replaces it. The modified flags clear only after every file is written, so a failed save can be retried.
- A save with no edits says **No Changes** and writes nothing. A rewrite on a no-op save is a bug, and `Tests/SaveRoundTrip.cs` fails on it.
- `SaveDataWrapper` keeps each nested document as it was loaded and only counts a marked document that now differs (`JToken.DeepEquals`). So an edit undone, or a tool that finds nothing to change, writes nothing, and the close prompt only asks about real changes.
- Restore copies the current files to a safety backup first, then copies the chosen backup back and removes files the backup lacks (the game renames the `.name` file on every save). **Backups → Restore Backup** lists them newest first (`BackupManager.List`) with their size in MB, Newest and Oldest greyed on the right, and a delete button per row that asks first.
- Backups are capped at `BackupManager.SizeLimit` (500 MB, no picker). After each new backup, `Prune` deletes the oldest until the total fits, but every savegame keeps its very first backup and the one just made. `Pruned` raises a warning snackbar in `MainLayout`, and the restore dialog shows the usage. Restore prunes only after copying back, or it could delete the backup it is restoring. A folder that fails to delete is skipped, saving never fails over it. `BackupManager.Delete` only removes a dated folder inside the backups folder.
- Open, reload, save and restore run through `MainLayout.RunBusyAsync`: it shows the busy overlay and yields once, so the overlay paints before the blocking work.
- The Item Plater writes at once (`MainViewModel.SaveWrappers`), so it asks to discard unsaved edits and reloads before plating.
- Closing the window with unsaved edits asks first; Windows shutting down never waits for it.
- Anything that changes actors goes through `MainViewModel.ModifyActors`: it writes unsaved follower edits into the JSON first, runs the change, then reloads followers and NPCs.
- The game leaves out values that are `0` or `false`, so a missing field reads as its default, never as an error. The Game State and Weather tabs list every known field even when the save lacks it (a spring save has no `_currentSeason`), and `GenericSettingExtensions.MergeTo` adds a missing field only for a non-default value.
- The player's cave or bunker is the `player.areaMask` entry (`IntValue`); followers and NPCs carry a `GraphMask`. Moving a follower writes both its position and its graph mask.
- A storage keeps every slot the save holds, even past its definition: stone holders now hold two stacks, and dropping the extra one lost items on every save.
- A stored item writes its loaded `UniqueItems` back untouched (food timers, actor seeds); only a changed count or item builds new ones (`StoredItem.UniqueItemsToSave`). A food storage swaps a food module only for another food state (`IsEqualTo` on `CurrentState`); comparing whole records reset spoil timers on every save.
- The `Clothing` list holds the outfit plus other worn pieces such as the Rebreather. The outfit picker changes only the first outfit entry and keeps the rest, and no outfit listed means the starting Tactical Jacket (`495`). `PlayerPageViewModel.WornItemIds` feeds the map, so a worn Rebreather counts as owned.
- Tools only change the in-memory JSON and return a message for the snackbar; nothing reaches the disk until Save. The Item Plater and Unlocks are the exceptions and say so: the Plater saves at once, Unlocks edits the profile file directly after its own backup.
- Big actions ask first with one sentence of explanation: every tool, Fill All Storages, Apply To All Of Same Type, Remove All, Set All To Remove, Remove All Copies and Delete Backup. Pages use `EditorPageBase.ConfirmAsync`.
- Teleport and spawning edit the save, so they work with the game closed.

## Navigation

- The left menu holds every page, Tools included (`/Tools`). The app bar holds Save, Reload, Backups, Language and Help, nothing else.
- The Tools page lists each tool as a card with an icon, a one-sentence description and a chip: **Needs Save** (most), **Saves At Once** in amber (Item Plater, Unlocks), or **Host Save Only** greyed out for world tools on a client save. A hint at the top says tools need Save like any other edit.
- Help holds GitHub, an **Updates** submenu (Check For Updates, Check For Updates On Start, Download Latest Version) and About.

## Text

- Editor text is `Resources/SharedResource.resx` (English) and `SharedResource.nl.resx` (Dutch); both hold the same keys. Pages use `Localizer`, services `AppText`.
- `AppText.Culture` is the one source of the language. The WPF dispatcher keeps the startup thread culture, so setting `CultureInfo` does nothing; `AppLocalizer` reads `AppText.Culture` instead. English is the default, the `LanguagePicker` in the app bar switches and saves it.
- Item, creature and structure names stay English in every language, in `GameNames.resx`, because the game shows them in English.
- Labels are short Title Case. Longer sentences (hints, info, warnings, snackbar messages) are sentence case, short, and never end in a symbol; with two sentences only the last goes without a full stop.
- A field or section that needs explaining gets a blue `InfoButton` (`Icons.Material.Rounded.Info` in a tooltip), not a caption: `Header` takes `Info`, `StatSlider` takes `Info`, and the Game tab puts one beside every setup setting. Their keys are the label key plus `Info` (`EnemyHealthInfo`). Game State and Weather rows get theirs from `TextKeys.SettingInfo` for the names in `TextKeys.SettingsWithInfo`.
- Keys built at runtime (`Item12`, `ActorType9`, `StructureCategoryTreeShelter`, `SettingGameDaysInfo`) come only from `TextKeys`. A Debug start checks every one against the game data and asserts on any missing key, so a new item, enum value or info field needs its key in both files.
- Numbers follow the Windows number format; dates are `yyyy-MM-dd`; sizes are `Formatter.FormatSize` in MB.

## Layout

Pages with lists (Storage, NPCs, Inventory, Structures, Map, the Game state tab) fill the window and end `--page-bottom` above it, through the `page-fill`, `fill`, `fill-column` and `fit` classes in `SoTF.Editor.css`. Never size a panel with a `%` or `vh` height; it misses the bottom margin. A `MudTabs` fills the page while the open tab's root carries `fill`; its other tabs scroll with the page.

Lists and tables run edge to edge in their panel: the paper carries `panel-flush` (no side padding, clipped corners) and each row, group title or toolbar carries its own `px-4`. A new list follows that, never a padded paper around an inset list.

The last table row has no bottom line. A table whose rows virtualize keeps its columns with `fixed-columns` and a `ColGroup`, or the columns shift while scrolling.

- Inventory is one alphabetical list of every item. Held items show count, max and delete; the rest are greyed (`not-held`) with a green add button. A Held Only switch, a type filter and Add All From Category sit in the toolbar (`InventoryPageViewModel.Filter`).
- Map collections with sub groups (Actors, Items, Structures) start folded behind an expand button; their checkbox still covers every sub group.
- Max Health is derived, so it shows as a read-only bar (`StatSlider ReadOnly`), not a slider.
- The follower copy picker (Kelvin 1, Kelvin 2) is an outlined select at the top right inside the panel. NPC thumbs are round and cropped (`thumb-round`), so odd-sized images line up.
- Icons are `Icons.Material.Rounded.*`, the filled forms (`Folder`, `Help`, `Error`), never the outlined ones.
- A dialog closes with its X only: no Cancel or Later button beside it. `ConfirmDialog` keeps Yes and No and hides the X.

## Updates

`UpdateChecker.CheckAsync` reads the `releases/latest` redirect and returns the tag and version. On start (with Check For Updates On Start on) and from **Help → Updates → Check For Updates**, a newer version opens `UpdateDialog`, which loads the release body through the GitHub API (`AppInfo.ReleaseApiUrl`, no token) and lists it under What Changed; no notes just hide the list. Update Now asks to discard unsaved edits, `Updater.DownloadAsync` saves `SoTF.Editor_<tag>.exe` beside the running exe from the tag's asset URL (so `v1.5.1` works too), through a `.download` file, `Updater.Launch` starts it with `--after-update <pid>`, and the window closes without asking (`MainWindow.ClosesWithoutAsking`). The new exe calls `Updater.FinishPreviousVersion` before the single-instance check: it waits up to 15 s for the old process and keeps the newest older `SoTF.Editor_v*.exe` as a way back, deleting any older ones. A protected folder such as Program Files (`UnauthorizedAccessException`) says so and opens the release page. A Debug build or a renamed exe (`Updater.CanInstall` false) opens the release page instead. Build with `-p:Version=0.9` and use Check For Updates to see the dialog.

## Looks Wrong, Is Not

- `WebResourceRequested` answers only `https://files.local/` (`LocalFiles`), and only for `.png` and `.jpg` files that exist. BlazorWebView routes its own requests through the same event; answering them breaks the page.
- `MainWindow` and WebView2 both use `#202020`, so the window never flashes white.
- The map points are plain `<img>` in one canvas and pan and zoom in JS (`window.sotfMap` in `AppShell.js`): a map shows thousands of them. `MapViewModel` rebuilds them whenever `MainViewModel.Revision` moves (a reload or an actor change), because old points hold JSON that is no longer saved. The JS blocks native image dragging and ends a drag on `pointercancel`, or the map follows the cursor after a press on a point.
- `PoiDetails` calls `OnMapChanged` after closing or removing, because a child's event only re-renders the child and the map page would keep the old overlay.
- `AppShell.js` blurs the focused field before Ctrl S, so a number typed but not yet committed is saved too.
- `MudSelect` puts its `Class` and `Style` on an inner element, so placing one (the follower copy picker) takes a `MudElement` around it.
- The static map points come from `lmachens/sons-of-the-forest-map` (MIT), and much of the save handling started from SOTFEdit by codengine (MIT). Both are credited in `THIRD-PARTY-NOTICES.txt` with their licence text, which is all MIT asks; keep the file while either code or data ships. The editor itself is GPL-3.0.
- Regrow Trees regrows the share asked for, so 30% regrows about 30% of the cut trees.
- Armour pieces keep the slot they were loaded in; only a new or clashing piece takes the next free slot (`PlayerArmourSystemModel.ArmorSlots`). Renumbering moved durabilities and made every save a change.
- `NamedIntDatas` compare as a set: the editor sorts them, the game does not.
- `GenericModule` compares its token with `JToken.DeepEquals`; a record compares a `JObject` by reference and made every storage look changed.
- `SaveDataWrapper.SerializeAllModified` skips a key with no parsed node (`TryGetValue`): a token edited in place is already in the parent.
- The game setup merge writes each known setting as name, type and value only; settings the editor does not know stay untouched. Keys come from the game's own list in `global-metadata.dat`: Inventory Pause is `GameSetting.Survival.PausedInInventory`, Stat Regeneration Penalties is `GameSetting.Survival.StatRegenerationPenalty`. `SleepingDelay` and `InstantSleepSeasonSwitch` exist but their values are unknown, so the page leaves them out.
- Every Kelvin and Virginia in the save, spawned copies included, gets its own `FollowerState`; the panel picks one at a time and the merge writes each by unique id. A follower missing from the save is left alone. The game calls Kelvin `Robby` (`IsRobbyDead`).
- A MultiplayerClient save holds only the player: world pages show a hint and world tools are disabled.
- The update check reads the `releases/latest` redirect without following it, so it needs no token. A `v1.0` tag parses as `1.0`, which is lower than the assembly's `1.0.0.0`, so it never prompts falsely. The notes call is unauthenticated and rate limited; failing just hides them.
- The save holds no length per season. Only the custom game setting `SeasonLength` exists, and it applies to every season. The Weather tab's Season Day Offset (`_startingDayOffset`) shifts where the year stands, so lowering it keeps the current season longer. Tested in game: day 36 at offset 5 showed spring, offset 15 autumn. The season is `floor((days + offset) / days per season) mod 4` from Spring (`SeasonCalendar`: Default 5, Long 10, Realistic 90; Short is unknown, so it gets no picker). The Season row is computed, not saved: picking one moves the offset by whole seasons.
- `_startingDayOffset` is saved as `5.0`. It is a `Decimal` setting holding a `double`, so a no-op save writes the exact number back; an `int` made every save a change.
- Max Health is `100 + 2` per Strength Level above 1, so the page derives it and shows it read-only; in game, a save storing 100 at level 41 showed health at about half. `TargetHealth` is written only when Current Health changed. Moving the player clears `player.localToRaftUniqueId`, or the game puts them back on the raft (seen in game). Inventory Pause was checked in game too: with it on, the minimap markers stand still in the inventory.
- `PlayerState.CurrentHealth` reads capped by `MaxHealth` but keeps the value set, so lowering max and raising it again brings current health back. The health bars share one scale, so the current health thumb stays put while max moves.

## Verify

- `PlayerArmourSystemModel`, `GenericModule` and `Position` (every spawn pattern places enough finite points) carry `SelfTestPasses()`.
- `dotnet run Tests/SaveRoundTrip.cs` copies the sample to temp, checks that a save without edits reports No Changes and leaves every file identical, makes one edit per page (Player, Armour, Inventory, Storage, Followers, Game Setup, Game State, Weather, NPCs, Structures, Map teleport), saves, reloads, reads it back and checks nothing is left unsaved, then restores the oldest backup and compares every file byte for byte. It exits 1 on any failure and the release workflow runs it. It restores the Debug settings file and deletes only the sample's backup folder. A new page or edit path gets a line there.
- The sample is a real multiplayer save with `MultiplayerPlayerRolesSaveData.json` removed, because that file held friends' Steam ids and names. Never commit a save file that names real players.
- After a change near saving, also run a round trip on copies of the real savegames with a file-based app in the scratchpad: a no-op save leaves every file byte identical; an edit changes only what it should (compare after decoding the nested JSON strings); Restore Oldest brings every file back byte for byte.
- After a refactor, compare old and new behaviour: `git archive HEAD` into the scratchpad, run one harness (`#:project` plus `Microsoft.Extensions.DependencyInjection`, registered like `App`) against both Services projects on the same save copies, and diff what it writes: page state, every map point under each filter, and every decoded file after page edits, tools, actor edits and plating. Seeds and GUIDs are random, so normalize them first. The harness writes the Debug settings and backups, so it restores the settings file and removes only the backup folders it created.
- Before touching anything near the real saves, hash every file under `Saves` and keep a verified copy; compare the hashes afterwards. Only the game may change them.
- Look at the app over CDP: start it with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`, and `http://127.0.0.1:9333/json` lists the page. The menu items are links (`a[href="Tools"]`) once a savegame is open, and icon buttons carry an `aria-label`. Check both languages and set the language back afterwards.
- In the game, test with an edited copy in a new folder, `Saves\<steam id>\SinglePlayer\<new ten digit id>` with the id below 2147483647 (the game skips a larger one), edited with a file-based app on the Services; writes to `LocalLow` from a session land in the real folder: Single Player, Continue lists it with its `GameName` and day count, even when it came from a multiplayer save. Load it, check, and quit without saving. Tab opens the inventory, Escape the pause menu; the game runs borderless on the primary screen, so screen captures work. Delete the test folder while the game is still running, then exit: Steam Auto-Cloud uploads a new folder at exit and downloads it again if it was deleted while the game was closed.
- A Release exe run from another folder writes the real `%AppData%\SoTF.Editor` and `Documents\SoTF.Editor`; delete what a test run left there.
