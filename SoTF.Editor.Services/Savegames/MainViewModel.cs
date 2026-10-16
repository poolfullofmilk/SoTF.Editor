using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Services.Savegames;

public class MainViewModel(
    PlayerPageViewModel playerPage,
    ArmorPageViewModel armorPage,
    InventoryPageViewModel inventoryPage,
    StoragePageViewModel storagePage,
    FollowerPageViewModel followerPage,
    GameSetupPageViewModel gameSetupPage,
    GameStatePageViewModel gameStatePage,
    WeatherPageViewModel weatherPage,
    NpcsPageViewModel npcsPage,
    StructuresPageViewModel structuresPage,
    MapManager mapManager,
    PoiLoader poiLoader
)
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public Savegame? SelectedSavegame { get; private set; }

    public event Action? Changed;

    public event Action<Savegame>? OpenRequested;

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    #region Savegame Methods
    public void SelectSavegame(Savegame? savegame)
    {
        SelectedSavegame = savegame;

        playerPage.Load(savegame);
        armorPage.Load(savegame);
        inventoryPage.Load(savegame);
        storagePage.Load(savegame);
        followerPage.Load(savegame);
        gameSetupPage.Load(savegame);
        gameStatePage.Load(savegame);
        weatherPage.Load(savegame);
        npcsPage.Load(savegame);
        structuresPage.Load(savegame);

        UserSettings.Current.LastSavegame = savegame?.FullPath ?? "";
        UserSettings.Current.Save();
        NotifyChanged();
    }

    public void RequestOpen(Savegame savegame)
    {
        // The Layout Asks First When Edits Would Be Lost
        if (OpenRequested is { } openRequested)
        {
            openRequested(savegame);
        }
        else
        {
            SelectSavegame(savegame);
        }
    }

    public static bool CanOpenLastSavegame()
    {
        return !string.IsNullOrEmpty(UserSettings.Current.LastSavegame)
            && Directory.Exists(UserSettings.Current.LastSavegame);
    }

    public void OpenLastSavegame()
    {
        // A Deleted Last Savegame Opens Nothing Instead Of Crashing
        if (!CanOpenLastSavegame())
        {
            return;
        }

        var directoryInfo = new DirectoryInfo(UserSettings.Current.LastSavegame);
        SelectSavegame(SavegameManager.CreateSaveInfo(directoryInfo));
    }

    public void ReloadSavegame()
    {
        if (SelectedSavegame is not { } selectedSavegame)
        {
            return;
        }

        SelectSavegame(SavegameManager.ReloadSavegame(selectedSavegame));
    }

    public void ModifyActors(Action<Savegame> modify)
    {
        if (SelectedSavegame is not { } savegame)
        {
            return;
        }

        // Unsaved Follower Edits Go In First, Or Reload Drops Them
        followerPage.Update(savegame);
        modify(savegame);
        followerPage.Load(savegame);
        npcsPage.Load(savegame);
        NotifyChanged();
    }

    public bool HasUnsavedChanges()
    {
        if (SelectedSavegame is not { } selectedSavegame)
        {
            return false;
        }

        // Merging Only Touches The Open Copy, Nothing Reaches The Disk
        var hasChanges = MergePages(selectedSavegame);
        return selectedSavegame.SavegameStore.HasModified() || hasChanges;
    }

    public bool SaveChanges()
    {
        if (SelectedSavegame is not { } selectedSavegame)
        {
            return false;
        }

        var hasChanges = MergePages(selectedSavegame);
        hasChanges = selectedSavegame.SavegameStore.SaveAllModified() || hasChanges;

        if (hasChanges)
        {
            ReloadSavegame();
        }

        return hasChanges;
    }

    private bool MergePages(Savegame selectedSavegame)
    {
        var hasChanges = gameSetupPage.Update(selectedSavegame);
        hasChanges = gameStatePage.Update(selectedSavegame) || hasChanges;
        hasChanges = weatherPage.Update(selectedSavegame) || hasChanges;
        hasChanges = inventoryPage.Update(selectedSavegame) || hasChanges;
        hasChanges = armorPage.Update(selectedSavegame) || hasChanges;
        hasChanges = followerPage.Update(selectedSavegame) || hasChanges;
        hasChanges = playerPage.Update(selectedSavegame) || hasChanges;
        hasChanges = storagePage.Update(selectedSavegame) || hasChanges;
        return structuresPage.Update(selectedSavegame) || hasChanges;
    }

    public void SaveWrappers(Dictionary<SavegameStore.FileType, SaveDataWrapper> wrappersToSave)
    {
        if (SelectedSavegame is not { } selectedSavegame || wrappersToSave.Count == 0)
        {
            return;
        }

        selectedSavegame.SavegameStore.SaveWrappers(wrappersToSave);
        ReloadSavegame();
    }
    #endregion

    #region Tool Methods
    public string ResetCannibalAnger()
    {
        if (
            SelectedSavegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not { } vailWorldSim
        )
        {
            return "";
        }

        foreach (var killStat in vailWorldSim["KillStatsList"] ?? Enumerable.Empty<JToken>())
        {
            killStat["PlayerKilled"]?.Replace(0);
        }

        vailWorldSim["PlayerStats"]?["CutTrees"]?.Replace(0);
        vailWorldSim["PlayerStats"]?["SeenInVillageCount"]?.Replace(0);
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
        return AppText.Get("ChangesAdded");
    }

    public string ResetTraps()
    {
        if (
            SelectedSavegame is not { } selectedSavegame
            || selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.ScrewTrapsSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewTraps) is not { } traps
            || traps["_tarpStructureData"] is not JArray structureData
            || structureData.Count == 0
        )
        {
            return AppText.Get("NothingToReset");
        }

        var countReset = 0;

        foreach (var trapData in structureData)
        {
            if (trapData["IsTriggered"] is { } isTriggeredToken && isTriggeredToken.Value<bool>())
            {
                trapData["IsTriggered"] = false;
                countReset++;
            }

            if (trapData["Data1"] is { } data1Token && data1Token.Value<int>() < 5)
            {
                trapData["Data1"] = 5;
                countReset++;
            }
        }

        if (countReset == 0)
        {
            return AppText.Get("NothingToReset");
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewTraps);
        return AppText.Format("TrapsReset", countReset);
    }

    public string ResetStructureDamage()
    {
        if (
            SelectedSavegame is not { } selectedSavegame
            || selectedSavegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.StructureDestructionSaveData
            )
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.StructureDestruction)
                is not { } structureDestruction
            || structureDestruction["Data"] is not JArray destructionData
        )
        {
            return AppText.Get("NoStructureDamage");
        }

        var entryCount = destructionData.Count;
        destructionData.Clear();
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.StructureDestruction);

        return AppText.Format("StructureDamageRepaired", entryCount);
    }

    public string EternalFires()
    {
        return ChangeFires(true, 65535f, 0.00001f);
    }

    public string ResetFires()
    {
        return ChangeFires(true, 300f, 1.0f);
    }

    private string ChangeFires(bool isLit, float fuel, float fuelDrainRate)
    {
        if (
            SelectedSavegame is not { } selectedSavegame
            || selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.FiresSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.Fires) is not { } fires
            || fires["FiresPerStructureType"] is not { } firesPerStructureType
        )
        {
            return AppText.Get("NoFiresFound");
        }

        var countChanged = 0;

        foreach (var fireType in firesPerStructureType)
        foreach (var firesByType in fireType.Children())
        foreach (var fire in firesByType)
        {
            fire["IsLit"] = isLit;
            fire["Fuel"] = fuel;
            fire["FuelDrainRate"] = fuelDrainRate;
            countChanged++;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.Fires);
        return AppText.Format("FiresChanged", countChanged);
    }

    public string ReviveFollower(int typeId)
    {
        if (SelectedSavegame is null)
        {
            return "";
        }

        var follower =
            typeId == Constants.Actors.KelvinTypeId
                ? followerPage.KelvinState
                : followerPage.VirginiaState;
        var hasChanges = false;
        ModifyActors(savegame =>
            hasChanges = savegame.ReviveFollower(
                typeId,
                follower.GetSelectedInventoryItemIds(),
                follower.Outfit,
                playerPage.PlayerState.Pos
            )
        );

        var actorName = GameNames.Get(TextKeys.ActorType(typeId));
        if (!hasChanges)
        {
            return AppText.Format("FollowerAlreadyAlive", actorName);
        }

        return AppText.Format("FollowerRevived", actorName);
    }

    public string SpawnFollowers(int typeId, int count)
    {
        if (
            SelectedSavegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            is not { } saveDataWrapper
        )
        {
            s_logger.Warn("Failed To Load Save Data");
            return AppText.Get("UnableToSpawnFollowers");
        }

        var follower =
            typeId == Constants.Actors.KelvinTypeId
                ? followerPage.KelvinState
                : followerPage.VirginiaState;
        var hasChanges = false;

        // Through ModifyActors So The NPCs Page And Map Show Them
        ModifyActors(_ =>
            hasChanges = new FollowerModifier(saveDataWrapper).CreateFollowers(
                typeId,
                count,
                follower.GetSelectedInventoryItemIds(),
                follower.Outfit,
                playerPage.PlayerState.Pos
            )
        );

        return hasChanges ? AppText.Format("FollowersCreated", count) : "";
    }
    #endregion

    public List<IPoiGrouper> BuildPoiGroups()
    {
        var actorPoiGroups = mapManager.GetActorPois();

        var structurePoiGroups = mapManager
            .GetStructurePois()
            .Select(keyValuePair => new PoiGroup(
                false,
                keyValuePair.Value,
                keyValuePair.Key,
                PoiGroupKeys.Structures
                    + (
                        keyValuePair
                            .Value.FirstOrDefault()
                            ?.ScrewStructureWrapper.ScrewStructure?.Id.ToString()
                        ?? ""
                    ),
                PoiGroupType.Structures,
                keyValuePair.Value.First().Icon
            ))
            .ToList();

        var poiGroups = new List<IPoiGrouper>
        {
            PoiGroupCollection.ForActors(actorPoiGroups),
            new PoiGroupCollection(
                false,
                AppText.Get("Structures"),
                PoiGroupKeys.Structures,
                structurePoiGroups,
                PoiGroupType.Structures
            ),
        };

        if (SelectedSavegame is { } selectedSavegame)
        {
            poiGroups.AddRange(
                MapManager
                    .GetWorldItemPois(selectedSavegame)
                    .Select(keyValuePair => new PoiGroup(
                        false,
                        keyValuePair.Value,
                        keyValuePair.Key,
                        PoiGroupKeys.WorldItems + keyValuePair.Key,
                        PoiGroupType.WorldItems,
                        keyValuePair.Value.First().Icon
                    ))
            );

            var ziplinePois = MapManager.GetZiplinePois(selectedSavegame);

            if (ziplinePois.Count > 0)
            {
                var zipPois = new List<IPoi>();

                foreach (var ziplinePoi in ziplinePois)
                {
                    zipPois.Add(ziplinePoi.PointA);
                    zipPois.Add(ziplinePoi.PointB);
                    zipPois.Add(ziplinePoi);
                }

                poiGroups.Add(
                    new PoiGroup(
                        false,
                        zipPois,
                        AppText.Get("Ziplines"),
                        PoiGroupType.ZipLines,
                        "images/icons/" + ZipPointPoi.IconFile
                    )
                );
            }
        }

        poiGroups.AddRange(poiLoader.Load());

        poiGroups.Add(
            new PoiGroup(
                true,
                BuildPlayerPois(),
                AppText.Get("Player"),
                PoiGroupType.Player,
                "images/icons/" + PlayerPoi.IconFile
            )
        );

        return [.. poiGroups.OrderBy(group => group.Title)];
    }

    private List<IPoi> BuildPlayerPois()
    {
        return [new PlayerPoi(playerPage.PlayerState.Pos) { Enabled = true }];
    }
}
