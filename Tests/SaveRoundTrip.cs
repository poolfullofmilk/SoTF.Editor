#:project ../SoTF.Editor.Services/SoTF.Editor.Services.csproj
#:package Microsoft.Extensions.DependencyInjection@10.0.14
#:property NoWarn=$(NoWarn);IL2026;IL3050;IL2072
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Integration;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;
using SoTF.Editor.Services.Tools;

// Every Write Lands In A Sandbox This Run Owns
const string OwnerFile = "SoTF.Editor.Tests.Owner.txt";
var testsRoot = Path.Combine(Path.GetTempPath(), "SoTF.Editor.Tests");
RemoveAbandonedRuns(testsRoot, OwnerFile);
var sandbox = Path.Combine(testsRoot, Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);
if (HasLinkedAncestor(sandbox))
{
    Console.WriteLine("Refused To Run, The Sandbox Sits Behind A Linked Folder");
    Directory.Delete(sandbox);
    return 2;
}

// Written Whole, A Half Marker Never Reads As Ended
File.WriteAllText(Path.Combine(sandbox, OwnerFile + ".new"), OwnerStamp(Environment.ProcessId));
File.Move(Path.Combine(sandbox, OwnerFile + ".new"), Path.Combine(sandbox, OwnerFile));
Environment.SetEnvironmentVariable("SOTF_EDITOR_ROOT", sandbox);
var ownerFolders = OwnerManifest();

// Refused Before Any Write When A Folder Lies Outside
string[] writableFolders =
[
    AppInfo.DataDirectory,
    AppInfo.DocumentsDirectory,
    AppInfo.ExportsDirectory,
    AppInfo.LoadoutsDirectory,
    BackupManager.BackupDirectory,
];
if (!writableFolders.All(folder => IsInside(folder, sandbox)))
{
    Console.WriteLine("Refused To Run, A Folder Lies Outside The Sandbox");
    Directory.Delete(sandbox, true);
    return 2;
}

// Explicit Nulls In Settings, Read Before Anything Else Loads Them
Directory.CreateDirectory(AppInfo.DataDirectory);
File.WriteAllText(
    Path.Combine(AppInfo.DataDirectory, "settings.json"),
    """{ "SavegamePath": null, "CopiedSavegames": null }"""
);
if (!IsInside(SavegameManager.GetSavePath(), sandbox))
{
    Console.WriteLine("Refused To Run, The Saves Folder Lies Outside The Sandbox");
    Directory.Delete(sandbox, true);
    return 2;
}

// The Sample Save, Copied So The Test Never Touches It
const string SampleSteamId = "76561190000000000";
var work = Path.Combine(sandbox, "Work");
CopyDirectory(Path.Combine(SourceDirectory(), "Sample"), work);
var savegamePath = Path.Combine(work, SampleSteamId, "Multiplayer", "1000000001");
var failures = new List<string>();
var deniedFolders = new List<string>();

