#:project ../SoTF.Editor.Services/SoTF.Editor.Services.csproj
#:package Microsoft.Extensions.DependencyInjection@10.0.14
#:property NoWarn=$(NoWarn);IL2026;IL3050
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
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

// The Sample Save, Copied So The Test Never Touches It
const string SampleSteamId = "76561190000000000";
var work = Path.Combine(Path.GetTempPath(), "SoTF.Editor.Tests", Guid.NewGuid().ToString("N"));
CopyDirectory(Path.Combine(SourceDirectory(), "Sample"), work);
var savegamePath = Path.Combine(work, SampleSteamId, "Multiplayer", "1000000001");
var settingsPath = Path.Combine(AppInfo.DataDirectory, "settings.json");
var settingsBefore = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
var failures = new List<string>();

try
{
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

    var days = SettingOf(gameState.Settings, "GameDays").IntValue ?? 0;
    EditAndCheck(
        "Game State",
        "Game",
        () => SettingOf(gameState.Settings, "GameDays").IntValue = days + 1,
        () => SettingOf(gameState.Settings, "GameDays").IntValue == days + 1
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
    ToolCheck("Eternal Fires", main.EternalFires);
    ToolCheck("Reset Fires", main.ResetFires);
    ToolCheck("Reset Structure Damage", main.ResetStructureDamage);
    ToolCheck("Reset Traps", main.ResetTraps);
    ToolCheck("Reset Containers", gameState.ResetContainers);
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
            ("EternalFires", () => main.EternalFires()),
            ("CannibalAnger", () => main.SetCannibalAnger(45)),
            ("ResetContainers", () => gameState.ResetContainers()),
        }
    )
    {
        var toolBefore = main.ToolState();
        run();
        main.FinishTool(tool, toolBefore);
    }

    Check(main.HasChanges("Tools") && main.HasChanges("Npcs"), "Tool Edits Are Seen");
    main.RevertPage("Tools");
    Check(!main.HasUnsavedChanges(), "Reverted Tools Leave Nothing Unsaved");

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
        (SettingOf(gameState.Settings, "GameDays").IntValue ?? 0)
        + ((SettingOf(gameState.Settings, "GameHours").IntValue ?? 0) / 24.0);
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

    // A Failed Copy Leaves No Backup Behind
    var archivePath = Path.Combine(savegamePath, "SaveData.zip");
    var backupCount = BackupManager.List(savegamePath).Count;
    using (File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Check(Throws(() => BackupManager.Create(savegamePath)), "Locked File Fails The Backup");
    }

    Check(BackupManager.List(savegamePath).Count == backupCount, "Failed Backup Leaves No Folder");

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
    // The Test Leaves No Settings, Backups Or Copies Behind
    if (settingsBefore != null)
    {
        File.WriteAllBytes(settingsPath, settingsBefore);
    }
    else if (File.Exists(settingsPath))
    {
        File.Delete(settingsPath);
    }

    var backups = Path.Combine(BackupManager.BackupDirectory, SampleSteamId);
    if (Directory.Exists(backups))
    {
        Directory.Delete(backups, true);
    }

    Directory.Delete(work, true);
}

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
