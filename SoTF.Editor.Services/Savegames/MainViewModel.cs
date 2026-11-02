using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Services.Savegames;

public sealed class MainViewModel(
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
    GameData gameData
)
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    // Every Page Route That Can Hold An Edit
    private static readonly string[] s_routes =
    [
        "Player",
        "Inventory",
        "Storage",
        "Followers",
        "Game",
        "Npcs",
        "Structures",
        "Map",
        "Tools",
    ];

    public Savegame? SelectedSavegame { get; private set; }

    // Set By The Layout Watch Every Two Seconds
    public bool IsGameRunning { get; set; }

    // Bumped Whenever Actors Or The Savegame Itself Change
    public int Revision { get; private set; }

    private readonly HashSet<string> _changedTools = [];

    public event Action? Changed;

    public event Action<Savegame>? OpenRequested;

    // A Card's Backups Chip, The Layout Opens And Restores
    public event Action<Savegame>? RestoreRequested;

    public void RequestRestore(Savegame savegame)
    {
        RestoreRequested?.Invoke(savegame);
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    #region Savegame Methods
    public void SelectSavegame(Savegame? savegame)
    {
        try
        {
            Load(savegame);
        }
        catch when (savegame != null)
        {
            // A Half Loaded Savegame Is Closed, Never Left Open
            Load(null);
            throw;
        }
    }

    private void Load(Savegame? savegame)
    {
        SelectedSavegame = savegame;
        Revision++;
        _changedTools.Clear();

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

    public void ReloadSavegame()
    {
        if (SelectedSavegame is not { } selectedSavegame)
        {
            return;
        }

        SelectSavegame(SavegameManager.Load(selectedSavegame.FullPath));
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
        Revision++;
        NotifyChanged();
    }

    public void RevertPage(string page)
    {
        if (SelectedSavegame is not { } savegame)
        {
            return;
        }

        switch (page)
        {
            case "Player":
            case "Inventory":
                // Armour Moved Through The Backpack Goes Back On Both Pages
                var moved = armorPage.MovedThroughInventory;
                if (page == "Player" || moved)
                {
                    playerPage.Load(savegame);
                    armorPage.Load(savegame);
                }

                if (page == "Inventory" || moved)
                {
                    inventoryPage.Load(savegame);
                }

                break;
            case "Storage":
                storagePage.Revert(savegame);
                break;
            case "Game":
                gameSetupPage.Load(savegame);
                gameStatePage.Load(savegame);
                weatherPage.Load(savegame);
                break;
            case "Structures":
                structuresPage.Load(savegame);
                break;
            case "Followers":
                ModifyActors(_ => RevertActors(savegame, true));
                gameStatePage.RefreshDeathFlags(savegame);
                break;
            case "Npcs":
                ModifyActors(_ => RevertActors(savegame, false));
                break;
            case "Map":
                RevertZiplines(savegame);
                Revision++;
                break;
            case "Tools":
                RevertTools(savegame);
                break;
        }

        // A Tool Whose Edit Was Reverted Loses Its Dot
        if (!savegame.SavegameStore.HasModified() && !gameStatePage.HasChanges)
        {
            _changedTools.Clear();
        }

        NotifyChanged();
    }

    private static void RevertActors(Savegame savegame, bool followers)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not { } vailWorldSim
            || saveDataWrapper.LoadedCopy(Constants.JsonKeys.VailWorldSim) is not { } loaded
        )
        {
            return;
        }

        // Followers By Loaded Type, Spawned Ones By Their Type Now
        var loadedIds = IdsOf(loaded["Actors"], _ => true);
        var followerIds = IdsOf(loaded["Actors"], IsFollower)
            .Concat(IdsOf(vailWorldSim["Actors"], IsFollower).Except(loadedIds))
            .ToHashSet();
        bool IsOwned(JToken entry)
        {
            return JsonModifier.UniqueIdOf(entry) is { } uniqueId
                && followerIds.Contains(uniqueId) == followers;
        }

        foreach (var key in new[] { "Actors", "InfluenceMemory" })
        {
            JsonModifier.RestoreEntries(
                vailWorldSim[key],
                loaded[key],
                IsOwned,
                JsonModifier.UniqueIdOf
            );
        }

        JsonModifier.RestoreEntries(
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.NpcItemInstances)?["ActorItems"],
            saveDataWrapper.LoadedCopy(Constants.JsonKeys.NpcItemInstances)?["ActorItems"],
            IsOwned,
            JsonModifier.UniqueIdOf
        );

        if (!followers)
        {
            JsonModifier.RestoreEntries(
                vailWorldSim["Spawners"],
                loaded["Spawners"],
                _ => true,
                JsonModifier.UniqueIdOf
            );
            return;
        }

        // Revive Also Cleared Kills And The Death Flags
        JsonModifier.RestoreEntries(
            vailWorldSim["KillStatsList"],
            loaded["KillStatsList"],
            IsFollower,
            killStat => killStat["TypeId"]?.Value<int>()
        );
        var gameStateWrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.GameStateSaveData
        );
        var gameState = gameStateWrapper?.GetJsonBasedToken(Constants.JsonKeys.GameState);
        var loadedGameState = gameStateWrapper?.LoadedCopy(Constants.JsonKeys.GameState);
        foreach (
            var flag in new[]
            {
                Constants.GameStateKeys.IsRobbyDead,
                Constants.GameStateKeys.IsVirginiaDead,
            }
        )
        {
            if (gameState?[flag] is { } current && loadedGameState?[flag] is { } loadedFlag)
            {
                current.Replace(loadedFlag);
            }
        }
    }

    private static bool IsFollower(JToken entry)
    {
        return entry["TypeId"]?.Value<int>()
            is Constants.Actors.KelvinTypeId
                or Constants.Actors.VirginiaTypeId;
    }

    private static HashSet<int> IdsOf(JToken? actors, Func<JToken, bool> include)
    {
        return
        [
            .. (actors ?? Enumerable.Empty<JToken>())
                .Where(include)
                .Select(JsonModifier.UniqueIdOf)
                .OfType<int>(),
        ];
    }

    private static void RevertZiplines(Savegame savegame)
    {
        // Lines And Their Midpoint Structures, Other Structures Keep Rows
        savegame
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.ZipLineManagerSaveData)
            ?.Revert(Constants.JsonKeys.ZipLineManager);
        var structuresWrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.ScrewStructureInstancesSaveData
        );
        JsonModifier.RestoreEntries(
            structuresWrapper
                ?.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"],
            structuresWrapper
                ?.LoadedCopy(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"],
            structure => structure["Id"]?.Value<int>() == ZiplineManager.ZiplineTypeId
        );
    }

    private void RevertTools(Savegame savegame)
    {
        // Documents Only Tools Change Before A Save
        foreach (
            var (fileType, jsonKey) in new[]
            {
                (SavegameStore.FileType.FiresSaveData, Constants.JsonKeys.Fires),
                (SavegameStore.FileType.ScrewTrapsSaveData, Constants.JsonKeys.ScrewTraps),
                (
                    SavegameStore.FileType.StructureDestructionSaveData,
                    Constants.JsonKeys.StructureDestruction
                ),
                (
                    SavegameStore.FileType.WorldItemManagerSaveData,
                    Constants.JsonKeys.WorldItemManager
                ),
                (
                    SavegameStore.FileType.WorldObjectLocatorManagerSaveData,
                    Constants.JsonKeys.WorldObjectLocatorManager
                ),
                (SavegameStore.FileType.PlayerStateSaveData, Constants.JsonKeys.PlayerState),
                (
                    SavegameStore.FileType.GardenPlotManagerSaveData,
                    Constants.JsonKeys.GardenPlotManager
                ),
                (
                    SavegameStore.FileType.MultiplayerPlayerRolesSaveData,
                    Constants.JsonKeys.MultiplayerPlayerRoles
                ),
            }.Concat(
                WorldTools.Printers.Select(printer => (printer, WorldTools.PrinterKeyOf(printer)))
            )
        )
        {
            savegame.SavegameStore.LoadJsonRaw(fileType)?.Revert(jsonKey);
        }

        // Anger And Game State Tools Share Data, Only When Run
        if (_changedTools.Contains("CannibalAnger"))
        {
            ModifyActors(_ => RevertCannibalAnger(savegame));
        }

        // Only The Rows These Tools Own, Other Game Edits Stay
        if (_changedTools.Contains("ResetContainers"))
        {
            gameStatePage.RevertNamedIntDatas(GameStatePageViewModel.IsContainer);
        }

        if (_changedTools.Contains("SetToEndgame"))
        {
            gameStatePage.RevertNamedIntDatas(GameStatePageViewModel.IsEndgame);
        }

        _changedTools.Clear();
        Revision++;
    }

    private void RevertCannibalAnger(Savegame savegame)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not JObject vailWorldSim
            || saveDataWrapper.LoadedCopy(Constants.JsonKeys.VailWorldSim) is not { } loaded
        )
        {
            return;
        }

        // Kills, World Counts And Cannibal Memories
        var cannibalIds = LivingCannibalIds(vailWorldSim)
            .Concat(LivingCannibalIds(loaded))
            .ToHashSet();
        JsonModifier.RestoreEntries(
            vailWorldSim["InfluenceMemory"],
            loaded["InfluenceMemory"],
            memory =>
                JsonModifier.UniqueIdOf(memory) is { } uniqueId && cannibalIds.Contains(uniqueId),
            JsonModifier.UniqueIdOf
        );
        JsonModifier.RestoreEntries(
            vailWorldSim["KillStatsList"],
            loaded["KillStatsList"],
            _ => true,
            killStat => killStat["TypeId"]?.Value<int>()
        );
        if (loaded["PlayerStats"] is { } playerStats)
        {
            vailWorldSim["PlayerStats"] = playerStats;
        }
    }

    public bool HasChanges(string page)
    {
        // Nothing Loaded Means Nothing Changed
        if (SelectedSavegame is null)
        {
            return false;
        }

        // Pages By Their Route, Compared With The Last Load
        return page switch
        {
            "Player" => playerPage.HasChanges || armorPage.HasChanges,
            "Inventory" => inventoryPage.HasChanges,
            "Storage" => storagePage.HasChanges,
            "Followers" => followerPage.HasChanges,
            "Game" => gameSetupPage.HasChanges
                || gameStatePage.HasChanges
                || weatherPage.HasChanges,
            "Npcs" => npcsPage.HasChanges,
            "Structures" => structuresPage.HasChanges,
            "Map" => SelectedSavegame.SavegameStore.HasModified(
                SavegameStore.FileType.ZipLineManagerSaveData
            ),
            "Tools" => _changedTools.Count > 0
                && (SelectedSavegame.SavegameStore.HasModified() || gameStatePage.HasChanges),
            _ => false,
        };
    }

    public IEnumerable<string> ChangedTools => _changedTools;

    public bool HasUnsavedChanges()
    {
        // Asking Never Merges, Or Revert Would Restore Kept Edits
        return SelectedSavegame is { } selectedSavegame
            && (s_routes.Any(HasChanges) || selectedSavegame.SavegameStore.HasModified());
    }

    public bool SaveChanges(Dictionary<string, List<Change>>? changes = null)
    {
        if (SelectedSavegame is not { } selectedSavegame || !HasUnsavedChanges())
        {
            return false;
        }

        // Pages Merge Into A Draft, Failed Saves Keep The Load
        var draft = selectedSavegame.WithStore(selectedSavegame.SavegameStore.Draft());
        MergePages(draft);

        // Only Real Differences Are Backed Up And Written
        if (!draft.SavegameStore.HasModified())
        {
            s_logger.Info("Found No Changes");
            return false;
        }

        draft.SavegameStore.BackUpLoaded(changes);
        draft.SavegameStore.SaveAllModified();
        ReloadSavegame();
        return true;
    }

    private void MergePages(Savegame selectedSavegame)
    {
        // Pages Mark Changes, The Store Knows What Really Differs
        gameSetupPage.Update(selectedSavegame);
        gameStatePage.Update(selectedSavegame);
        weatherPage.Update(selectedSavegame);
        inventoryPage.Update(selectedSavegame);
        armorPage.Update(selectedSavegame);
        followerPage.Update(selectedSavegame);
        playerPage.Update(selectedSavegame);
        storagePage.Update(selectedSavegame);
        structuresPage.Update(selectedSavegame);
    }

    public void SaveWrappers(
        Dictionary<SavegameStore.FileType, SaveDataWrapper> wrappersToSave,
        Dictionary<string, List<Change>>? changes = null
    )
    {
        if (SelectedSavegame is not { } selectedSavegame || wrappersToSave.Count == 0)
        {
            return;
        }

        selectedSavegame.SavegameStore.SaveWrappers(wrappersToSave, changes);
        ReloadSavegame();
    }
    #endregion

    #region Tool Methods
    // Unsaved Files And Game State, A Tool Moving Them Changed
    public string ToolState()
    {
        return (SelectedSavegame?.SavegameStore.ChangedState() ?? "") + gameStatePage.ValueState;
    }

    public bool IsToolChanged(string tool)
    {
        // A Tool Whose Changes Were All Put Back Shows Clean
        return _changedTools.Contains(tool) && HasChanges("Tools");
    }

    public void FinishTool(string tool, string stateBefore)
    {
        if (ToolState() != stateBefore)
        {
            // A Tool Can Add Or Move What The Map Shows
            Revision++;
            if (SelectedSavegame?.SavegameStore.HasModified() == true || gameStatePage.HasChanges)
            {
                _changedTools.Add(tool);
            }
        }

        NotifyChanged();
    }

    public CannibalMood? ReadCannibalMood()
    {
        if (
            SelectedSavegame is not { } savegame
            || VailWorldSimOf(savegame) is not { } vailWorldSim
        )
        {
            return null;
        }

        var memories = MemoriesById(vailWorldSim);
        var playerInfluences = LivingCannibalIds(vailWorldSim)
            .Select(uniqueId => PlayerInfluenceOf(memories.GetValueOrDefault(uniqueId)))
            .ToList();
        return new CannibalMood(
            playerInfluences.All(influence => influence != null),
            [
                .. playerInfluences
                    .OfType<JToken>()
                    .Select(influence =>
                        (
                            influence["Anger"]?.Value<float>() ?? 0f,
                            influence["Sentiment"]?.Value<float>() ?? 0f
                        )
                    ),
            ],
            WorldCountsOf(vailWorldSim).Any(count => !IsZero(count))
        );
    }

    public string SetCannibalAnger(int level)
    {
        // Through ModifyActors So The NPCs Page Shows The New Anger
        var message = "";
        ModifyActors(savegame => message = SetCannibalAnger(savegame, level));
        return message;
    }

    private string SetCannibalAnger(Savegame savegame, int level)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                is not { } saveDataWrapper
            || VailWorldSimOf(savegame) is not { } vailWorldSim
            || vailWorldSim["InfluenceMemory"] is not JArray influenceMemories
        )
        {
            return "";
        }

        // Exactly That Anger And Its Negative As Sentiment, Strangers Included
        var memories = MemoriesById(vailWorldSim);
        var hasChanges = false;
        foreach (var uniqueId in LivingCannibalIds(vailWorldSim))
        {
            if (!memories.TryGetValue(uniqueId, out var memory))
            {
                memory = new JObject { ["UniqueId"] = uniqueId };
                influenceMemories.Add(memory);
                hasChanges = true;
            }

            if (PlayerInfluenceOf(memory) is not { } playerInfluence)
            {
                if (memory["Influences"] is not JArray influences)
                {
                    influences = [];
                    memory["Influences"] = influences;
                }

                playerInfluence = new JObject { ["TypeId"] = Influence.Type.Player };
                influences.Add(playerInfluence);
                hasChanges = true;
            }

            hasChanges |= JsonModifier.SetFloat(playerInfluence, "Sentiment", -level);
            hasChanges |= JsonModifier.SetFloat(playerInfluence, "Anger", level);
        }

        // Calm Also Forgets Kills, Cut Trees And Village Visits
        if (level == 0)
        {
            foreach (var count in WorldCountsOf(vailWorldSim).Where(count => !IsZero(count)))
            {
                if (count is JObject killedOnDay)
                {
                    killedOnDay.RemoveAll();
                }
                else
                {
                    count.Replace(0);
                }

                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return AppText.Get("NothingToChange");
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
        return AppText.Get("ChangesAdded");
    }

    private static JToken? VailWorldSimOf(Savegame savegame)
    {
        return savegame
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim);
    }

    private IEnumerable<int> LivingCannibalIds(JToken vailWorldSim)
    {
        var cannibalTypeIds = gameData
            .ActorTypes.Where(actorType => actorType.IsCannibal)
            .Select(actorType => actorType.Id)
            .ToHashSet();
        return (vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
            .Where(actor =>
                cannibalTypeIds.Contains(actor["TypeId"]?.Value<int>() ?? -1)
                && actor["State"]?.Value<int>() != Constants.Actors.StateDead
            )
            .Select(actor => actor["UniqueId"]?.Value<int>())
            .OfType<int>();
    }

    private static Dictionary<int, JToken> MemoriesById(JToken vailWorldSim)
    {
        var memories = new Dictionary<int, JToken>();
        foreach (var memory in vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>())
        {
            // The Newest Repeated Memory Wins, As Pages Read It
            if (memory["UniqueId"]?.Value<int>() is { } uniqueId)
            {
                memories[uniqueId] = memory;
            }
        }

        return memories;
    }

    private static JToken? PlayerInfluenceOf(JToken? memory)
    {
        return memory?["Influences"]?.FirstOrDefault(influence =>
            influence["TypeId"]?.ToString() == Influence.Type.Player
        );
    }

    private static IEnumerable<JToken> WorldCountsOf(JToken vailWorldSim)
    {
        // Kills, Kills Per Day, Cut Trees And Village Sightings
        var playerStats = vailWorldSim["PlayerStats"];
        return new[] { playerStats?["CutTrees"], playerStats?["SeenInVillageCount"] }
            .Concat(
                (vailWorldSim["KillStatsList"] ?? Enumerable.Empty<JToken>()).SelectMany(killStat =>
                    new[] { killStat["PlayerKilled"], killStat["KilledOnDay"] }
                )
            )
            .OfType<JToken>();
    }

    private static bool IsZero(JToken count)
    {
        return count is JObject killedOnDay ? killedOnDay.Count == 0 : count.Value<double>() == 0;
    }

    public string ReviveFollower(int typeId)
    {
        if (SelectedSavegame is not { } selectedSavegame)
        {
            return "";
        }

        var follower = followerPage.StateOf(typeId);
        var hasChanges = false;
        ModifyActors(savegame =>
            hasChanges = savegame.ReviveFollower(
                typeId,
                follower.GetSelectedInventoryItemIds(),
                follower.Outfit,
                ArrivalPosition()
            )
        );
        gameStatePage.RefreshDeathFlags(selectedSavegame);

        return AppText.Format(
            hasChanges ? "FollowerRevived" : "FollowerAlreadyAlive",
            GameNames.Get(TextKeys.ActorType(typeId))
        );
    }

    public void RemoveFollower(FollowerState follower)
    {
        if (
            follower.UniqueId is not { } uniqueId
            || SelectedSavegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                is not { } saveDataWrapper
        )
        {
            return;
        }

        ModifyActors(_ => new FollowerModifier(saveDataWrapper).Remove(uniqueId));
    }

    public Position? ArrivalPosition()
    {
        if (!playerPage.PlayerState.Pos.Area.IsUnderground())
        {
            return playerPage.PlayerState.Pos;
        }

        // Underground, Beside A Follower Still On The Surface
        return followerPage
            .CopiesOf(Constants.Actors.KelvinTypeId)
            .Concat(followerPage.CopiesOf(Constants.Actors.VirginiaTypeId))
            .FirstOrDefault(follower =>
                follower.State == Constants.Actors.StateAlive && !follower.Pos.Area.IsUnderground()
            )
            ?.Pos;
    }

    public string SpawnFollower(int typeId)
    {
        if (
            SelectedSavegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            is not { } saveDataWrapper
        )
        {
            s_logger.Warn("Failed To Load Save Data");
            return AppText.Get("UnableToSpawnFollowers");
        }

        if (ArrivalPosition() is not { } arrival)
        {
            return AppText.Get("UnableToSpawnFollowers");
        }

        var follower = followerPage.StateOf(typeId);
        var hasChanges = false;

        // Through ModifyActors So The NPCs Page And Map Show Them
        ModifyActors(_ =>
            hasChanges = new FollowerModifier(saveDataWrapper).CreateFollower(
                typeId,
                follower.GetSelectedInventoryItemIds(),
                follower.Outfit,
                arrival
            )
        );

        if (!hasChanges)
        {
            return "";
        }

        // New Copies Join The End, The Panel Shows The Newest
        followerPage.Select(followerPage.CopiesOf(typeId)[^1]);
        return AppText.Format("FollowerSpawned", GameNames.Get(TextKeys.ActorType(typeId)));
    }
    #endregion
}