try
{
    // Null Settings Read As Defaults, Kept Usable For Older Code
    var copiedSavegames = UserSettings.Current.CopiedSavegames;
    Check(
        NoThrow(() =>
            SavegameManager.IsCopy(
                new Savegame(savegamePath, "1000000001", new SavegameStore(savegamePath))
            )
        )
            && copiedSavegames != null,
        "Null Settings Read As Defaults"
    );
    UserSettings.Current.CopiedSavegames ??= [];

    var gameData = JsonConvert.DeserializeObject<GameData>(ResourceFile.Read("GameData.json"))!;
    var services = new ServiceCollection();
    services.AddSingleton(gameData);
    foreach (var type in EditorServices.Singletons)
    {
        services.AddSingleton(type);
    }
    using var provider = services.BuildServiceProvider();
    var main = provider.GetRequiredService<MainViewModel>();
    var player = provider.GetRequiredService<PlayerPageViewModel>();
    var armor = provider.GetRequiredService<ArmorPageViewModel>();
    var inventory = provider.GetRequiredService<InventoryPageViewModel>();
    var storage = provider.GetRequiredService<StoragePageViewModel>();
    var followers = provider.GetRequiredService<FollowerPageViewModel>();
    var setup = provider.GetRequiredService<GameSetupPageViewModel>();
    var gameState = provider.GetRequiredService<GameStatePageViewModel>();
    var weather = provider.GetRequiredService<WeatherPageViewModel>();
    var npcs = provider.GetRequiredService<NpcsPageViewModel>();
    var structures = provider.GetRequiredService<StructuresPageViewModel>();
    var actorModifier = provider.GetRequiredService<ActorModifier>();
    var map = provider.GetRequiredService<MapViewModel>();

    // Nothing Open Shows No Changes, Before And After A Load
    string[] pageRoutes =
    [
        "Player",
        "Inventory",
        "Storage",
        "Followers",
        "Game",
        "Npcs",
        "Structures",
    ];
    Check(pageRoutes.All(route => !main.HasChanges(route)), "No Savegame Shows No Changes");

    var pristine = HashFiles(savegamePath);
    main.SelectSavegame(new Savegame(savegamePath, "1000000001", new SavegameStore(savegamePath)));

    // A Save Without Edits Writes Nothing And Shows No Changes
    Check(pageRoutes.All(route => !main.HasChanges(route)), "Fresh Load Shows No Changes");
    Check(!main.SaveChanges(), "No Edits Report No Changes");
    Check(SameHashes(pristine, HashFiles(savegamePath)), "No Edits Leave Every File Identical");

    // One Edit Per Page, Each Saved, Reloaded And Read Back
    EditAndCheck(
        "Player",
        "Player",
        () => player.PlayerState.Fullness = 42,
        () => Math.Abs(player.PlayerState.Fullness - 42) < 0.01
    );

    EditAndCheck(
        "Armour",
        "Player",
        armor.SetAllToMax,
        () =>
            armor.Armour.All(piece =>
                piece.ArmourPiece.RemainingArmourpoints == piece.MaxDurability
            )
    );

    var added = inventory.Filter(null, null, false).First(item => !inventory.IsHeld(item));
    EditAndCheck(
        "Inventory",
        "Inventory",
        () => inventory.AddUnassignedItem(added),
        () => inventory.Inventory.Any(item => item.Id == added.Id)
    );

    var (collectionIndex, storageIndex) = FirstFillableStorage(storage);
    var fillItemId = 0;
    EditAndCheck(
        "Storage",
        "Storage",
        () =>
        {
            var target = storage.StorageCollections[collectionIndex].Storages[storageIndex];
            fillItemId = target.SupportedItems[0].Item.Id;
            StoragePageViewModel.FillAll(target, target.SupportedItems[0], 1);
        },
        () =>
            StoragePageViewModel
                .StoredItemsOf(storage.StorageCollections[collectionIndex].Storages[storageIndex])
                .Any(item => item.SelectedItem?.Item.Id == fillItemId)
    );

    EditAndCheck(
        "Followers",
        "Followers",
        () => followers.KelvinState.Health = 77,
        () => Math.Abs(followers.KelvinState.Health - 77) < 0.01
    );

    var kelvinCount = followers.CopiesOf(Constants.Actors.KelvinTypeId).Count;
    EditAndCheck(
        "Follower Spawn",
        "Followers",
        () => main.SpawnFollower(Constants.Actors.KelvinTypeId),
        () => followers.CopiesOf(Constants.Actors.KelvinTypeId).Count == kelvinCount + 1
    );

    EditAndCheck(
        "Follower Remove",
        "Followers",
        () => main.RemoveFollower(followers.CopiesOf(Constants.Actors.KelvinTypeId)[^1]),
        () => followers.CopiesOf(Constants.Actors.KelvinTypeId).Count == kelvinCount
    );

    // A Follower The Game Deleted Comes Back Through Revive
    EditAndCheck(
        "Follower Revive",
        "Followers",
        () =>
        {
            main.RemoveFollower(followers.CopiesOf(Constants.Actors.VirginiaTypeId)[0]);
            main.ReviveFollower(Constants.Actors.VirginiaTypeId);
        },
        () =>
            followers.CopiesOf(Constants.Actors.VirginiaTypeId)
                is [{ State: Constants.Actors.StateAlive }]
    );

    // Underground, New Followers Appear Beside One On The Surface
    var surfacePosition = player.PlayerState.Pos;
    player.PlayerState.Pos = surfacePosition.WithoutOffset(
        gameData.AreaManager.GetAreaForGraphMask(16)
    );
    Check(
        followers
            .CopiesOf(Constants.Actors.KelvinTypeId)
            .Any(kelvin => ReferenceEquals(kelvin.Pos, main.ArrivalPosition())),
        "Underground Arrival Is Beside A Follower"
    );
    player.PlayerState.Pos = surfacePosition;

    EditAndCheck(
        "Game Setup",
        "Game",
        () => setup.SetString(Constants.GameSetupKeys.EnemyHealth, "High", "NORMAL"),
        () => setup.GetString(Constants.GameSetupKeys.EnemyHealth, "") == "High"
    );

    var days = SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue ?? 0;
    EditAndCheck(
        "Game State",
        "Game",
        () => SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue = days + 1,
        () => SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue == days + 1
    );

    var offset = SettingOf(weather.Settings, "_startingDayOffset").DecimalValue ?? 0;
    EditAndCheck(
        "Weather",
        "Game",
        () => SettingOf(weather.Settings, "_startingDayOffset").DecimalValue = offset + 5,
        () =>
            Math.Abs(
                (SettingOf(weather.Settings, "_startingDayOffset").DecimalValue ?? 0) - offset - 5
            ) < 0.001
    );

    var actor = npcs.AllActors.First(candidate =>
        candidate.ActorType?.IsFollower() != true && candidate.Stats?.ContainsKey("Health") == true
    );
    var actorId = actor.UniqueId;
    EditAndCheck(
        "NPCs",
        "Npcs",
        () =>
        {
            var edit = new EditActorViewModel(actor, gameData.ActorTypes) { ActorSelection = 0 };
            edit.ModifyOptions.ActorHealth = 33;
            main.ModifyActors(save => actorModifier.Modify(save, edit));
        },
        () =>
            npcs.AllActors.First(candidate => candidate.UniqueId == actorId).Stats?["Health"] == 33
    );

    // Cannibal Anger Reaches Every Living Cannibal And The NPCs Page
    EditAndCheck(
        "Cannibal Anger",
        "Npcs",
        () => main.SetCannibalAnger(70),
        () =>
            main.ReadCannibalMood() is { AllKnowPlayer: true, Average: 70 } mood
            && !mood.Changes(70)
            && mood.Changes(0)
            && npcs.AllActors.Where(npc => npc.ActorType?.IsCannibal == true)
                .SelectMany(npc => npc.Influences ?? [])
                .Where(influence => influence.TypeId == Influence.Type.Player)
                .All(influence => influence.Anger == 70 && influence.Sentiment == -70)
    );

    // Calm Lowers That Anger Again And Forgets Kills
    EditAndCheck(
        "Cannibal Calm",
        "Npcs",
        () => main.SetCannibalAnger(0),
        () =>
            main.ReadCannibalMood()
                is { AllKnowPlayer: true, Average: 0, HasWorldCounts: false } mood
            && !mood.Changes(0)
    );

    var structureCount = structures.Structures.Count;
    EditAndCheck(
        "Structures",
        "Structures",
        () =>
            structures
                .Structures.First(wrapper =>
                    wrapper.ModificationModes.Contains(ScrewStructureModificationMode.Remove)
                )
                .ModificationMode = ScrewStructureModificationMode.Remove,
        () => structures.Structures.Count == structureCount - 1
    );

    EditAndCheck(
        "Game Preset",
        "Game",
        () => setup.ApplyPreset(GameSetupPageViewModel.HardGameMode),
        () =>
            setup.SelectedMode == GameSetupPageViewModel.CustomGameMode
            && setup.GetString(Constants.GameSetupKeys.AnimalSpawnRate, "")
                == GameSetupPageViewModel.SettingValueLOW
    );

    var target = new Position(-900, 120, 850) { Area = player.PlayerState.Pos.Area };
    EditAndCheck(
        "Map Teleport",
        "Player",
        () => player.PlayerState.Pos = target,
        () => player.PlayerState.Pos.HasSameCoordinates(target)
    );

    // A Removed Zipline Lights The Map And Saves
    map.Build();
    var ziplineCount = map.AllPois().OfType<ZiplinePoi>().Count();
    EditAndCheck(
        "Zipline",
        "Map",
        () => map.RemoveZipline(map.AllPois().OfType<ZiplinePoi>().First()),
        () =>
        {
            map.Build();
            return map.AllPois().OfType<ZiplinePoi>().Count() == ziplineCount - 1;
        }
    );

    // A Setting Switched And Switched Back Leaves Nothing To Save
    var paused = setup.GetBool(Constants.GameSetupKeys.PausedInInventory, false);
    setup.SetBool(Constants.GameSetupKeys.PausedInInventory, !paused, false);
    setup.SetBool(Constants.GameSetupKeys.PausedInInventory, paused, false);
    Check(!main.HasChanges("Game") && !main.SaveChanges(), "Setting Switched Back Saves Nothing");

    // Edits On Several Pages And A Tool Save Together
    EditAndCheck(
        "Mixed",
        "Player",
        () =>
        {
            player.PlayerState.Fullness = 55;
            followers.KelvinState.Health = 66;
            setup.SetString(Constants.GameSetupKeys.EnemyDamage, "LOW", "NORMAL");
            main.SetCannibalAnger(20);
        },
        () =>
            Math.Abs(player.PlayerState.Fullness - 55) < 0.01
            && Math.Abs(followers.KelvinState.Health - 66) < 0.01
            && setup.GetString(Constants.GameSetupKeys.EnemyDamage, "") == "LOW"
    );

    // Every Tool Runs Once, Saves, And Leaves Nothing Unsaved
    ToolCheck("Regrow Trees", () => new RegrowTreesViewModel(main.SelectedSavegame!).Save());
    ToolCheck("Eternal Fires", () => WorldTools.EternalFires(main.SelectedSavegame));
    Check(
        WorldTools.EternalFires(main.SelectedSavegame) == AppText.Get("NothingToChange"),
        "Eternal Fires Change No More"
    );
    ToolCheck("Reset Fires", () => WorldTools.ResetFires(main.SelectedSavegame));
    ToolCheck(
        "Reset Structure Damage",
        () => WorldTools.ResetStructureDamage(main.SelectedSavegame)
    );
    Check(
        WorldTools.ResetStructureDamage(main.SelectedSavegame) == AppText.Get("NoStructureDamage"),
        "Repaired Structures Repair No More"
    );
    ToolCheck("Reset Traps", () => WorldTools.ResetTraps(main.SelectedSavegame));
    ToolCheck("Ripen Gardens", () => WorldTools.RipenGardens(main.SelectedSavegame));
    Check(
        WorldTools.RipenGardens(main.SelectedSavegame) == AppText.Get("NothingGrowing"),
        "Ripe Gardens Ripen No More"
    );
    ToolCheck("Fill Printers", () => WorldTools.FillPrinters(main.SelectedSavegame));
    const SavegameStore.FileType printer = SavegameStore.FileType.Resin3dPrinterBunkerASaveData;
    Check(
        main.SelectedSavegame!.SavegameStore.LoadJsonRaw(printer)
            ?.GetJsonBasedToken(WorldTools.PrinterKeyOf(printer))
            ?["CurrentResinVolume"]?.Value<float>() == WorldTools.PrinterResin
            && WorldTools.FillPrinters(main.SelectedSavegame) == AppText.Get("PrintersFull"),
        "Printers Hold A Sled's Resin"
    );
    ToolCheck("Calm Cannibals", () => main.SetCannibalAnger(0));
    ToolCheck("Cannibal Anger To Half", () => main.SetCannibalAnger(50));
    Check(
        main.SetCannibalAnger(50) == AppText.Get("NothingToChange") && !main.HasUnsavedChanges(),
        "Same Cannibal Anger Changes Nothing"
    );
    ToolCheck(
        "Set To Endgame",
        () =>
        {
            gameState.SetToEndgame();
            return "";
        }
    );
    Check(!gameState.SetToEndgame(), "Endgame Twice Changes Nothing");
    ToolCheck(
        "World Item Cloner",
        () =>
            new WorldItemClonerViewModel(main.SelectedSavegame!)
            {
                SelectedWorldItemType = WorldItemType.Radio,
            }.CloneObjectAtPlayerPos(player.PlayerState.Pos)
    );
    ToolCheck(
        "Consumed Items",
        () =>
        {
            new ModifyConsumedItemsViewModel(main.SelectedSavegame!, gameData).RemoveAll();
            return "";
        }
    );

    // The Plater Saves At Once, Then Nothing Is Left
    var plating = new ItemPlaterViewModel(gameData).AddPlating(main.SelectedSavegame!);
    main.SaveWrappers(plating.WrappersToSave);
    Check(
        main.SelectedSavegame != null && !main.SaveChanges(),
        "Item Plater Leaves Nothing Unsaved"
    );

    // Unlocks Write The Profile After Its Own Backup
    var profilePath = Path.Combine(work, SampleSteamId, "PlayerProfile.json");
    File.WriteAllText(profilePath, """{ "_settings": [] }""");
    var unlocks = new UnlocksViewModel(profilePath);
    unlocks.Values["Stats.CreativeModeUnlocked"] = true;
    unlocks.Save();
    Check(
        new UnlocksViewModel(profilePath).Values["Stats.CreativeModeUnlocked"],
        "Unlocks Read Back"
    );

    // Revert Takes One Page Back, Other Pages Keep Their Edits
    var fullness = player.PlayerState.Fullness;
    player.PlayerState.Fullness = fullness == 11 ? 12 : 11;
    followers.KelvinState.Health = 22;
    Check(main.HasUnsavedChanges(), "Unsaved Edit Is Seen");
    main.RevertPage("Player");
    Check(
        !main.HasChanges("Player") && Math.Abs(player.PlayerState.Fullness - fullness) < 0.01,
        "Reverted Player Is Back"
    );
    Check(main.HasChanges("Followers"), "Revert Keeps Other Pages");
    var revertStorage = storage
        .StorageCollections.SelectMany(collection => collection.Storages)
        .First(candidate => StoragePageViewModel.HasStoredItems(candidate));
    storage.SelectedStorage = revertStorage;
    StoragePageViewModel.ClearAll(revertStorage);
    Check(main.HasUnsavedChanges(), "Cleared Storage Is Seen");
    main.RevertPage("Storage");
    Check(
        !main.HasChanges("Storage")
            && storage.SelectedStorage != null
            && StoragePageViewModel.HasStoredItems(storage.SelectedStorage),
        "Reverted Storage Keeps Its Selection"
    );

    // A Food Slot In Another State Counts As Changed
    var foodSlot = storage
        .StorageCollections.SelectMany(collection => collection.Storages)
        .OfType<FoodStorage>()
        .SelectMany(StoragePageViewModel.StoredItemsOf)
        .First(storedItem => storedItem.SelectedItem?.FoodSpoilStorageModuleWrapper != null);
    var food = foodSlot.SelectedItem!;
    foodSlot.SelectedItem = foodSlot.SupportedItems.First(candidate =>
        candidate.Item.Id == food.Item.Id && candidate != food
    );
    Check(foodSlot.Count == 1 && main.HasChanges("Storage"), "Food State Shows As Changed");
    main.RevertPage("Storage");
    Check(main.SaveChanges() && !main.SaveChanges(), "Edits Left After Revert Save");

    // Followers And NPCs Revert Apart, Each Keeps The Other's Edit
    var copiesBefore = followers.CopiesOf(Constants.Actors.KelvinTypeId).Count;
    var removedNpc = npcs.AllActors.First(candidate => candidate.ActorType?.IsFollower() != true);
    main.SpawnFollower(Constants.Actors.KelvinTypeId);

    // Kelvin's Own Dialog Reaches Every Kelvin, The Filters Guard Others
    var kelvins = npcs
        .AllActors.Where(candidate => candidate.TypeId == Constants.Actors.KelvinTypeId)
        .ToList();
    var kelvinEdit = new EditActorViewModel(kelvins[0], gameData.ActorTypes) { ActorSelection = 2 };
    var strangerEdit = new EditActorViewModel(removedNpc, gameData.ActorTypes)
    {
        ActorSelection = 3,
        OnlyInSameAreaAsActor = false,
    };
    Check(
        kelvins.Count > 1
            && kelvins.All(kelvin =>
                kelvinEdit.Matches(
                    kelvin.TypeId,
                    kelvin.FamilyId,
                    kelvin.GraphMask,
                    kelvin.UniqueId
                )
                && !strangerEdit.Matches(
                    kelvin.TypeId,
                    kelvin.FamilyId,
                    kelvin.GraphMask,
                    kelvin.UniqueId
                )
            ),
        "Kelvin Reaches Every Copy, Others Leave Him Alone"
    );
    main.ModifyActors(save =>
        actorModifier.Modify(
            save,
            new EditActorViewModel(removedNpc, gameData.ActorTypes)
            {
                ModificationMode = ActorModificationMode.Remove,
            }
        )
    );
    Check(main.HasChanges("Followers") && main.HasChanges("Npcs"), "Spawn And Removal Are Seen");
    main.RevertPage("Followers");
    Check(
        !main.HasChanges("Followers")
            && followers.CopiesOf(Constants.Actors.KelvinTypeId).Count == copiesBefore
            && main.HasChanges("Npcs"),
        "Reverted Followers Keep NPC Edits"
    );
    main.RevertPage("Npcs");
    Check(
        !main.HasUnsavedChanges()
            && npcs.AllActors.Any(candidate => candidate.UniqueId == removedNpc.UniqueId),
        "Reverted NPCs Leave Nothing Unsaved"
    );

    // A Removed Zipline And Tools Revert Too
    map.Build();
    map.RemoveZipline(map.AllPois().OfType<ZiplinePoi>().First());
    Check(main.HasChanges("Map"), "Removed Zipline Is Seen");
    main.RevertPage("Map");
    Check(!main.HasUnsavedChanges(), "Reverted Map Leaves Nothing Unsaved");

    // Near Player Leaves Only Points Within Reach
    map.Build();
    map.SetAllEnabled(true);
    map.MapFilter.NearPlayerMetres = 250;
    map.ApplyFilterToAllPois();
    var standing = player.PlayerState.Pos;
    Check(
        map.VisiblePois().OfType<PlayerPoi>().Any()
            && map.VisiblePois()
                .OfType<BasePoi>()
                .All(poi => new Position(poi.X, 0, poi.Z).GroundDistanceTo(standing) <= 250)
            && map.VisiblePois().Count() < map.AllPois().Count(),
        "Near Player Shows Only Close Points"
    );
    map.MapFilter.Reset();
    map.ResetToDefault();
    map.ApplyFilterToAllPois();

    // A Clicked Spot Takes A Moved Anchor And New Zipline
    map.Build();
    var anchor = map.AllPois().OfType<ZipPointPoi>().First();
    var anchorX = anchor.X;
    var ziplines = map.AllPois().OfType<ZiplinePoi>().Count();
    var (spotLeft, spotTop) = CoordinateConverter.IngameToPixel(anchor.X + 40, anchor.Z + 40);
    map.TogglePick(anchor, false);
    var partner = map.PickLineStart!;
    var (partnerLeft, partnerTop) = CoordinateConverter.IngameToPixel(partner.X, partner.Z);
    map.Click(partnerLeft, partnerTop, 1, true);
    Check(
        map.PickFrom == anchor && Math.Abs(anchor.X - anchorX) < 0.01,
        "Anchor On Its Partner Keeps Waiting"
    );
    map.Click(spotLeft, spotTop, 1, true);
    Check(
        main.HasChanges("Map") && Math.Abs(anchor.X - anchorX - 40) < 0.5,
        "Anchor Moves To A Clicked Spot"
    );
    map.Click(spotLeft + 30, spotTop + 30, 1, true);
    if (map.SelectedPoi is LocationPoi spot)
    {
        map.TogglePick(spot, true);
        map.Click(spotLeft + 60, spotTop + 60, 1, true);
    }

    Check(
        map.AllPois().OfType<ZiplinePoi>().Count() == ziplines + 1,
        "New Zipline From A Clicked Spot"
    );
    main.RevertPage("Map");
    Check(!main.HasUnsavedChanges(), "Reverted Zipline Picks Leave Nothing Unsaved");

    // Ziplines Count Lines, A Line Hides With Its Anchors
    map.Build();
    var ziplineGroup = map.AllGroups().First(group => group.GroupType == PoiGroupType.ZipLines);
    Check(
        ziplineGroup.Count == map.AllPois().OfType<ZiplinePoi>().Count(),
        "Ziplines Group Counts Lines"
    );
    ziplineGroup.Enabled = true;
    map.MapFilter.FullText = "No Point Has This Name";
    map.ApplyFilterToAllPois();
    Check(!map.VisibleZiplines().Any(), "A Line Hides With Its Anchors");
    map.MapFilter.FullText = null;
    map.ApplyFilterToAllPois();

    // Show On Map At A Zipline's Middle Picks That Zipline
    var line = map.AllPois().OfType<ZiplinePoi>().First();
    map.FocusPosition = new Position(
        (line.PointA.Position!.X + line.PointB.Position!.X) / 2,
        (line.PointA.Position.Y + line.PointB.Position.Y) / 2,
        (line.PointA.Position.Z + line.PointB.Position.Z) / 2
    );
    Check(map.TakeFocus() == line.PointA, "Show on Map Finds A Zipline");

    // A Visit Keeps Its Points, Leaving Ends A Pick
    map.Open();
    var firstPoint = map.AllPois().First();
    map.TogglePick(map.AllPois().OfType<ZipPointPoi>().First(), false);
    map.Close();
    map.Open();
    Check(
        ReferenceEquals(firstPoint, map.AllPois().First()) && map.PickFrom == null,
        "Map Keeps Its Points And Ends A Pick"
    );

    // A Save's First Zipline Makes Its Group, Shown
    foreach (var zipline in map.AllPois().OfType<ZiplinePoi>().ToList())
    {
        map.RemoveZipline(zipline);
    }

    map.Build();
    var (emptyLeft, emptyTop) = CoordinateConverter.IngameToPixel(0, 0);
    map.Click(emptyLeft, emptyTop, 1, true);
    if (map.SelectedPoi is LocationPoi start)
    {
        map.TogglePick(start, true);
        map.Click(emptyLeft + 40, emptyTop + 40, 1, true);
    }

    Check(
        map.AllGroups().Any(group => group.GroupType == PoiGroupType.ZipLines && group.Enabled)
            && map.VisibleZiplines().Count() == 1,
        "First Zipline Makes Its Group, Shown"
    );
    main.RevertPage("Map");
    Check(!main.HasUnsavedChanges(), "Reverted Zipline Group Leaves Nothing Unsaved");

    // An Unknown Build Cost Never Reads As Done
    var platform = new ScrewStructureWrapper(
        gameData.ScrewStructures.First(structure => structure.Id == 98),
        new JObject(),
        16,
        null,
        ScrewStructureOrigin.Unfinished,
        0
    );
    Check(
        platform.PctDone == -1 && platform.ModificationModes.Count == 1,
        "Unknown Cost Is Not Done"
    );
    foreach (
        var (tool, run) in new (string, Action)[]
        {
            ("EternalFires", () => WorldTools.EternalFires(main.SelectedSavegame)),
            ("CannibalAnger", () => main.SetCannibalAnger(45)),
            ("ResetContainers", () => gameState.ResetContainers()),
            ("RipenGardens", () => WorldTools.RipenGardens(main.SelectedSavegame)),
            ("FillPrinters", () => WorldTools.FillPrinters(main.SelectedSavegame)),
        }
    )
    {
        var toolBefore = main.ToolState();
        run();
        main.FinishTool(tool, toolBefore);
    }

    Check(
        main.HasChanges("Tools") && main.HasChanges("Npcs") && main.HasChanges("Game"),
        "Tool Edits Are Seen"
    );
    main.RevertPage("Tools");
    Check(!main.HasUnsavedChanges(), "Reverted Tools Leave Nothing Unsaved");

    // Reset Containers Saves, Every Cleared State Reads Back
    EditAndCheck(
        "Reset Containers",
        "Game",
        () => gameState.ResetContainers(),
        () => gameState.ResetContainers() == AppText.Get("NothingToReset")
    );

    // Guest Roles Change Only Who Moved, Revert Takes Them Back
    var rolesPath = Path.Combine(savegamePath, "MultiplayerPlayerRolesSaveData.json");
    File.WriteAllText(
        rolesPath,
        JsonConvert.SerializeObject(
            new
            {
                Version = "0.0.0",
                Data = new
                {
                    MultiplayerPlayerRoles = JsonConvert.SerializeObject(
                        new
                        {
                            Version = "0.0.0",
                            PlayerList = new[]
                            {
                                new
                                {
                                    PlayerId = 76561190000000001,
                                    LastKnownPlayerName = "Bravo",
                                    PlayerRole = 1,
                                },
                                new
                                {
                                    PlayerId = 76561190000000002,
                                    LastKnownPlayerName = "Alpha",
                                    PlayerRole = 16,
                                },
                            },
                        }
                    ),
                },
            }
        )
    );
    main.ReloadSavegame();
    var guests = WorldTools.ReadGuests(main.SelectedSavegame);
    Check(guests is [{ Name: "Alpha", Role: 16 }, { Name: "Bravo" }], "Guests Read By Name");
    var rolesBefore = main.ToolState();
    Check(
        WorldTools.SetGuestRoles(
            main.SelectedSavegame,
            new Dictionary<long, int> { [guests[0].SteamId] = 16 }
        ) == AppText.Get("NothingToChange"),
        "Same Role Changes Nothing"
    );
    WorldTools.SetGuestRoles(
        main.SelectedSavegame,
        new Dictionary<long, int> { [guests[1].SteamId] = 4 }
    );
    main.FinishTool("GuestRoles", rolesBefore);
    Check(
        main.HasChanges("Tools") && WorldTools.ReadGuests(main.SelectedSavegame)[1].Role == 4,
        "Guest Role Changes"
    );
    main.RevertPage("Tools");
    Check(
        !main.HasUnsavedChanges() && WorldTools.ReadGuests(main.SelectedSavegame)[1].Role == 1,
        "Reverted Guest Role"
    );
    File.Delete(rolesPath);
    main.ReloadSavegame();

    // A Clone Shows On The Next Map Visit
    map.Open();
    var worldItems = map.AllPois().OfType<WorldItemPoi>().Count();
    var cloneBefore = main.ToolState();
    new WorldItemClonerViewModel(main.SelectedSavegame!)
    {
        SelectedWorldItemType = WorldItemType.KnightV,
    }.CloneObjectAtPlayerPos(player.PlayerState.Pos);
    main.FinishTool("WorldItemCloner", cloneBefore);
    map.Close();
    map.Open();
    Check(
        map.AllPois().OfType<WorldItemPoi>().Count() == worldItems + 1,
        "Cloned Item Shows On The Map"
    );
    main.RevertPage("Tools");
    Check(!main.HasUnsavedChanges(), "Reverted Clone Leaves Nothing Unsaved");

    // A Deleted Follower Keeps Its Memory, Revive Never Doubles It
    var deletedId = followers.CopiesOf(Constants.Actors.VirginiaTypeId)[0].UniqueId;
    main.ModifyActors(save =>
        save
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)!
            .GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)!["Actors"]!
            .First(actor => actor["UniqueId"]?.Value<int>() == deletedId)
            .Remove()
    );
    main.ReviveFollower(Constants.Actors.VirginiaTypeId);
    Check(
        followers.CopiesOf(Constants.Actors.VirginiaTypeId)
            is [{ UniqueId: { } revivedId, Influences.Count: 3 }]
            && revivedId != deletedId,
        "Revive Takes A Fresh Id"
    );
    main.RevertPage("Followers");
    Check(
        !main.HasUnsavedChanges()
            && followers.CopiesOf(Constants.Actors.VirginiaTypeId) is [{ UniqueId: var backId }]
            && backId == deletedId,
        "Reverted Revive Leaves Nothing Unsaved"
    );

    // A Repeated Memory Reads Once, The Newest Wins
    main.ModifyActors(save =>
    {
        var memories = (JArray)
            save
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)!
                .GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)!["InfluenceMemory"]!;
        var memory = memories.First(entry => entry["UniqueId"]?.Value<int>() == deletedId);
        var repeated = memory.DeepClone();
        ((JArray)repeated["Influences"]!).Merge(memory["Influences"]!.DeepClone());
        memories.Add(repeated);
    });
    Check(
        followers.CopiesOf(Constants.Actors.VirginiaTypeId) is [{ Influences.Count: 3 }],
        "Repeated Memory Reads Once"
    );
    main.RevertPage("Followers");
    Check(!main.HasUnsavedChanges(), "Reverted Repeat Leaves Nothing Unsaved");

    // Armour Moves Through The Backpack, Revert Takes Both Back
    var armourCount = armor.Armour.Count();
    var movable = armor.Armour.FirstOrDefault(armor.CanMoveToInventory);
    Check(movable != null, "Sample Has Armour To Move");
    if (movable != null)
    {
        armor.MoveToInventory(movable);
        var stored = armor.InventoryArmour.FirstOrDefault(item => item.Id == movable.Id);
        Check(
            armor.Armour.Count() == armourCount - 1
                && stored != null
                && main.HasChanges("Inventory")
                && main.HasChanges("Player"),
            "Armour Moves To The Inventory"
        );
        armor.Wear(stored!);
        Check(armor.Armour.Count() == armourCount, "Armour Is Worn Again");
        main.RevertPage("Player");
        Check(!main.HasUnsavedChanges(), "Reverted Armour Move Leaves Nothing Unsaved");
    }

    // Deleting An Inventory Piece Changes Only The Inventory
    var heldArmour = armor.InventoryArmour.FirstOrDefault(inventory.IsHeld);
    Check(heldArmour != null, "Sample Holds Armour In The Inventory");
    if (heldArmour != null)
    {
        var heldCount = heldArmour.TotalCount;
        armor.DeleteFromInventory(heldArmour);
        Check(
            (heldCount > 1 ? heldArmour.TotalCount == heldCount - 1 : !inventory.IsHeld(heldArmour))
                && main.HasChanges("Inventory")
                && !main.HasChanges("Player"),
            "Deleted Inventory Piece Changes Only The Inventory"
        );
        main.RevertPage("Inventory");
        Check(!main.HasUnsavedChanges(), "Reverted Inventory Delete Leaves Nothing Unsaved");
    }

    // An Exported Inventory Imports Back With Nothing Changed
    var exportedInventory = inventory.ExportText();
    inventory.RemoveAllEquipped();
    Check(
        inventory.Inventory.All(item => item.IsEquipped) && !inventory.CanRemoveAll(),
        "Remove All Keeps The Item In Hand"
    );
    var importedCount = inventory.Import(exportedInventory + "\nnot a line\n999999 5 Unknown");
    Check(
        importedCount == inventory.Inventory.Count && !main.HasChanges("Inventory"),
        "Inventory Export Imports Back Unchanged"
    );
    Check(inventory.Import("nothing here") == 0, "Import Without Known Items Changes Nothing");

    // A Worn Piece Takes Another Type At Its Default Durability
    var worn = armor.Armour.FirstOrDefault();
    var otherType = armor.ArmourTypes.FirstOrDefault(type => type.Id != worn?.Id);
    Check(worn != null && otherType != null, "Sample Wears Armour");
    if (worn != null && otherType != null)
    {
        worn.ChangeType(otherType);
        Check(
            worn.Id == otherType.Id
                && worn.ArmourPiece.RemainingArmourpoints == worn.DefaultDurability
                && armor.IsTypeChanged(worn)
                && main.HasChanges("Player")
                && !main.HasChanges("Inventory"),
            "Worn Piece Changes Type"
        );
        main.RevertPage("Player");
        Check(!main.HasUnsavedChanges(), "Reverted Type Change Leaves Nothing Unsaved");
    }

    // Add Piece Wears A Type At Its Default Durability
    var weakest = armor.ArmourTypes[0];
    var wornBefore = armor.Armour.Count();
    Check(armor.FreeSlots.Any(), "Sample Has A Free Armour Slot");
    armor.AddPiece(weakest);
    Check(
        armor.Armour.Count() == wornBefore + 1
            && armor.Armour.Any(piece =>
                armor.IsTypeChanged(piece)
                && piece.Id == weakest.Id
                && piece.ArmourPiece.RemainingArmourpoints == weakest.Durability?.Default
            )
            && main.HasChanges("Player"),
        "Added Piece Wears At Its Default"
    );
    main.RevertPage("Player");
    Check(!main.HasUnsavedChanges(), "Reverted Added Piece Leaves Nothing Unsaved");

    // A Backpack Stack Turns Into A Type Not Held Yet
    var stack = armor.InventoryArmour.FirstOrDefault(inventory.IsHeld);
    var freeType =
        stack == null ? null : armor.TypesFor(stack).FirstOrDefault(type => type.Id != stack.Id);
    Check(stack != null && freeType != null, "Sample Holds A Stack To Convert");
    if (stack != null && freeType != null)
    {
        var stackCount = stack.TotalCount;
        armor.ChangeType(stack, freeType);
        var converted = inventory.ItemOf(freeType.Id);
        Check(
            !inventory.IsHeld(stack)
                && converted != null
                && inventory.IsHeld(converted)
                && converted.TotalCount == Math.Min(stackCount, converted.Max)
                && main.HasChanges("Inventory")
                && !main.HasChanges("Player"),
            "Backpack Stack Changes Type"
        );
        main.RevertPage("Inventory");
        Check(!main.HasUnsavedChanges(), "Reverted Stack Change Leaves Nothing Unsaved");
    }

    // Fill All Bars Raises Strength, Then Health To Its Max
    player.FillAllBars();
    Check(
        player.PlayerState.StrengthLevel == PlayerState.MaxStrengthLevel
            && player.PlayerState.CurrentHealth
                == PlayerState.MaxHealthAt(PlayerState.MaxStrengthLevel)
            && !player.CanFillAllBars,
        "Fill All Bars Fills Strength And Health"
    );
    main.RevertPage("Player");
    Check(
        !main.HasUnsavedChanges() && player.CanFillAllBars,
        "Reverted Fill Leaves Nothing Unsaved"
    );

    // Picking A Day, Then The Loaded One, Leaves No Change
    var dayOffset = SettingOf(weather.Settings, "_startingDayOffset");
    var playedDays =
        (SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue ?? 0)
        + ((SettingOf(gameState.Settings, Constants.GameStateKeys.GameHours).IntValue ?? 0) / 24.0);
    var loadedSeason = SeasonCalendar.SeasonAt(playedDays, weather.LoadedOffset, 5);
    var loadedDay = 5 - SeasonCalendar.DaysLeft((int)playedDays, weather.LoadedOffset, 5) + 1;
    var pickedSeason = (loadedSeason + 2) % 4;
    dayOffset.DecimalValue = SeasonCalendar.OffsetForDay(
        pickedSeason,
        1,
        playedDays,
        weather.LoadedOffset,
        5
    );
    Check(
        main.HasChanges("Game")
            && SeasonCalendar.SeasonAt(playedDays, dayOffset.DecimalValue ?? 0, 5) == pickedSeason
            && SeasonCalendar.DaysLeft((int)playedDays, dayOffset.DecimalValue ?? 0, 5) == 5,
        "Picked Season Day Is Seen"
    );
    dayOffset.DecimalValue = SeasonCalendar.OffsetForDay(
        loadedSeason,
        loadedDay,
        playedDays,
        weather.LoadedOffset,
        5
    );
    Check(!main.HasUnsavedChanges(), "Picking The Loaded Day Again Leaves No Change");

    // A Stat The Save Left Out Is Written Once Set
    var playerWrapper = main.SelectedSavegame!.SavegameStore.LoadJsonRaw(
        SavegameStore.FileType.PlayerStateSaveData
    )!;
    ((JArray)playerWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState)!["_entries"]!)
        .First(entry => entry["Name"]?.ToString() == "Sickness")
        .Remove();
    playerWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);
    Check(main.SaveChanges(), "Save Without Sickness Writes");
    EditAndCheck(
        "Missing Stat",
        "Player",
        () => player.PlayerState.Sickness = 12,
        () => Math.Abs(player.PlayerState.Sickness - 12) < 0.01
    );

    // A Save Keeps Its Change List In Its Backup
    player.PlayerState.Fullness = 44;
    Check(main.SaveChanges(new() { ["Player"] = [new Change("Fullness")] }), "Listed Change Saves");
    var newest = BackupManager.List(savegamePath)[0];
    Check(
        BackupManager.ChangesOf(newest.Path).GetValueOrDefault("Player") is [{ Name: "Fullness" }],
        "Backup Keeps Its Change List"
    );
    Check(
        BackupManager.Compare(savegamePath, newest.Path).Count > 0,
        "Newest Backup Differs From The Save"
    );
    Check(
        BackupDiff
            .Describe(savegamePath, newest.Path, gameData, key => key)
            .GetValueOrDefault("Player")
            ?.Exists(change => change.Name.Contains(AppText.Get("Fullness"))) == true,
        "Backup Difference Names The Setting"
    );

    // A Pinned Backup Keeps Its Label, Never Restored
    BackupManager.Pin(BackupManager.List(savegamePath)[^1], " Before Boss ");
    Check(
        BackupManager.List(savegamePath)[^1].Label == "Before Boss",
        "Pinned Backup Keeps Its Label"
    );

    // Restore Oldest Brings Back The Sample, Without The List
    var oldest = BackupManager.List(savegamePath)[^1];
    BackupManager.Restore(savegamePath, oldest.Path);
    Check(SameHashes(pristine, HashFiles(savegamePath)), "Restore Oldest Brings Every File Back");
    Check(
        BackupManager.Compare(savegamePath, oldest.Path).Count == 0,
        "Restored Backup Matches The Save"
    );

    // A Missing Backup Fails Before Any File Is Touched
    Check(
        Throws(() => BackupManager.Restore(savegamePath, Path.Combine(work, "Missing")))
            && SameHashes(pristine, HashFiles(savegamePath)),
        "Missing Backup Changes Nothing"
    );

    // An Unreadable Backup File Leaves The Save Untouched
    var partialBackup = Path.Combine(work, "PartialBackup");
    CopyDirectory(BackupManager.List(savegamePath)[0].Path, partialBackup);
    var unreadable = Path.Combine(partialBackup, "Unreadable.json");
    File.WriteAllText(unreadable, "{}");
    using (File.Open(unreadable, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Check(
            Throws(() => BackupManager.Restore(savegamePath, partialBackup))
                && SameHashes(pristine, HashFiles(savegamePath)),
            "Failed Restore Changes Nothing"
        );
    }

    // A Failed Copy Leaves No Backup Behind
    var archivePath = Path.Combine(savegamePath, "SaveData.zip");
    var backupCount = BackupManager.List(savegamePath).Count;
    using (File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Check(Throws(() => BackupManager.Create(savegamePath)), "Locked File Fails The Backup");
    }

    Check(BackupManager.List(savegamePath).Count == backupCount, "Failed Backup Leaves No Folder");

    // A Savegame Too Damaged To Open Still Restores
    File.WriteAllText(archivePath, "Not A Zip");
    var damagedOpened = true;
    try
    {
        main.SelectSavegame(SavegameManager.Load(savegamePath));
    }
    catch (InvalidDataException)
    {
        damagedOpened = false;
    }

    Check(!damagedOpened && main.SelectedSavegame is null, "Damaged Savegame Closes Itself");
    BackupManager.Restore(savegamePath, oldest.Path);
    main.SelectSavegame(SavegameManager.Load(savegamePath));
    Check(
        SameHashes(pristine, HashFiles(savegamePath)) && main.SelectedSavegame is not null,
        "Damaged Savegame Restores And Opens"
    );

    // A File Rewritten In Place Counts As An Outside Change
    main.ReloadSavegame();
    var store = main.SelectedSavegame!.SavegameStore;
    Check(!store.HasChanged(), "Fresh Load Shows No Outside Change");
    File.SetLastWriteTimeUtc(archivePath, File.GetLastWriteTimeUtc(archivePath).AddMinutes(1));
    Check(store.HasChanged(), "Rewritten File Shows As Outside Change");

    // A Missing File Fails The Save And Keeps The Edit
    var fullnessBefore = player.PlayerState.Fullness;
    player.PlayerState.Fullness = fullnessBefore + 7;
    var movedPath = Path.Combine(work, "Moved.zip");
    File.Move(archivePath, movedPath);
    Check(Throws(() => main.SaveChanges()), "Missing File Fails The Save");
    Check(main.HasUnsavedChanges(), "Failed Save Keeps The Edit");
    File.Move(movedPath, archivePath);

    // The Failed Save Merged Nothing, So Revert Still Undoes It
    main.RevertPage("Player");
    Check(
        !main.HasUnsavedChanges() && player.PlayerState.Fullness == fullnessBefore,
        "Revert After A Failed Save Undoes The Edit"
    );

    // A Singleplayer Copy Matches Byte For Byte, Save Time Included
    var copied = SavegameManager.Duplicate(
        main.SelectedSavegame!,
        SavegameManager.SinglePlayerFolder
    );
    var copiedId = Path.GetFileName(copied);
    Check(
        Path.GetFileName(Path.GetDirectoryName(copied)) == "SinglePlayer"
            && copiedId.Length == 10
            && long.Parse(copiedId) < int.MaxValue
            && SameHashes(HashFiles(savegamePath), HashFiles(copied))
            && Directory.GetLastWriteTimeUtc(copied) == Directory.GetLastWriteTimeUtc(savegamePath),
        "Copy To Singleplayer Matches The Save"
    );

    // Progress Reads Every Collectible Group And The Counts
    var progress = ProgressReader.Read(
        main.SelectedSavegame!,
        gameData,
        provider.GetRequiredService<PoiLoader>()
    );
    Check(
        progress.Collectibles.Count == 4
            && progress.Collectibles[0].Items.Count == 7
            && progress.Days > 0
            && progress.Kills is not null
            && progress.Crafted.Count > 0,
        "Progress Reads Collectibles And Counts"
    );

    // Every Bunker And Most Pages Open On Their Map Point
    var located = progress
        .Collectibles.SelectMany(group => group.Items)
        .Where(collectible => collectible.Position is not null)
        .ToList();
    Check(
        progress.Collectibles[0].Items.All(bunker => bunker.Position is not null)
            && located.Count >= 25
            && located.All(collectible =>
            {
                map.FocusPosition = collectible.Position;
                return map.TakeFocus() is not null;
            }),
        "Progress Collectibles Show On Map"
    );

    // A Copied Player Fills The Other Save After A Backup
    var emptied = new SavegameStore(copied);
    var emptiedInventory = emptied.LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)!;
    emptiedInventory.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)![
        "ItemInstanceManagerData"
    ]!["ItemBlocks"] = new JArray();
    emptiedInventory.MarkAsModified(Constants.JsonKeys.PlayerInventory);
    emptied.SaveWrappers(
        new() { [SavegameStore.FileType.PlayerInventorySaveData] = emptiedInventory }
    );
    var backupsBeforeCopy = BackupManager.Count(copied);
    var copiedParts = PlayerCopier.Copy(
        savegamePath,
        copied,
        Enum.GetValues<PlayerPart>(),
        "Sample"
    );
    Check(
        copiedParts.Count == 4
            && JToken.DeepEquals(
                InventoryOf(new SavegameStore(savegamePath)),
                InventoryOf(new SavegameStore(copied))
            )
            && BackupManager.Count(copied) == backupsBeforeCopy + 1,
        "Copied Player Matches, After A Backup"
    );

    // Only A Copy The Editor Made Can Be Deleted
    var multiplayerCopy = SavegameManager.Load(
        SavegameManager.Duplicate(main.SelectedSavegame!, SavegameManager.MultiplayerFolder)
    )!;
    Check(
        SavegameManager.IsCopy(multiplayerCopy)
            && !SavegameManager.IsCopy(main.SelectedSavegame!)
            && multiplayerCopy.IsMultiPlayer()
            && Throws(() => SavegameManager.DeleteCopy(main.SelectedSavegame!)),
        "Only Copies Count As Copies"
    );
    // A Repeated Stat Copies Once, The Newest Wins
    var repeated = new SavegameStore(copied);
    var repeatedState = repeated.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)!;
    var repeatedEntries = (JArray)
        repeatedState.GetJsonBasedToken(Constants.JsonKeys.PlayerState)!["_entries"]!;
    repeatedEntries.Add(
        new JObject
        {
            ["Name"] = "StrengthLevel",
            ["SettingType"] = 1,
            ["IntValue"] = 77,
        }
    );
    repeatedState.MarkAsModified(Constants.JsonKeys.PlayerState);
    repeated.SaveWrappers(new() { [SavegameStore.FileType.PlayerStateSaveData] = repeatedState });
    PlayerCopier.Copy(copied, multiplayerCopy.FullPath, [PlayerPart.Stats], "Repeated");
    var strengthLevels = new SavegameStore(multiplayerCopy.FullPath)
        .LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)!
        .GetJsonBasedToken(Constants.JsonKeys.PlayerState)!["_entries"]!
        .Where(entry => entry["Name"]?.ToString() == "StrengthLevel")
        .ToList();
    Check(
        strengthLevels is [var strengthLevel] && strengthLevel["IntValue"]?.Value<int>() == 77,
        "Repeated Stat Copies Once"
    );

    SavegameManager.DeleteCopy(multiplayerCopy);
    Check(
        !Directory.Exists(multiplayerCopy.FullPath) && Directory.Exists(savegamePath),
        "Deleting A Copy Keeps The Original"
    );

    // An Export Never Overwrites
    var exported = SavegameManager.Export(main.SelectedSavegame!);
    var exportedAgain = SavegameManager.Export(main.SelectedSavegame!);
    using (var archive = ZipFile.OpenRead(exported))
    {
        Check(
            exported != exportedAgain
                && archive.Entries.Any(entry =>
                    entry.FullName.StartsWith(Path.GetFileName(savegamePath) + "/SaveData")
                ),
            "Export Holds The Save Folder"
        );
    }

    File.Delete(exported);
    File.Delete(exportedAgain);

    // A Failed Export Leaves No Half Zip Behind
    var exportsBefore = Directory.GetFiles(AppInfo.ExportsDirectory).Length;
    using (File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Check(
            Throws(() => SavegameManager.Export(main.SelectedSavegame!))
                && Directory.GetFiles(AppInfo.ExportsDirectory).Length == exportsBefore,
            "Failed Export Leaves No File"
        );
    }

    // A Failed Restore Leaves Every File, Stale Temporaries Too
    var restoreSave = FreshSample(sandbox, "Restore");
    var oddBackup = Path.Combine(sandbox, "OddBackup");
    CopyDirectory(restoreSave, oddBackup);
    File.WriteAllText(Path.Combine(oddBackup, "Custom_36_Days_09_58_47.name"), "changed");
    File.WriteAllText(Path.Combine(oddBackup, "SaveData.zip.tmp"), "kept in the backup");
    File.WriteAllText(Path.Combine(oddBackup, "Zz.json"), "{}");
    File.WriteAllText(Path.Combine(restoreSave, "SaveData.zip.tmp"), "live sentinel");
    Directory.CreateDirectory(Path.Combine(restoreSave, "nested"));
    File.WriteAllText(Path.Combine(restoreSave, "nested", "extra.json"), "{}");
    var restoreStart = HashTree(restoreSave);
    using (
        File.Open(
            Path.Combine(oddBackup, "Zz.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        )
    )
    {
        Check(
            Refused(() => BackupManager.Restore(restoreSave, oddBackup))
                && SameHashes(restoreStart, HashTree(restoreSave)),
            "Unreadable Backup File Keeps Live Temporaries"
        );
    }

    using (
        File.Open(
            Path.Combine(restoreSave, "SaveData.zip"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        )
    )
    {
        Check(
            Refused(() => BackupManager.Restore(restoreSave, oddBackup))
                && SameHashes(restoreStart, HashTree(restoreSave)),
            "Locked Save File Mid Restore Puts Every File Back"
        );
    }

    // A Finished Restore Matches The Backup, Nested Files Too
    BackupManager.Restore(restoreSave, oddBackup);
    Check(
        SameHashes(HashTree(oddBackup), HashTree(restoreSave))
            && File.Exists(
                Path.Combine(BackupManager.List(restoreSave)[0].Path, "nested", "extra.json")
            ),
        "Restore Matches The Backup And Keeps Extras In Its Safety Copy"
    );

    // A Save Writes Every File Or None, Edits Stay
    var mixedSave = FreshSample(sandbox, "Mixed");
    EditArchive(
        mixedSave,
        archive =>
            archive
                .GetEntry("PlayerStateSaveData.json")!
                .ExtractToFile(Path.Combine(mixedSave, "PlayerStateSaveData.json"))
    );
    main.SelectSavegame(SavegameManager.Load(mixedSave));
    var mixedDays = SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue ?? 0;
    var loadedFullness = player.PlayerState.Fullness;
    player.PlayerState.Fullness = 21;
    SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue = mixedDays + 1;
    var mixedStart = HashTree(mixedSave);
    var archiveLock = File.Open(
        Path.Combine(mixedSave, "SaveData.zip"),
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read
    );
    var saveFailed = Refused(() => main.SaveChanges());
    var keptFiles = SameHashes(mixedStart, HashTree(mixedSave));
    archiveLock.Dispose();
    Check(
        saveFailed && keptFiles && main.HasChanges("Player") && main.HasChanges("Game"),
        "Failed Save Writes No File And Keeps Every Edit"
    );

    // Revert After A Failed Write Takes Back Only That Page
    main.RevertPage("Player");
    Check(
        Math.Abs(player.PlayerState.Fullness - loadedFullness) < 0.01
            && !main.HasChanges("Player")
            && main.HasChanges("Game"),
        "Revert After A Failed Write Keeps Other Pages"
    );
    player.PlayerState.Fullness = 21;
    Check(
        main.SaveChanges()
            && Math.Abs(player.PlayerState.Fullness - 21) < 0.01
            && SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue
                == mixedDays + 1,
        "Retried Save Writes The Loose File And The Archive"
    );

    // A Target Gone At Write Time Fails, Never Skipped Silently
    var mixedStore = main.SelectedSavegame!.SavegameStore;
    var playerWrapperOnDisk = mixedStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)!;
    (
        (JArray)playerWrapperOnDisk.GetJsonBasedToken(Constants.JsonKeys.PlayerState)!["_entries"]!
    ).Add(new JObject { ["Name"] = "Sentinel", ["IntValue"] = 1 });
    playerWrapperOnDisk.MarkAsModified(Constants.JsonKeys.PlayerState);
    File.Move(
        Path.Combine(mixedSave, "PlayerStateSaveData.json"),
        Path.Combine(sandbox, "PlayerStateSaveData.json")
    );
    EditArchive(mixedSave, archive => archive.GetEntry("PlayerStateSaveData.json")!.Delete());
    Check(
        Refused(() => mixedStore.SaveAllModified()) && mixedStore.HasModified(),
        "Vanished Target Fails And Stays Marked"
    );

    // A Tool Put Back By Hand Leaves Nothing To Save
    var toolSave = FreshSample(sandbox, "Tool");
    main.SelectSavegame(SavegameManager.Load(toolSave));
    var heldValues = gameState
        .NamedIntDatas.Select(setting => (setting, setting.IntValue))
        .ToList();
    var resetBefore = main.ToolState();
    gameState.ResetContainers();
    main.FinishTool("ResetContainers", resetBefore);
    var toolMarked = main.HasChanges("Tools");
    heldValues.ForEach(pair => pair.setting.IntValue = pair.IntValue);
    var toolBackups = BackupManager.List(toolSave).Count;
    Check(
        toolMarked
            && !main.HasChanges("Tools")
            && !main.HasUnsavedChanges()
            && !main.SaveChanges()
            && BackupManager.List(toolSave).Count == toolBackups,
        "Tool Put Back By Hand Leaves Nothing To Save"
    );

    // Revert Tools Keeps A Game Days Edit Made Beside It
    var toolDays = SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue ?? 0;
    var heldBefore = gameState.NamedIntDatas.Count(setting => setting.IntValue != null);
    SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue = toolDays + 3;
    resetBefore = main.ToolState();
    gameState.ResetContainers();
    main.FinishTool("ResetContainers", resetBefore);
    main.RevertPage("Tools");
    Check(
        SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue == toolDays + 3
            && gameState.NamedIntDatas.Count(setting => setting.IntValue != null) == heldBefore,
        "Revert Tools Keeps Other Game Edits"
    );
    main.RevertPage("Game");

    // An Unreadable Old Backup Never Blocks A Save
    var unreadableBackup = Path.Combine(
        BackupManager.BackupDirectory,
        "Unrelated",
        "Mode",
        "Save",
        "2001-01-01 00-00-00"
    );
    Directory.CreateDirectory(unreadableBackup);
    File.WriteAllText(Path.Combine(unreadableBackup, "x.json"), "{}");
    deniedFolders.Add(unreadableBackup);
    SetDenied(unreadableBackup, true);
    player.PlayerState.Fullness = 44;
    Check(
        NoThrow(() => main.SaveChanges()) && Math.Abs(player.PlayerState.Fullness - 44) < 0.01,
        "Unreadable Old Backup Never Blocks A Save"
    );
    Check(
        BackupManager.TotalSizeOrNull() is null
            && NoThrow(() =>
                BackupManager.List(
                    Path.Combine(BackupManager.BackupDirectory, "Unrelated", "Mode", "Save")
                )
            ),
        "Unreadable Backups Leave Usage Unknown And Still List"
    );
    SetDenied(unreadableBackup, false);
    deniedFolders.Remove(unreadableBackup);

    // Twelve Backups In One Second List Newest First
    var twins = Enumerable
        .Range(1, 12)
        .Select(number =>
            Path.Combine(
                BackupManager.FolderOf(toolSave),
                number == 1 ? "2001-01-01 00-00-00" : $"2001-01-01 00-00-00 ({number})"
            )
        )
        .ToList();
    foreach (var twin in twins)
    {
        Directory.CreateDirectory(twin);
        File.WriteAllText(Path.Combine(twin, "x.json"), "{}");
    }

    Check(
        BackupManager
            .List(toolSave)
            .Select(backup => backup.Path)
            .Where(path => path.Contains("2001-01-01", StringComparison.Ordinal))
            .SequenceEqual(Enumerable.Reverse(twins)),
        "Same Second Backups List Newest First"
    );
    twins.ForEach(twin => Directory.Delete(twin, true));

    // A Damaged Change List Drops Its Nulls
    var metadata = Path.Combine(sandbox, "Metadata");
    Directory.CreateDirectory(metadata);
    File.WriteAllText(
        Path.Combine(metadata, BackupManager.ChangesFileName),
        """{ "Player": null, "Game": [null, { "Name": "Days" }] }"""
    );
    var readChanges = BackupManager.ChangesOf(metadata);
    Check(
        !readChanges.ContainsKey("Player") && readChanges["Game"] is [{ Name: "Days" }],
        "Damaged Change Lists Drop Their Nulls"
    );

    // A Wrong Shaped Save Never Breaks The Healthy List
    var savesRoot = SavegameManager.GetSavePath();
    CopyDirectory(Path.Combine(SourceDirectory(), "Sample"), savesRoot);
    var wrongShaped = Path.Combine(savesRoot, SampleSteamId, "Multiplayer", "1000000003");
    CopyDirectory(Path.Combine(savesRoot, SampleSteamId, "Multiplayer", "1000000001"), wrongShaped);
    File.WriteAllText(
        Path.Combine(wrongShaped, "GameStateSaveData.json"),
        """{ "Data": { "GameState": "{ \"GameDays\": \"abc\" }" } }"""
    );
    Check(
        NoThrow(() => SavegameManager.GetSavegames().ForEach(savegame => _ = savegame.Summary))
            && SavegameManager.GetSavegames() is { Count: 2 } listed
            && listed.Count(savegame => savegame.Summary != null) == 1,
        "A Wrong Shaped Save Never Breaks The List"
    );

    // Copy Player Changes Nothing It Cannot Finish
    var copyTarget = FreshSample(sandbox, "CopyTarget");
    EditArchive(copyTarget, archive => archive.GetEntry("PlayerStateSaveData.json")!.Delete());
    var copySource = FreshSample(sandbox, "CopySource");
    var sourceStore = new SavegameStore(copySource);
    var sourceInventory = sourceStore.LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)!;
    var firstBlock = sourceInventory
        .GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)!
        .SelectToken("ItemInstanceManagerData.ItemBlocks")![0]!;
    firstBlock["TotalCount"] = firstBlock["TotalCount"]!.Value<int>() + 1;
    sourceInventory.MarkAsModified(Constants.JsonKeys.PlayerInventory);
    sourceStore.SaveAllModified();
    var targetInventory = EntryHash(copyTarget, "PlayerInventorySaveData.json");
    var blockedParts = PlayerCopier.Copy(copySource, copyTarget, [PlayerPart.Inventory], "Source");
    Check(
        blockedParts.Count == 0
            && EntryHash(copyTarget, "PlayerInventorySaveData.json") == targetInventory,
        "Copy Player Changes Nothing It Cannot Finish"
    );

    // Damaged Profiles Are Refused And Stay Byte For Byte
    var profile = Path.Combine(sandbox, "Profile", "PlayerProfile.json");
    Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
    string[] brokenProfiles =
    [
        "",
        "not json",
        "[]",
        """{ "_settings": 5 }""",
        """{ "_settings": {} }""",
        """{ "_settings": [null] }""",
        """{ "_settings": [5] }""",
        """{ "_settings": [{ "BoolValue": true }] }""",
        """{ "_settings": [{ "Name": 7 }] }""",
        """{ "_settings": [{ "Name": "Stats.EscapedIsland", "BoolValue": "yes" }] }""",
    ];
    Check(
        brokenProfiles.All(text =>
        {
            File.WriteAllText(profile, text);
            return Refused(() => new UnlocksViewModel(profile))
                && File.ReadAllText(profile) == text;
        }),
        "Damaged Profiles Are Refused Untouched"
    );

    // Unlocks Change Every Repeat And Keep What Changed Outside
    const string RepeatedProfile = """
        { "_settings": [
            { "Name": "Stats.CreativeModeUnlocked", "BoolValue": false },
            { "Name": "Stats.CreativeModeUnlocked", "BoolValue": true },
            { "Name": "Other", "IntValue": 3 }
        ] }
        """;
    File.WriteAllText(profile, RepeatedProfile);
    var repeatedUnlocks = new UnlocksViewModel(profile);
    var shownLast = repeatedUnlocks.Values["Stats.CreativeModeUnlocked"];
    repeatedUnlocks.Values["Stats.CreativeModeUnlocked"] = false;
    File.WriteAllText(
        profile,
        RepeatedProfile.Replace(
            """{ "Name": "Other", "IntValue": 3 }""",
            """{ "Name": "Other", "IntValue": 3 }, { "Name": "Stats.EscapedIsland", "BoolValue": true }"""
        )
    );
    repeatedUnlocks.Save();
    var rereadUnlocks = new UnlocksViewModel(profile);
    Check(
        shownLast
            && !rereadUnlocks.Values["Stats.CreativeModeUnlocked"]
            && rereadUnlocks.Values["Stats.EscapedIsland"]
            && File.ReadAllText(profile).Contains("\"Other\"", StringComparison.Ordinal),
        "Unlocks Write Every Repeat And Keep Outside Changes"
    );
    main.SelectSavegame(SavegameManager.Load(savegamePath));

    // A Linked Folder Inside The Save Refuses The Restore
    var linkedSave = FreshSample(sandbox, "Linked");
    var outside = Path.Combine(sandbox, "Outside");
    Directory.CreateDirectory(outside);
    File.WriteAllText(Path.Combine(outside, "x.json"), "outside sentinel");
    var linkedBackup = Path.Combine(sandbox, "LinkedBackup");
    CopyDirectory(linkedSave, linkedBackup);
    Directory.CreateDirectory(Path.Combine(linkedBackup, "nested"));
    File.WriteAllText(Path.Combine(linkedBackup, "nested", "x.json"), "from the backup");
    Junction(Path.Combine(linkedSave, "nested"), outside);
    var outsideBefore = HashTree(outside);
    var linkedBefore = HashFiles(linkedSave);
    Check(
        Refused(() => BackupManager.Restore(linkedSave, linkedBackup))
            && SameHashes(outsideBefore, HashTree(outside))
            && SameHashes(linkedBefore, HashFiles(linkedSave)),
        "Linked Folder In The Save Refuses The Restore"
    );

    // Editor Files Count Only At The Backup's Root
    var metadataSave = FreshSample(sandbox, "MetadataSave");
    var metadataBackup = Path.Combine(sandbox, "MetadataBackup");
    CopyDirectory(metadataSave, metadataBackup);
    File.WriteAllText(Path.Combine(metadataBackup, "SoTF.Editor.Label.txt"), "root label");
    Directory.CreateDirectory(Path.Combine(metadataBackup, "nested"));
    File.WriteAllText(Path.Combine(metadataBackup, "nested", "SoTF.Editor.Label.txt"), "data");
    BackupManager.Restore(metadataSave, metadataBackup);
    Check(
        !File.Exists(Path.Combine(metadataSave, "SoTF.Editor.Label.txt"))
            && File.Exists(Path.Combine(metadataSave, "nested", "SoTF.Editor.Label.txt")),
        "Only Root Editor Files Stay Out Of A Restore"
    );

    // A Loose File Document Is Never Written Into The Archive
    var sourceSave = FreshSample(sandbox, "Source");
    EditArchive(
        sourceSave,
        archive =>
            archive
                .GetEntry("PlayerStateSaveData.json")!
                .ExtractToFile(Path.Combine(sourceSave, "PlayerStateSaveData.json"))
    );
    main.SelectSavegame(SavegameManager.Load(sourceSave));
    player.PlayerState.Fullness = 12;
    File.Delete(Path.Combine(sourceSave, "PlayerStateSaveData.json"));
    var archiveEntryBefore = EntryHash(sourceSave, "PlayerStateSaveData.json");
    Check(
        Refused(() => main.SaveChanges())
            && EntryHash(sourceSave, "PlayerStateSaveData.json") == archiveEntryBefore
            && main.HasChanges("Player"),
        "Loose File Gone Since Loading Fails Instead Of Moving Into The Archive"
    );

    // A Target Gone After Staging Fails, Never Created Anew
    var transaction = Path.Combine(sandbox, "Transaction");
    Directory.CreateDirectory(transaction);
    var firstFile = Path.Combine(transaction, "A.json");
    var secondFile = Path.Combine(transaction, "B.json");
    File.WriteAllText(firstFile, "old a");
    File.WriteAllText(secondFile, "old b");
    Check(
        Refused(() =>
            FileTransaction.Commit(
                [
                    (firstFile, true, temporary => File.WriteAllText(temporary, "new a")),
                    (
                        secondFile,
                        true,
                        temporary =>
                        {
                            File.Delete(firstFile);
                            File.WriteAllText(temporary, "new b");
                        }
                    ),
                ],
                []
            )
        )
            && !File.Exists(firstFile)
            && File.ReadAllText(secondFile) == "old b"
            && Directory.GetFiles(transaction).Length == 1,
        "Target Gone After Staging Fails And Is Not Recreated"
    );

    // A Failure After The First Replacement Puts It Back
    File.WriteAllText(firstFile, "old a");
    using (File.Open(secondFile, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        Check(
            Refused(() =>
                FileTransaction.Commit(
                    [
                        (firstFile, true, temporary => File.WriteAllText(temporary, "new a")),
                        (secondFile, true, temporary => File.WriteAllText(temporary, "new b")),
                    ],
                    []
                )
            )
                && File.ReadAllText(firstFile) == "old a"
                && File.ReadAllText(secondFile) == "old b"
                && Directory.GetFiles(transaction).Length == 2,
            "Failure After A Placed File Rolls It Back"
        );
    }

    // Saving Over A Version Changed Since It Was Confirmed Fails
    var confirmSave = FreshSample(sandbox, "Confirm");
    main.SelectSavegame(SavegameManager.Load(confirmSave));
    player.PlayerState.Fullness = 13;
    var confirmedStamps = main.SelectedSavegame!.SavegameStore.Stamps();
    var confirmArchive = Path.Combine(confirmSave, "SaveData.zip");
    File.SetLastWriteTimeUtc(
        confirmArchive,
        File.GetLastWriteTimeUtc(confirmArchive).AddMinutes(1)
    );
    var confirmFiles = HashFiles(confirmSave);
    Check(
        Refused(() => main.SaveChanges(null, confirmedStamps))
            && SameHashes(confirmFiles, HashFiles(confirmSave))
            && main.HasChanges("Player"),
        "Save Confirmed For An Older Version Writes Nothing"
    );

    // Cannibal Anger Revert Keeps An Independent Fear Edit
    var angerSave = FreshSample(sandbox, "Anger");
    main.SelectSavegame(SavegameManager.Load(angerSave));
    var cannibal = npcs.AllActors.First(npc =>
        npc.ActorType?.IsCannibal == true
        && npc.State != Constants.Actors.StateDead
        && npc.Influences?.Any(influence => influence.TypeId == Influence.Type.Player) == true
    );
    var cannibalId = cannibal.UniqueId;
    var angerBeforeTool = cannibal
        .Influences!.First(influence => influence.TypeId == Influence.Type.Player)
        .Anger;
    var angerBefore = main.ToolState();
    main.SetCannibalAnger(70);
    main.FinishTool("CannibalAnger", angerBefore);
    main.ModifyActors(save =>
    {
        var wrapper = save.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)!;
        var memory = wrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)![
            "InfluenceMemory"
        ]!.Last(entry => entry["UniqueId"]?.Value<int>() == cannibalId);
        memory["Influences"]!.First(influence =>
            influence["TypeId"]?.ToString() == Influence.Type.Player
        )["Fear"] = 33f;
        wrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
    });
    main.RevertPage("Tools");
    var playerFeeling = npcs
        .AllActors.First(npc => npc.UniqueId == cannibalId)
        .Influences!.First(influence => influence.TypeId == Influence.Type.Player);
    Check(
        Math.Abs(playerFeeling.Fear - 33) < 0.01
            && Math.Abs(playerFeeling.Anger - angerBeforeTool) < 0.01,
        "Cannibal Anger Revert Keeps An Independent Fear Edit"
    );

    // A Tool Undone By Hand Stays Clean Beside Game Edits
    var dotSave = FreshSample(sandbox, "Dot");
    main.SelectSavegame(SavegameManager.Load(dotSave));
    var dotValues = gameState.NamedIntDatas.Select(setting => (setting, setting.IntValue)).ToList();
    var dotBefore = main.ToolState();
    gameState.ResetContainers();
    main.FinishTool("ResetContainers", dotBefore);
    dotValues.ForEach(pair => pair.setting.IntValue = pair.IntValue);
    SettingOf(gameState.Settings, Constants.GameStateKeys.GameDays).IntValue += 2;
    Check(
        !main.IsToolChanged("ResetContainers")
            && !main.HasChanges("Tools")
            && main.HasChanges("Game"),
        "Undone Tool Stays Clean Beside A Game Edit"
    );
    main.RevertPage("Game");

    // A Sandbox Ignores Outside Save Folders And Never Installs
    UserSettings.Current.SavegamePath = Path.GetPathRoot(Path.GetTempPath())!;
    var ignoredOutside = IsInside(SavegameManager.GetSavePath(), sandbox);
    UserSettings.Current.SavegamePath = "";
    Check(ignoredOutside && !Updater.CanInstall, "Sandbox Ignores Outside Folders And Updates");

    // A Malformed Owner Marker Never Counts As An Ended Run
    var strangerRun = Path.Combine(testsRoot, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(strangerRun);
    File.WriteAllText(Path.Combine(strangerRun, OwnerFile), "not a marker");
    RemoveAbandonedRuns(testsRoot, OwnerFile);
    Check(Directory.Exists(strangerRun), "Malformed Owner Marker Keeps Its Folder");
    Directory.Delete(strangerRun, true);

    // Array Shaped Game State Lists And Compares As Damaged
    var arrayShaped = Path.Combine(savesRoot, SampleSteamId, "Multiplayer", "1000000004");
    CopyDirectory(Path.Combine(savesRoot, SampleSteamId, "Multiplayer", "1000000001"), arrayShaped);
    File.WriteAllText(
        Path.Combine(arrayShaped, "GameStateSaveData.json"),
        """{ "Data": { "GameState": "[]" } }"""
    );
    Check(
        NoThrow(() => SavegameManager.GetSavegames().ForEach(savegame => _ = savegame.Summary))
            && SavegameManager.Load(arrayShaped)!.Summary is null
            && Refused(() =>
                BackupDiff.Describe(
                    arrayShaped,
                    Path.Combine(savesRoot, SampleSteamId, "Multiplayer", "1000000001"),
                    gameData,
                    key => key
                )
            ),
        "Array Shaped Game State Lists And Compares As Damaged"
    );

    // Vanished Or Already Satisfied Profiles Write Nothing
    File.WriteAllText(profile, """{ "_settings": [] }""");
    var goneUnlocks = new UnlocksViewModel(profile);
    goneUnlocks.Values["Stats.EscapedIsland"] = true;
    File.Delete(profile);
    var goneRefused = Refused(goneUnlocks.Save) && !File.Exists(profile);
    File.WriteAllText(profile, """{ "_settings": [] }""");
    var convergedUnlocks = new UnlocksViewModel(profile);
    convergedUnlocks.Values["Stats.EscapedIsland"] = true;
    const string Converged =
        """{ "_settings": [{ "Name": "Stats.EscapedIsland", "BoolValue": true }] }""";
    File.WriteAllText(profile, Converged);
    convergedUnlocks.Save();
    Check(
        goneRefused && File.ReadAllText(profile) == Converged && !convergedUnlocks.HasChanges,
        "Unlocks Refuse A Vanished Profile And Skip An Effective No Op"
    );
    main.SelectSavegame(SavegameManager.Load(savegamePath));

    // Every Language Reads Back As Picked, Brazilian Portuguese Too
    Check(
        AppText.Languages.All(language =>
        {
            AppCulture.Apply(language.Culture);
            return AppCulture.Current == language.Culture;
        }),
        "Every Language Reads Back As Picked"
    );
    AppCulture.Apply(AppText.DefaultCulture);

    // Every Language Holds Every Text With English Placeholders
    var resources = Path.Combine(SourceDirectory(), "..", "SoTF.Editor.Data", "Resources");
    var englishTexts = ResxTexts(Path.Combine(resources, "SharedResource.resx"));
    foreach (
        var (culture, name) in AppText.Languages.Where(language =>
            language.Culture != AppText.DefaultCulture
        )
    )
    {
        var texts = ResxTexts(Path.Combine(resources, $"SharedResource.{culture}.resx"));
        Check(
            texts.Count == englishTexts.Count
                && englishTexts.All(pair =>
                    texts.TryGetValue(pair.Key, out var text)
                    && text.Trim().Length > 0
                    && Placeholders(text) == Placeholders(pair.Value)
                ),
            $"{name} Holds Every Text"
        );
    }

    static JToken? InventoryOf(SavegameStore store)
    {
        return store
            .LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory);
    }

    void ToolCheck(string tool, Func<string> run)
    {
        // A Tool That Finds Nothing Saves Nothing, Nothing Stays
        run();
        main.SaveChanges();
        Check(
            main.SelectedSavegame != null && !main.SaveChanges(),
            $"{tool} Leaves Nothing Unsaved"
        );
    }

    void EditAndCheck(string page, string route, Action edit, Func<bool> readBack)
    {
        edit();
        Check(main.HasChanges(route), $"{page} Edit Shows As Changed");
        Check(main.SaveChanges(), $"{page} Edit Saves");
        Check(!main.HasChanges(route), $"{page} Shows No Changes After Saving");
        Check(readBack(), $"{page} Edit Reads Back After Reload");
        Check(!main.SaveChanges(), $"{page} Leaves Nothing Unsaved");
    }
}
finally
{
    // Only This Run's Sandbox Goes, Readable Again First
    deniedFolders.ForEach(folder => SetDenied(folder, false));
    try
    {
        Directory.Delete(sandbox, true);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        // A Junction Can Report Denied Once While Its Removal Lands
        Directory.Delete(sandbox, true);
    }
}

