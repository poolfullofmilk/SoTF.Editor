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
using SoTF.Editor.Services.Pages;

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

    // Bumped Whenever Actors Or The Savegame Itself Change
    public int Revision { get; private set; }

    private readonly HashSet<string> _changedTools = [];

    public event Action? Changed;

    public event Action<Savegame>? OpenRequested;

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

    // Only These Pages Keep Their Edits In Memory Until Save
    public static bool CanRevert(string page)
    {
        return page is "Player" or "Inventory" or "Storage" or "Game" or "Structures";
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
                playerPage.Load(savegame);
                armorPage.Load(savegame);
                break;
            case "Inventory":
                inventoryPage.Load(savegame);
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
        }

        // A Tool Whose Edit Was Reverted Loses Its Dot
        if (!savegame.SavegameStore.HasModified() && !gameStatePage.HasChanges)
        {
            _changedTools.Clear();
        }

        NotifyChanged();
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
            "Tools" => _changedTools.Count > 0,
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
        if (SelectedSavegame is not { } selectedSavegame)
        {
            return false;
        }

        // Only Content That Differs From The Load Is Written
        MergePages(selectedSavegame);
        var hasChanges = selectedSavegame.SavegameStore.SaveAllModified(changes);

        if (hasChanges)
        {
            ReloadSavegame();
        }

        return hasChanges;
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
    // Unsaved Files And Game State, A Tool That Moves Them Changed Something
    public string ToolState()
    {
        return (SelectedSavegame?.SavegameStore.ChangedState() ?? "") + gameStatePage.ValueState;
    }

    public bool IsToolChanged(string tool)
    {
        return _changedTools.Contains(tool);
    }

    public void FinishTool(string tool, string stateBefore)
    {
        if (
            ToolState() != stateBefore
            && (SelectedSavegame?.SavegameStore.HasModified() == true || gameStatePage.HasChanges)
        )
        {
            _changedTools.Add(tool);
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
            if (memory["UniqueId"]?.Value<int>() is { } uniqueId)
            {
                memories.TryAdd(uniqueId, memory);
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
            var wasReset = false;
            if (trapData["IsTriggered"]?.Value<bool>() == true)
            {
                trapData["IsTriggered"] = false;
                wasReset = true;
            }

            if (trapData["Data1"]?.Value<int>() < 5)
            {
                trapData["Data1"] = 5;
                wasReset = true;
            }

            // A Trap Counts Once, However Much Of It Was Reset
            countReset += wasReset ? 1 : 0;
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
        return ChangeFires(65535f, 0.00001f);
    }

    public string ResetFires()
    {
        return ChangeFires(300f, 1.0f);
    }

    private string ChangeFires(float fuel, float fuelDrainRate)
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
        foreach (
            var fire in firesPerStructureType
                .SelectMany(fireType => fireType.Children())
                .SelectMany(fires => fires)
        )
        {
            fire["IsLit"] = true;
            fire["Fuel"] = fuel;
            fire["FuelDrainRate"] = fuelDrainRate;
            countChanged++;
        }

        if (countChanged == 0)
        {
            return AppText.Get("NoFiresFound");
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.Fires);
        return AppText.Format("FiresChanged", countChanged);
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
