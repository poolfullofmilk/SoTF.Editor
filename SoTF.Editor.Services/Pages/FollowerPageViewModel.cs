using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Savegames;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Pages;

public class FollowerPageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly AreaMaskManager _areaManager;
    private readonly GameData _gameData;

    // Every Kelvin And Virginia In The Save, Spawned Copies Included
    private readonly List<FollowerState> _followers = [];
    private readonly Dictionary<int, FollowerState> _selectedByTypeId = [];

    public FollowerPageViewModel(GameData gameData)
    {
        _gameData = gameData;
        _areaManager = gameData.AreaManager;
        SelectFirstOfEachType();
    }

    public FollowerState KelvinState => _selectedByTypeId[KelvinTypeId];
    public FollowerState VirginiaState => _selectedByTypeId[VirginiaTypeId];

    public FollowerState StateOf(int typeId)
    {
        return typeId == KelvinTypeId ? KelvinState : VirginiaState;
    }

    public FollowerState OtherOf(FollowerState follower)
    {
        return follower.TypeId == KelvinTypeId ? VirginiaState : KelvinState;
    }

    public List<FollowerState> CopiesOf(int typeId)
    {
        return [.. _followers.Where(follower => follower.TypeId == typeId)];
    }

    public FollowerState? StateOfActor(int uniqueId)
    {
        return _followers.FirstOrDefault(follower => follower.UniqueId == uniqueId);
    }

    public void Select(FollowerState follower)
    {
        _selectedByTypeId[follower.TypeId] = follower;
    }

    private void SelectFirstOfEachType()
    {
        // A Follower Missing From The Save Gets A Stand In
        foreach (var typeId in new[] { KelvinTypeId, VirginiaTypeId })
        {
            _selectedByTypeId[typeId] =
                _followers.FirstOrDefault(follower => follower.TypeId == typeId)
                ?? BuildFollowerState(_gameData, typeId);
        }
    }

    public static void FillAllBars(FollowerState followerState)
    {
        followerState.Health = 100;
        followerState.Fullness = 100;
        followerState.Hydration = 100;
        followerState.Energy = 100;
        followerState.Affection = 100;
    }

    private static FollowerState BuildFollowerState(GameData gameData, int typeId)
    {
        return new FollowerState(
            typeId,
            gameData.FollowerData.GetOutfits(typeId),
            gameData.FollowerData.GetEquippableItems(typeId, gameData.Items)
        );
    }

    public void Load(Savegame? selectedSavegame)
    {
        _followers.Clear();
        if (selectedSavegame != null)
        {
            LoadFollowerData(selectedSavegame);
        }

        SelectFirstOfEachType();
        LoadDeathFlags(selectedSavegame);
    }

    public static void MoveTo(FollowerState follower, Position target)
    {
        follower.Pos = Teleporter.MoveToPos(target);
    }

    private void LoadDeathFlags(Savegame? savegame)
    {
        // A Dead Follower Can Be Gone From The Actor List
        var gameState = savegame
            ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.GameState);
        var kelvinDead = gameState?["IsRobbyDead"]?.Value<bool>() ?? false;
        var virginiaDead = gameState?["IsVirginiaDead"]?.Value<bool>() ?? false;
        foreach (var follower in _followers.Append(KelvinState).Append(VirginiaState))
        {
            follower.IsMarkedDead = follower.TypeId == KelvinTypeId ? kelvinDead : virginiaDead;
        }
    }

    private void LoadFollowerData(Savegame savegame)
    {
        var saveDataWrapper = savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData);

        if (
            saveDataWrapper?.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
            is not JObject vailWorldSim
        )
        {
            return;
        }

        // One State Per Follower Actor, A Repeated Id Only Once
        var found = new HashSet<int>();
        foreach (var actor in vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
        {
            if (
                actor["TypeId"]?.Value<int>()
                    is not ((KelvinTypeId or VirginiaTypeId) and var typeId)
                || actor["UniqueId"]?.Value<int>() is not { } uniqueId
                || !found.Add(uniqueId)
            )
            {
                continue;
            }

            var followerModel = BuildFollowerState(_gameData, typeId);
            followerModel.UniqueId = uniqueId;
            _followers.Add(followerModel);

            followerModel.State = actor["State"]?.Value<int>() ?? -1;

            if (actor["Position"]?.ToObject<Position>() is { } position)
            {
                // The Graph Mask Says Which Cave The Follower Is In
                followerModel.Pos = position.WithoutOffset(
                    actor["GraphMask"]?.Value<int>() is { } graphMask
                        ? _areaManager.GetAreaForGraphMask(graphMask)
                        : AreaMaskManager.Surface
                );
            }

            if (actor["Stats"] is { } stats)
            {
                followerModel.Health = stats["Health"]?.Value<float>() ?? 0f;
                followerModel.Anger = stats["Anger"]?.Value<float>() ?? 0f;
                followerModel.Fear = stats["Fear"]?.Value<float>() ?? 0f;
                followerModel.Fullness = stats["Fullness"]?.Value<float>() ?? 0f;
                followerModel.Hydration = stats["Hydration"]?.Value<float>() ?? 0f;
                followerModel.Energy = stats["Energy"]?.Value<float>() ?? 0f;
                followerModel.Affection = stats["Affection"]?.Value<float>() ?? 0f;
            }

            if (
                actor["EquippedItems"] is JArray equippedItems
                && equippedItems.ToObject<int[]>() is { } itemIds
            )
            {
                foreach (var itemId in itemIds)
                {
                    if (
                        followerModel.Inventory.FirstOrDefault(item => item.ItemId == itemId)
                        is not { } inventoryItem
                    )
                    {
                        s_logger.Warn(
                            $"Found Unknown Item {itemId} In Follower {typeId} Inventory"
                        );
                        var item = new Item { Id = itemId };
                        followerModel.Inventory.Add(new EquippableItem(item) { Selected = true });
                        continue;
                    }

                    inventoryItem.Selected = true;
                }
            }

            // The Game Leaves Out The Default Outfit
            var outfitId = actor["OutfitId"]?.Value<int>() ?? 0;
            followerModel.Outfit = followerModel.Outfits.FirstOrDefault(outfit =>
                outfit.Id == outfitId
            );
        }

        foreach (
            var influenceMemory in vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>()
        )
        {
            var followerState = influenceMemory["UniqueId"]?.Value<int>() is { } uniqueId
                ? StateOfActor(uniqueId)
                : null;
            followerState?.Influences.AddRange(
                influenceMemory["Influences"]?.ToObject<List<Influence>>() ?? []
            );
        }
    }

    public bool Update(Savegame? savegame)
    {
        if (
            savegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            is not { } saveDataWrapper
        )
        {
            return false;
        }

        return new FollowerModifier(saveDataWrapper).Update(_followers);
    }
}