// The Owner's Own Folders Never Saw This Run
Check(SameHashes(ownerFolders, OwnerManifest()), "Owner Folders Untouched");
Console.WriteLine(failures.Count == 0 ? "All Checks Passed" : $"{failures.Count} Checks Failed");
return failures.Count == 0 ? 0 : 1;

void Check(bool passed, string name)
{
    Console.WriteLine($"{(passed ? "Passed" : "Failed")} {name}");
    if (!passed)
    {
        failures.Add(name);
    }
}

static bool Throws(Action action)
{
    try
    {
        action();
        return false;
    }
    catch (IOException)
    {
        return true;
    }
}

static GenericSetting SettingOf(IEnumerable<GenericSetting> settings, string name)
{
    return settings.First(setting => setting.Name == name);
}

static (int Collection, int Storage) FirstFillableStorage(StoragePageViewModel storage)
{
    for (var collection = 0; collection < storage.StorageCollections.Count; collection++)
    {
        var storages = storage.StorageCollections[collection].Storages;
        for (var index = 0; index < storages.Count; index++)
        {
            if (storages[index].SupportedItems.Count > 0)
            {
                return (collection, index);
            }
        }
    }

    throw new InvalidOperationException("Found No Storage To Fill");
}

static Dictionary<string, string> ResxTexts(string path)
{
    return XDocument
        .Load(path)
        .Root!.Elements("data")
        .ToDictionary(
            data => (string)data.Attribute("name")!,
            data => (string?)data.Element("value") ?? ""
        );
}

