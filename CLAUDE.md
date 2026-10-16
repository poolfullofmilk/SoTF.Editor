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

The publish folder holds only `SoTF.Editor_v<Version>.exe`: `IncludeAllContentForSelfExtract` and the `BundleWebRoot` and `RemoveLooseWebRoot` targets pack `wwwroot` inside it. `.github/workflows/release.yml` publishes the same exe on a `v*` tag.

## Where Data Lives

| What | Where |
|---|---|
| Settings (`UserSettings`) | `%AppData%\SoTF.Editor\settings.json` |
| WebView2 profile | `%AppData%\SoTF.Editor\WebView2` |
| Logs | `Documents\SoTF.Editor\Logs` |
| Backups | `Documents\SoTF.Editor\Backups\<steam id>\<SinglePlayer or Multiplayer>\<save id>\<yyyy-MM-dd HH-mm-ss>` |
| Unlocks backups | `Documents\SoTF.Editor\Backups\<steam id>\<file name>\<yyyy-MM-dd HH-mm-ss>` |
| Real savegames | `%UserProfile%\AppData\LocalLow\Endnight\SonsOfTheForest\Saves\<steam id>\<mode>\<save id>` |

A Debug build uses `SoTF.Editor Debug` for all of these folders (`AppInfo.FolderName`), so a test run never mixes with the installed app.

## Saving

**Never edit a real savegame.** Copy the savegame with its parent folders into the scratchpad and point **Savegames → Change** at the copy.

- Every page view model has `Load(Savegame?)` and `Update(Savegame)`. `MainViewModel.SaveChanges` runs every `Update` in a fixed order (setup, state, weather, inventory, armour, followers, player, storage, structures); each marks the `SaveDataWrapper` it changed.
- `SavegameStore` writes only modified wrappers. `BackupManager.Create` first copies every file of the savegame and compares each copy byte for byte; if that fails, nothing is written. Loose files are written to a temp file and moved over; `SaveData.zip` is updated on a copy that then replaces it. The modified flags clear only after every file is written, so a failed save can be retried.
- A save with no edits says **No Changes** and writes nothing. A rewrite on a no-op save is a bug.
- Restore makes a backup of the current files first, then copies the chosen backup back and removes files the backup lacks (the game renames the `.name` file on every save). The app never deletes a backup.
- The Item Plater writes at once (`MainViewModel.SaveWrappers`), so it asks to discard unsaved edits and reloads before plating.
- Closing the window with unsaved edits asks first; Windows shutting down never waits for it.
- Anything that changes actors goes through `MainViewModel.ModifyActors`: it writes unsaved follower edits into the JSON first, runs the change, then reloads followers and NPCs.
- The game leaves out values that are `0` or `false`, so a missing field reads as its default, never as an error. The Game State and Weather tabs list every known field even when the save lacks it (a spring save has no `_currentSeason`), and `GenericSettingExtensions.MergeTo` adds a missing field only for a non-default value.
- The player's cave or bunker is the `player.areaMask` entry (`IntValue`); followers and NPCs carry a `GraphMask`. Moving a follower writes both its position and its graph mask.
- A storage keeps every slot the save holds, even past its definition: stone holders now hold two stacks, and dropping the extra one lost items on every save.
- A stored item writes its loaded `UniqueItems` back untouched (food timers, actor seeds); only a changed count or item builds new ones (`StoredItem.UniqueItemsToSave`).
- Tools only change the in-memory JSON and return a message for the snackbar; nothing reaches the disk until Save. Unlocks is the exception: it edits the profile file directly, after its own backup.
- Teleport and spawning edit the save, so they work with the game closed.

## Text

- Editor text is `Resources/SharedResource.resx` (English) and `SharedResource.nl.resx` (Dutch); both hold the same keys. Pages use `Localizer`, services `AppText`.
- `AppText.Culture` is the one source of the language. The WPF dispatcher keeps the startup thread culture, so setting `CultureInfo` does nothing; `AppLocalizer` reads `AppText.Culture` instead. English is the default, the `LanguagePicker` in the app bar switches and saves it.
- Item, creature and structure names stay English in every language, in `GameNames.resx`, because the game shows them in English.
- Keys built at runtime (`Item12`, `ActorType9`, `StructureCategoryTreeShelter`) come only from `TextKeys`. A Debug start checks every one against the game data and asserts on any missing key, so a new item or enum value needs its key in both files.
- Numbers follow the Windows number format; dates are `yyyy-MM-dd`.

## Layout

Pages with lists (Storage, NPCs, Inventory, Structures, Map) fill the window and end `--page-bottom` above it, through the `page-fill`, `fill`, `fill-column` and `fit` classes in `SoTF.Editor.css`. Never size a panel with a `%` or `vh` height; it misses the bottom margin.

## Looks Wrong, Is Not

- `WebResourceRequested` answers only `https://files.local/` (`LocalFiles`), and only for `.png` and `.jpg` files that exist. BlazorWebView routes its own requests through the same event; answering them breaks the page.
- `MainWindow` and WebView2 both use `#202020`, so the window never flashes white.
- The map points are plain `<img>` in one canvas and pan and zoom in JS (`window.sotfMap` in `AppShell.js`): a map shows thousands of them. `MapViewModel` rebuilds them whenever `MainViewModel.Revision` moves (a reload or an actor change), because old points hold JSON that is no longer saved.
- `AppShell.js` blurs the focused field before Ctrl S, so a number typed but not yet committed is saved too.
- The static map points come from `lmachens/sons-of-the-forest-map` (MIT), credited in `THIRD-PARTY-NOTICES.txt`. Keep that file while `Pois.json` and `ItemPois.json` ship.
- Regrow Trees regrows the share asked for, so 30% regrows about 30% of the cut trees.
- Armour pieces keep the slot they were loaded in; only a new or clashing piece takes the next free slot (`PlayerArmourSystemModel.ArmorSlots`). Renumbering moved durabilities and made every save a change.
- `NamedIntDatas` compare as a set: the editor sorts them, the game does not.
- `GenericModule` compares its token with `JToken.DeepEquals`; a record compares a `JObject` by reference and made every storage look changed.
- The game setup merge writes each known setting as name, type and value only; settings the editor does not know stay untouched.
- The follower merge writes only the follower that was loaded; a follower missing from the save is left alone.
- There is no Delete Backups action, on purpose.
- The update check reads the `releases/latest` redirect without following it, so it needs no token. A `v1.0` tag parses as `1.0`, which is lower than the assembly's `1.0.0.0`, so it never prompts falsely.
- The save holds no length per season. Only the custom game setting `SeasonLength` exists, and it applies to every season.

## Verify

- `PlayerArmourSystemModel`, `GenericModule` and `Position` (every spawn pattern places enough finite points) carry `SelfTestPasses()`.
- After a change near saving, run a round trip on copies of the real savegames with a file-based app in the scratchpad: a no-op save leaves every file byte identical; an edit changes only what it should (compare after decoding the nested JSON strings); Restore Oldest brings every file back byte for byte.
- Look at the app over CDP: start it with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`, and `http://127.0.0.1:9333/json` lists the page. Check both languages.
- A Release exe run from another folder writes the real `%AppData%\SoTF.Editor` and `Documents\SoTF.Editor`; delete what a test run left there.
