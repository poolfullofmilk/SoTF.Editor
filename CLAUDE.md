# SoTF Editor

A savegame editor for Sons of the Forest. A WPF window hosts a `BlazorWebView` whose whole UI is MudBlazor, laid out like `WiFiWatch`. It reads a savegame folder, lets people edit it page by page, and writes back only what changed, after a verified copy of the whole savegame.

| Project | Holds |
|---|---|
| `SoTF.Editor.Data` (`net10.0`) | Savegame and game data models, view models, enums, helpers (`AppInfo`, `SaveJson`, `Formatter`, `LoadedValues`, `Teleporter`, coordinates), and `Resources`: the resx text, `TextKeys`, `GameNames.resx`, the embedded `GameData.json`, `Pois.json`, `ItemPois.json` and `ActorTemplate.txt` |
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
2. Bump `<Version>` in `SoTF.Editor.Desktop.csproj` first (`1.1`, never `1.1.0`), so the Debug build is not older than the latest release and shows no update dialog or badge. Then retake all ten screenshots from a copy of a real savegame, copied with `cp -rp` and `touch -r` on the folder so the app bar shows the real save time: `Overview.png` (Player), `Inventory.png`, `Storage.png` (Arrow Holder #1 selected), `Followers.png`, `Game.png` (setup tab), `Npcs.png` (Baby selected), `Structures.png`, `Map.png`, `Tools.png` and `Backups.png` (Restore Backup with a row selected). Over CDP: `Emulation.setDeviceMetricsOverride` with `1600 x 845` at scale `1.6` gives LetsWatch's `2560 x 1352`. The README shows Overview, then three rows of three.
3. Update the README for what shipped, publish, and launch the exe alone from an empty folder. The installed copy holds the single-instance handle, so close it first.
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
- Save first shows `SaveChangesDialog`: the pages with a menu dot, each with its menu icon, a note that a backup is made first, and a filled Save button (the X cancels). With no dot anywhere it saves straight away and the store decides, usually No Changes.
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
- Big actions ask first with one sentence of explanation: every tool, Fill All Storages, Apply To All Of Same Type, Remove All, Set All To Remove, Remove All Copies, Remove Copy and Delete Backup. Pages use `EditorPageBase.ConfirmAsync`.
- Teleport and spawning edit the save, so they work with the game closed.

## Navigation

- The left menu holds every page, Tools included (`/Tools`). On a client save the world pages (Storage, Followers, Game, NPCs, Structures) are disabled there too (`PageLink.NeedsWorld`).
- The app bar holds the savegame chip (its game name, no id and no tooltip; the Savegames cards keep the id), then Save, Reload, Backups and a grey X that closes the savegame after asking about unsaved edits. Language and Help sit on the right, nothing else.
- The Tools page lists each tool as a card with an icon, a one-sentence description and a chip: **Needs Save** (most), **Saves At Once** in amber (Item Plater, Unlocks), or **Host Save Only** greyed out for world tools on a client save. A hint at the top says tools need Save like any other edit.
- Help holds GitHub, an **Updates** submenu (Check For Updates, Download Latest Version) and About. When a newer version was found, a red dot leads the way until the app restarts, so a dismissed update dialog is not forgotten: a `MudBadge` on Help, then the `update-dot` class on the Updates submenu and Check For Updates, which draws the same dot after their text.
- The left menu shows an amber dot on every page with changes (`MainViewModel.HasChanges(route)`), at once: `EditorPageBase` implements `IHandleEvent` and calls `MainViewModel.NotifyChanged` after each of its own UI events, which re-renders the layout. Only events do this, so scrolling a list never re-renders the layout. Every component that edits values inherits it: the pages, `FollowerPanel` and `GenericSettingEditor`.
- Tools gets its dot from `MainViewModel.FinishTool`, which the Tools page calls after every tool with `ToolState` from before it: the tool's card gets a `ChangedBadge` when the unsaved files or game state values moved and real changes remain, and the menu dot follows the cards. Both clear on every load or save. Set To Endgame and Reset Containers light the Game page too.

## Change Dots

A small amber dot marks every value that differs from what was loaded from disk, and saving clears them all.

- Each page view model remembers its values when a save is loaded and answers `IsChanged(...)` per value and `HasChanges` for the page. Named values (player, followers, NPCs, game state and weather) go through `LoadedValues`; list entries that keep their object (armour pieces, inventory items, storages) are keyed by reference; structures need no memory, a set modification or new type is the change. `LoadedValues.Format` formats every value the same way, floats invariant, items and outfits by id, positions with their area.
- A dot sits on the field that changed, never on a picture or a title. `ChangedBadge` puts it on the top right corner of an input (`MudBadge` with `Color.Warning`); `ChangedDot` puts it at the top right of a text: menu items, switch and checkbox labels, list rows and names, before any info button. Every dot is MudBadge's 9 px; `.changed-dot` uses the same size and aligns to the top of the line (`vertical-align: top`, and `align-self: flex-start` inside the flex label of a menu button). `StatSlider`, `GenericSettingEditor` and `ItemLabel` take a `Changed` parameter; `ItemLabel` shows it after the name.
- Where the dot goes: armour on its Durability field, storage slots on the count field, structures on the Modification select (the Percent Done column shows the loaded value in green, an arrow and what saving writes in amber, `ScrewStructureWrapper.NewPctDone`), NPCs on the name in the list and the table, influences on each Sentiment, Anger and Fear field (`FollowerPageViewModel.IsChanged(follower, influence, property)`, remembered per influence type).
- Followers and NPCs reload after every actor change (`ModifyActors`), so they remember values only when the savegame object itself changes. A real load or a save creates a new one, an actor change does not.
- With no savegame open nothing has changed, so `MainViewModel.HasChanges` answers false before asking any page; a page that never loaded counts every value as new.
- A dot is computed by whoever renders it. An edit inside `GenericSettingEditor` or `FollowerPanel` re-renders the page through `NotifyChanged`, so a plain `Changed` bool from the page stays fresh. `InfluencesEditor` builds its sliders itself, so it takes `ChangedWhen` (influence and property), and `FollowerPanel` passes `OnChanged` so its own handler runs and the menu follows.
- Inventory marks the count, not the picture: the dot sits on the count field, and an item removed since the load shows it in the count column where its count was.
- Decimal fields show two decimals, so typing back the shown value (`39,35` for `39,3456`) is a real change and keeps its dot.
- Picking a season moves the offset the short way (`SeasonCalendar.OffsetFor`), so going back to the loaded season restores the loaded offset and its dot goes away.
- Tools that only touch the world JSON (fires, traps, containers, anger) have no field to mark.
- Removed entries (an armour piece, an influence) have nothing left to mark, so they show only in the menu dot and the save summary.
- To check the dots, sweep the app over CDP: per page change every input, select and switch, check its dot and the menu dot appear at once, put the value back and check both go. Filters, map layers, the copy picker and tool inputs get none. Count inside a virtualized table per row, not per page, and with no save open count the disabled menu buttons too, not just links.

## Text

- Editor text is `Resources/SharedResource.resx` (English) and `SharedResource.nl.resx` (Dutch); both hold the same keys. Pages use `Localizer`, services `AppText`.
- `AppText.Culture` is the one source of the language. The WPF dispatcher keeps the startup thread culture, so setting `CultureInfo` does nothing; `AppLocalizer` reads `AppText.Culture` instead. English is the default, the `LanguagePicker` in the app bar switches and saves it.
- Item, creature and structure names stay English in every language, in `GameNames.resx`, because the game shows them in English.
- Labels are short Title Case. Longer sentences (hints, info, warnings, snackbar messages) are sentence case, short, and never end in a symbol; with two sentences only the last goes without a full stop.
- A field or section that needs explaining gets a blue `InfoButton` (`Icons.Material.Rounded.Info` in a tooltip, with a soft wide drop shadow and a small left margin), not a caption: `Header` takes `Info`, `StatSlider` takes `Info`, and the Game tab puts one beside every setup setting. Beside a switch it sits inside the switch label, so it follows a wrapped line; a button inside a label never toggles the switch. Their keys are the label key plus `Info` (`EnemyHealthInfo`). Game State and Weather rows get theirs from `TextKeys.SettingInfo` for the names in `TextKeys.SettingsWithInfo`.
- Keys built at runtime (`Item12`, `ActorType9`, `StructureCategoryTreeShelter`, `SettingGameDaysInfo`) come only from `TextKeys`. A Debug start checks every one against the game data and asserts on any missing key, so a new item, enum value or info field needs its key in both files.
- Numbers follow the Windows number format; dates are `yyyy-MM-dd`; sizes are `Formatter.FormatSize` in MB.

## Layout

Pages with lists (Storage, NPCs, Inventory, Structures, Map, the Game state tab) fill the window and end `--page-bottom` above it, through the `page-fill`, `fill`, `fill-column` and `fit` classes in `SoTF.Editor.css`. Never size a panel with a `%` or `vh` height; it misses the bottom margin. A `MudTabs` fills the page while the open tab's root carries `fill`; its other tabs scroll with the page.

Lists and tables run edge to edge in their panel: the paper carries `panel-flush` (no side padding, clipped corners) and each row, group title or toolbar carries its own `px-4`. A new list follows that, never a padded paper around an inset list.

The last table row has no bottom line. A table whose rows virtualize keeps its columns with `fixed-columns` and a `ColGroup`, or the columns shift while scrolling.

- The Game tab's **Load Preset** menu, an icon menu left of Mode (Normal, Hard, Hard Survival, Peaceful, Creative), fills every setting with that mode's values and switches the mode to Custom, so each one stays editable (`GameSetupPageViewModel.ApplyPreset`, the same table `PrepareSettingsBeforeMerge` writes for a preset mode; Creative also turns on Creative Mode). It asks first.
- Inventory is one alphabetical list of every item, not virtualized: 377 rows render fast, and virtualizing rows of two heights made scrolling jump. Held items show count, max and delete; the rest are greyed (`not-held`) with a green add button. A Held Only switch, a type filter and Add All From Category sit in the toolbar (`InventoryPageViewModel.Filter`).
- Map collections with sub groups (Actors, Items, Structures) start folded behind an expand button; their checkbox still covers every sub group.
- With no saved choice the map shows `MapViewModel.s_defaultGroups` and every structure; **Reset** beside Deselect All brings that back. Both are outlined buttons, text buttons read as plain text. The zoom buttons step by 1.25 and a third button centres the whole map (`sotfMap.reset`).
- Structures lists in a `fit` panel with its filter inside, like World Object States, so a short filtered list shrinks the panel.
- World Object States rows have a clear button: a cleared value leaves `NamedIntDatas`, which the game reads as never touched, the same as Reset Containers.
- Storage's **Fill Fitting Slots** fills only the slots the picked item fits in.
- Max Health is derived, so it shows as a read-only bar (`StatSlider ReadOnly`), not a slider.
- The follower copy picker (Kelvin 1, Kelvin 2) is an outlined select at the top right inside the panel. NPC thumbs are round and cropped (`thumb-round`), so odd-sized images line up.
- Icons are `Icons.Material.Rounded.*`, the filled forms (`Folder`, `Help`, `Error`), never the outlined ones.
- Every `MudTextField`, `MudSelect` and `MudNumericField` is `Variant.Outlined`. MudBlazor 9 has no global default for it, so each one sets it. Toolbars and table cells add `Margin.Dense`. The one exception is the read-only value text in `GenericSettingEditor`, which is a label, not an input.
- Alerts (`MudAlert`) carry `Elevation="4"` like panels.
- A dialog closes with its X only: never a Cancel, Close, Later or OK button beside it. A button in the actions does something and says what (Apply, Remove, Remove Selected, Restore). `ConfirmDialog` keeps Yes and No and hides the X.
- The actor dialog's picture sits at the top of its row (`AlignItems.Start`); a stretched row made the square pictures tall.

## Updates

`UpdateChecker.CheckAsync` reads the `releases/latest` redirect and returns the tag and version. It runs on every start (there is no setting) and from **Help → Updates → Check For Updates**; a newer version opens `UpdateDialog` and sets the red dot on Help. The dialog loads the release body through the GitHub API (`AppInfo.ReleaseApiUrl`, no token) and lists it under What Changed; no notes just hide the list. Update Now asks to discard unsaved edits, `Updater.DownloadAsync` saves `SoTF.Editor_<tag>.exe` beside the running exe from the tag's asset URL (so `v1.5.1` works too), through a `.download` file, `Updater.Launch` starts it with `--after-update <pid>`, and the window closes without asking (`MainWindow.ClosesWithoutAsking`). The new exe calls `Updater.FinishPreviousVersion` before the single-instance check: it waits up to 15 s for the old process and deletes every other `SoTF.Editor_v*.exe` beside it. A protected folder such as Program Files (`UnauthorizedAccessException`) says so and opens the release page. A Debug build or a renamed exe (`Updater.CanInstall` false) opens the release page instead. Build with `-p:Version=0.9` and use Check For Updates to see the dialog.

## Looks Wrong, Is Not

- `WebResourceRequested` answers only `https://files.local/` (`LocalFiles`), and only for `.png` and `.jpg` files that exist. BlazorWebView routes its own requests through the same event; answering them breaks the page.
- `MainWindow` and WebView2 both use `#202020`, so the window never flashes white.
- WebView2 zoom and pinch zoom are off (`IsZoomControlEnabled`, `IsPinchZoomEnabled`): a touchpad pinch resized the whole app. The map still zooms with the wheel, it handles that itself.
- The map points are plain `<img>` in one canvas and pan and zoom in JS (`window.sotfMap` in `AppShell.js`): a map shows thousands of them. `MapViewModel` rebuilds them whenever `MainViewModel.Revision` moves (a reload or an actor change), because old points hold JSON that is no longer saved. The JS blocks native image dragging and ends a drag on `pointercancel`, or the map follows the cursor after a press on a point.
- Map points carry no drop shadow. A filter on every icon repainted them on each pan frame: with all 3,200 points panning ran at 30 fps, without it at 60. Promoting the canvas to its own layer while dragging made it worse. Zooming with every group on still restyles all icons (about 50 ms a step, `--map-zoom` in their transform); Chrome already skips that while panning, because the value does not change.
- The zoom buttons and the point details panel share the `map-overlay` glass; plain filled buttons were 12% grey and vanished over the map.
- `.mud-table-container` inherits the table's background. At 125% scaling Chrome repaints a transparent scroller on every frame and only composites an opaque one, so Inventory and Structures scrolled at 30 fps; the paper lists were already opaque.
- `PoiDetails` calls `OnMapChanged` after closing or removing, because a child's event only re-renders the child and the map page would keep the old overlay.
- `AppShell.js` blurs the focused field before Ctrl S, so a number typed but not yet committed is saved too.
- `MudSelect` puts its `Class` and `Style` on an inner element, so placing one (the follower copy picker) takes a `MudElement` around it.
- The update badge around the Help menu carries `d-flex`: `MudBadge` renders a block with the text line height, which lifted Help 2 px above the other app bar buttons.
- `panel-flush` sets `overflow: hidden` to clip its corners, which beats MudBlazor's `overflow-y-auto`; `.panel-flush.overflow-y-auto` brings scrolling back. Without it the NPC, Storage and Game State lists stopped scrolling in 1.6.
- Actor types 31, 52 (Eddy) and 100 have no picture in the game data, so `Actor.ImageUrl` falls back to the type's icon, the question mark.
- The static map points come from `lmachens/sons-of-the-forest-map` (MIT), and much of the save handling started from SOTFEdit by codengine (MIT). Both are credited in `THIRD-PARTY-NOTICES.txt` with their licence text, which is all MIT asks; keep the file while either code or data ships. The editor itself is GPL-3.0.
- Regrow Trees regrows the share asked for, so 30% regrows about 30% of the cut trees.
- Armour pieces keep the slot they were loaded in; only a new or clashing piece takes the next free slot (`PlayerArmourSystemModel.ArmorSlots`). Renumbering moved durabilities and made every save a change.
- `NamedIntDatas` compare as a set: the editor sorts them, the game does not.
- `GenericModule` compares its token with `JToken.DeepEquals`; a record compares a `JObject` by reference and made every storage look changed.
- `SaveDataWrapper.SerializeAllModified` skips a key with no parsed node (`TryGetValue`): a token edited in place is already in the parent.
- The setup merge drops a known setting the editor no longer holds (back at its default) and counts that as a change; before 2.0 it was dropped only when something else also changed.
- Group title rows in a hovered table (Structures) keep no hover: `.mud-table-row:has(.group-title)` overrides MudBlazor's row hover, because they are titles, not rows to click.
- The game setup merge writes each known setting as name, type and value only; settings the editor does not know stay untouched. Keys come from the game's own list in `global-metadata.dat`: Inventory Pause is `GameSetting.Survival.PausedInInventory`, Stat Regeneration Penalties is `GameSetting.Survival.StatRegenerationPenalty`. `SleepingDelay` and `InstantSleepSeasonSwitch` exist but their values are unknown, so the page leaves them out.
- Every Kelvin and Virginia in the save, spawned copies included, gets its own `FollowerState`; the panel picks one at a time and the merge writes each by unique id. Spawning selects the newest copy. Every copy after the first can be removed (`FollowerModifier.Remove`), which also drops its influence memory and NPC items; `FollowerPageViewModel.HasChanges` remembers the follower ids, so a spawned or removed copy lights the menu. The save holds no creation time for an actor, so the panel shows none. A follower missing from the save is left alone. The game calls Kelvin `Robby` (`IsRobbyDead`).
- A MultiplayerClient save holds only the player: world pages show a hint and world tools are disabled.
- The update check reads the `releases/latest` redirect without following it, so it needs no token. A `v1.0` tag parses as `1.0`, which is lower than the assembly's `1.0.0.0`, so it never prompts falsely. The notes call is unauthenticated and rate limited; failing just hides them.
- The save holds no length per season. Only the custom game setting `SeasonLength` exists, and it applies to every season. The Weather tab's Season Day Offset (`_startingDayOffset`) shifts where the year stands, so lowering it keeps the current season longer. Tested in game: day 36 at offset 5 showed spring, offset 15 autumn. The season is `floor((days + offset) / days per season) mod 4` from Spring (`SeasonCalendar`: Default 5, Long 10, Realistic 90; Short is unknown, so it gets no picker). The Season row is computed, not saved: picking one moves the offset by whole seasons, at most one back or two forward.
- `_startingDayOffset` is saved as `5.0`. It is a `Decimal` setting holding a `double`, so a no-op save writes the exact number back; an `int` made every save a change.
- Max Health is `100 + 2` per Strength Level above 1, so the page derives it and shows it read-only; in game, a save storing 100 at level 41 showed health at about half. `TargetHealth` is written only when Current Health changed. Moving the player clears `player.localToRaftUniqueId`, or the game puts them back on the raft (seen in game). Inventory Pause was checked in game too: with it on, the minimap markers stand still in the inventory.
- `PlayerState.CurrentHealth` reads capped by `MaxHealth` but keeps the value set, so lowering max and raising it again brings current health back. The health bars share one scale, so the current health thumb stays put while max moves.

## Verify

- `PlayerArmourSystemModel`, `GenericModule` and `Position` (every spawn pattern places enough finite points) carry `SelfTestPasses()`.
- `dotnet run Tests/SaveRoundTrip.cs` copies the sample to temp, checks that a fresh load shows no change dots and that a save without edits reports No Changes and leaves every file identical, makes one edit per page (Player, Armour, Inventory, Storage, Followers, a spawned and a removed Kelvin, Game Setup, Game State, Weather, NPCs, Structures, a Hard preset, Map teleport), checks the page shows as changed, saves, checks the dots are gone, reloads, reads it back and checks nothing is left unsaved, then restores the oldest backup and compares every file byte for byte. It exits 1 on any failure and the release workflow runs it. It restores the Debug settings file and deletes only the sample's backup folder. A new page or edit path gets a line there.
- The sample is a real multiplayer save with `MultiplayerPlayerRolesSaveData.json` removed, because that file held friends' Steam ids and names. Never commit a save file that names real players.
- After a change near saving, also run a round trip on copies of the real savegames with a file-based app in the scratchpad: a no-op save leaves every file byte identical; an edit changes only what it should (compare after decoding the nested JSON strings); Restore Oldest brings every file back byte for byte.
- After a refactor, compare old and new behaviour: `git archive HEAD` into the scratchpad, run one harness (`#:project` plus `Microsoft.Extensions.DependencyInjection`, registered like `App`) against both Services projects on the same save copies, and diff what it writes: page state, every map point under each filter, and every decoded file after page edits, tools, actor edits and plating. Seeds and GUIDs are random, so normalize them first. The harness writes the Debug settings and backups, so it restores the settings file and removes only the backup folders it created.
- Before touching anything near the real saves, hash every file under `Saves` and keep a verified copy; compare the hashes afterwards. Only the game may change them.
- Look at the app over CDP: start it with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`, and `http://127.0.0.1:9333/json` lists the page. The menu items are links (`a[href="Tools"]`) once a savegame is open, and icon buttons carry an `aria-label`. Check both languages and set the language back afterwards.
- In the game, test with an edited copy in a new folder, `Saves\<steam id>\SinglePlayer\<new ten digit id>` with the id below 2147483647 (the game skips a larger one), edited with a file-based app on the Services; writes to `LocalLow` from a session land in the real folder: Single Player, Continue lists it with its `GameName` and day count, even when it came from a multiplayer save. Load it, check, and quit without saving. Tab opens the inventory, Escape the pause menu; the game runs borderless on the primary screen, so screen captures work. Delete the test folder while the game is still running, then exit: Steam Auto-Cloud uploads a new folder at exit and downloads it again if it was deleted while the game was closed.
- A Release exe run from another folder writes the real `%AppData%\SoTF.Editor` and `Documents\SoTF.Editor`; delete what a test run left there.