static string Placeholders(string text)
{
    return string.Join(" ", Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Order());
}

static string SourceDirectory([CallerFilePath] string path = "")
{
    return Path.GetDirectoryName(path)!;
}

static void CopyDirectory(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var file in Directory.GetFiles(from))
    {
        File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
    }

    foreach (var directory in Directory.GetDirectories(from))
    {
        CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
    }
}

static Dictionary<string, string> HashFiles(string directory)
{
    return Directory
        .GetFiles(directory)
        .ToDictionary(
            file => Path.GetFileName(file),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))
        );
}

static bool SameHashes(Dictionary<string, string> left, Dictionary<string, string> right)
{
    return left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
}

static Dictionary<string, string> HashTree(string directory)
{
    // Every Nested File By Its Relative Path
    return Directory
        .GetFiles(directory, "*", SearchOption.AllDirectories)
        .ToDictionary(
            file => Path.GetRelativePath(directory, file),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
            StringComparer.OrdinalIgnoreCase
        );
}

static bool IsInside(string path, string root)
{
    return Path.GetFullPath(path)
        .StartsWith(
            Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );
}

static string OwnerStamp(int processId)
{
    using var process = System.Diagnostics.Process.GetProcessById(processId);
    return $"{processId} {process.StartTime.Ticks}";
}

