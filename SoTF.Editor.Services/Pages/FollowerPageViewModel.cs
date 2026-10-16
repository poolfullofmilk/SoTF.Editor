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

    public FollowerPageViewModel(GameData gameData)
    {
        _areaManager = gameData.AreaManager;
        KelvinState = BuildFollowerState(gameData, KelvinTypeId);
        VirginiaState = BuildFollowerState(gameData, VirginiaTypeId);
    }

    public FollowerState KelvinState { get; }
    public FollowerState VirginiaState { get; }

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
        KelvinState.Reset();
        VirginiaState.Reset();

        if (selectedSavegame != null)
        {
            LoadFollowerData(selectedSavegame);
            LoadDeathFlags(selectedSavegame);
        }
    }

    public static void MoveToPlayer(FollowerState follower, Position playerPos)
    {
        follower.Pos = Teleporter.MoveToPos(playerPos);
    }

    public void MoveToKelvin(FollowerState follower)
    {
        follower.Pos = Teleporter.MoveToPos(KelvinState.Pos);
    }

    public void MoveToVirginia(FollowerState follower)
    {
        follower.Pos = Teleporter.MoveToPos(VirginiaState.Pos);
    }

    private void LoadDeathFlags(Savegame savegame)
    {
        // A Dead Follower Can Be Gone From The Actor List
        var gameState = savegame
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.GameState);
        KelvinState.IsMarkedDead = gameState?["IsRobbyDead"]?.Value<bool>() ?? false;
        VirginiaState.IsMarkedDead = gameState?["IsVirginiaDead"]?.Value<bool>() ?? false;
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

        var foundVirginia = false;
        var foundKelvin = false;

        foreach (var actor in vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
        {
            var typeId = actor["TypeId"]?.Value<int>();
            if (typeId is not (KelvinTypeId or VirginiaTypeId))
            {
                continue;
            }

            switch (typeId)
            {
                case KelvinTypeId when foundKelvin:
                case VirginiaTypeId when foundVirginia:
                    continue;
            }

            var followerModel = typeId == KelvinTypeId ? KelvinState : VirginiaState;

            switch (typeId)
            {
                case KelvinTypeId:
                    foundKelvin = true;
                    break;
                case VirginiaTypeId:
                    foundVirginia = true;
                    break;
            }

            if (actor["UniqueId"]?.Value<int>() is { } uniqueId)
            {
                followerModel.UniqueId = uniqueId;
            }

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
                        followerModel.Inventory.Add(
                            new EquippableItem(item, true) { Selected = true }
                        );
                        continue;
                    }

                    inventoryItem.Selected = true;
                }
            }

            if (actor["OutfitId"]?.Value<int>() is { } outfitId)
            {
                followerModel.Outfit = followerModel.Outfits.FirstOrDefault(outfit =>
                    outfit.Id == outfitId
                );
            }
            else
            {
                followerModel.Outfit = followerModel.Outfits.FirstOrDefault(outfit =>
                    outfit.Id == 0
                );
            }
        }

        foreach (
            var influenceMemory in vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>()
        )
        {
            if (influenceMemory["UniqueId"]?.Value<int>() is not { } uniqueId)
            {
                continue;
            }

            FollowerState followerState;

            if (uniqueId == KelvinState.UniqueId)
            {
                followerState = KelvinState;
            }
            else if (uniqueId == VirginiaState.UniqueId)
            {
                followerState = VirginiaState;
            }
            else
            {
                continue;
            }

            foreach (
                var influence in influenceMemory["Influences"]?.ToObject<List<Influence>>()
                    ?? Enumerable.Empty<Influence>()
            )
            {
                followerState.Influences.Add(influence);
            }
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

        var followerModifier = new FollowerModifier(saveDataWrapper);
        return followerModifier.Update([KelvinState, VirginiaState]);
    }
}