static void RemoveAbandonedRuns(string testsRoot, string ownerFile)
{
    // Only Ended Runs' Sandboxes, Never Unmarked Or Unreadable Ones
    foreach (var folder in Directory.Exists(testsRoot) ? Directory.GetDirectories(testsRoot) : [])
    {
        var owner = Path.Combine(folder, ownerFile);
        string[] parts;
        try
        {
            parts = File.Exists(owner) ? File.ReadAllText(owner).Split(' ') : [];
        }
        catch (IOException)
        {
            continue;
        }

        // A Whole Marker Whose Process Is Verifiably Gone Or Replaced
        if (
            parts.Length != 2
            || !int.TryParse(parts[0], out var processId)
            || !long.TryParse(parts[1], out _)
            || Alive(processId, string.Join(' ', parts))
        )
        {
            continue;
        }

        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Left Abandoned Run {Path.GetFileName(folder)}");
        }
    }

    static bool Alive(int processId, string stamp)
    {
        try
        {
            return OwnerStamp(processId) == stamp;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

static Dictionary<string, string> OwnerManifest()
{
    // Owner Debug And Release Folders Plus Game Saves, Hashed
    var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    string[] roots =
    [
        Path.Combine(appData, "SoTF.Editor Debug"),
        Path.Combine(appData, "SoTF.Editor"),
        Path.Combine(documents, "SoTF.Editor Debug"),
        Path.Combine(documents, "SoTF.Editor"),
        Path.Combine(
            Path.GetDirectoryName(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            )!,
            "LocalLow",
            "Endnight",
            "SonsOfTheForest",
            "Saves"
        ),
    ];
    foreach (var root in roots.Where(Directory.Exists))
    {
        manifest[root] = "present";
        foreach (
            var file in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories)
        )
        {
            if (
                file.FullName.Contains(@"\WebView2\", StringComparison.OrdinalIgnoreCase)
                || file.FullName.Contains(@"\Logs\", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            try
            {
                using var stream = new FileStream(
                    file.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                manifest[file.FullName] =
                    $"{file.Length} {file.LastWriteTimeUtc.Ticks} {Convert.ToHexString(SHA256.HashData(stream))}";
            }
            catch (IOException)
            {
                manifest[file.FullName] = $"{file.Length} {file.LastWriteTimeUtc.Ticks} unreadable";
            }
        }
    }

    return manifest;
}

static bool HasLinkedAncestor(string folder)
{
    // The Sandbox And Every Folder Above It, None A Link
    for (var current = new DirectoryInfo(folder); current != null; current = current.Parent)
    {
        if (current.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return true;
        }
    }

    return false;
}

static void Junction(string link, string target)
{
    using var process = System.Diagnostics.Process.Start(
        new System.Diagnostics.ProcessStartInfo("cmd", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        }
    )!;
    process.StandardOutput.ReadToEnd();
    process.WaitForExit();
}

static bool NoThrow(Action action)
{
    try
    {
        action();
        return true;
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Threw {exception.GetType().Name}");
        return false;
    }
}

static bool Refused(Action action)
{
    // Refused As Damaged, Not Crashed On Something Unexpected
    try
    {
        action();
        return false;
    }
    catch (Exception exception)
        when (exception is IOException or InvalidDataException or JsonException)
    {
        return true;
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Threw {exception.GetType().Name}");
        return false;
    }
}

static void SetDenied(string folder, bool denied)
{
    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException("Folder Permissions Need Windows");
    }

    // Listing Denied To This Account, Through The Windows Tool
    var account = $"*{System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value}";
    using var process = System.Diagnostics.Process.Start(
        new System.Diagnostics.ProcessStartInfo(
            "icacls",
            denied
                ? $"\"{folder}\" /deny {account}:(OI)(CI)(RD)"
                : $"\"{folder}\" /remove:d {account}"
        )
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        }
    )!;
    process.StandardOutput.ReadToEnd();
    process.WaitForExit();
}

static string FreshSample(string sandbox, string name)
{
    // Its Own Copy, So A Failure Never Touches Another Check
    var root = Path.Combine(sandbox, name);
    CopyDirectory(Path.Combine(SourceDirectory(), "Sample"), root);
    return Path.Combine(root, "76561190000000000", "Multiplayer", "1000000001");
}

static void EditArchive(string savegame, Action<ZipArchive> edit)
{
    using var archive = ZipFile.Open(Path.Combine(savegame, "SaveData.zip"), ZipArchiveMode.Update);
    edit(archive);
}

static string EntryHash(string savegame, string entry)
{
    using var archive = ZipFile.OpenRead(Path.Combine(savegame, "SaveData.zip"));
    using var stream = archive.GetEntry(entry)!.Open();
    return Convert.ToHexString(SHA256.HashData(stream));
}
