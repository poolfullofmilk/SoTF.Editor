using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public class WorldItemTeleporterViewModel(Savegame savegame)
{
    private static readonly Dictionary<int, WorldItemType> s_itemIdsToWorldItemTypes = new()
    {
        { 626, WorldItemType.HangGlider },
        { 630, WorldItemType.KnightV },
        { 590, WorldItemType.Radio },
    };

    private static readonly Dictionary<string, WorldItemType> s_objectNameIdToItemTypes = new()
    {
        { "GolfCart", WorldItemType.GolfCart },
        { "HangGlider", WorldItemType.HangGlider },
        { "KnightV", WorldItemType.KnightV },
    };

    public WorldItemType? SelectedWorldItemType { get; set; }

    public static IEnumerable<WorldItemState> Load(Savegame savegame, bool includeRuntimeCreated)
    {
        var result = new List<WorldItemState>();

        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.WorldItemManagerSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.WorldItemManager)
                is not { } worldItemManager
            || worldItemManager["WorldItemStates"] is not JArray worldItemStates
        )
        {
            return result;
        }

        var worldItemTypeCounts = new Dictionary<WorldItemType, int>();

        foreach (var worldItemState in worldItemStates)
        {
            var isRuntimeCreated = IsRuntimeCreated(worldItemState);

            if (
                (isRuntimeCreated && !includeRuntimeCreated)
                || worldItemState["Position"]?.ToObject<Position>() is not { } position
            )
            {
                continue;
            }

            var itemId = worldItemState["ItemId"]?.Value<int>();
            var objectNameId = worldItemState["ObjectNameId"]?.Value<string>();
            var worldItemType = GetWorldItemType(itemId, objectNameId);
            var group =
                worldItemType == WorldItemType.Unknown
                    ? AppText.Format("UnknownWorldItem", objectNameId ?? itemId?.ToString() ?? "")
                    : GameNames.Get(TextKeys.WorldItem(worldItemType));

            worldItemTypeCounts[worldItemType] =
                worldItemTypeCounts.GetValueOrDefault(worldItemType) + 1;
            var objectName = $"{group} ({worldItemTypeCounts[worldItemType]})";

            result.Add(
                new WorldItemState(objectName, group, position, worldItemType, isRuntimeCreated)
            );
        }

        return result;
    }

    private static bool IsRuntimeCreated(JToken worldItemState)
    {
        return (worldItemState["RuntimeCreated"]?.Value<bool>() ?? false)
            || (worldItemState["Unnamed"]?.Value<bool>() ?? false); //only for legacy reasons, has been replaced by RuntimeCreated
    }

    private static WorldItemType GetWorldItemType(int? itemId, string? objectNameId)
    {
        if (itemId is { } id && s_itemIdsToWorldItemTypes.TryGetValue(id, out var itemTypeByItemId))
        {
            return itemTypeByItemId;
        }

        if (string.IsNullOrWhiteSpace(objectNameId))
        {
            return WorldItemType.Unknown;
        }

        var objectNameParts = objectNameId.Split('.');
        if (
            objectNameParts.Length >= 2
            && s_objectNameIdToItemTypes.TryGetValue(
                objectNameParts[1],
                out var itemTybeByObjectNameId
            )
        )
        {
            return itemTybeByObjectNameId;
        }

        return WorldItemType.Unknown;
    }

    public string RemoveAllOfThisType()
    {
        if (SelectedWorldItemType is not { } selectedWorldItemType)
        {
            return "";
        }

        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.WorldItemManagerSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.WorldItemManager)
                is not { } worldItemManager
            || worldItemManager["WorldItemStates"] is not JArray worldItemStates
        )
        {
            return AppText.Get("NothingToRemove");
        }

        var toRemove = worldItemStates
            .Where(worldItemState => WorldItemHasType(worldItemState, selectedWorldItemType))
            .Where(IsRuntimeCreated)
            .ToList();

        if (toRemove.Count == 0)
        {
            return AppText.Get("NothingToRemove");
        }

        toRemove.ForEach(worldItemState => worldItemState.Remove());
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.WorldItemManager);

        return AppText.Format(
            "ClonesRemoved",
            toRemove.Count,
            GameNames.Get(TextKeys.WorldItem(selectedWorldItemType))
        );
    }

    private static bool WorldItemHasType(JToken worldItemState, WorldItemType requestedType)
    {
        var itemId = worldItemState["ItemId"]?.Value<int>();
        var objectNameId = worldItemState["ObjectNameId"]?.Value<string>();
        var worldItemType = GetWorldItemType(itemId, objectNameId);

        return worldItemType == requestedType;
    }

    public string CloneObjectAtPlayerPos(Position playerPos)
    {
        if (
            SelectedWorldItemType is not { } selectedWorldItemType
            || CreateClone(selectedWorldItemType, playerPos) is not { } clonedWorldItem
        )
        {
            return "";
        }

        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.WorldItemManagerSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.WorldItemManager)
                is not { } worldItemManager
            || worldItemManager["WorldItemStates"] is not JArray worldItemStates
        )
        {
            return AppText.Get("NothingToMove");
        }

        worldItemStates.Add(clonedWorldItem);
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.WorldItemManager);

        var itemName = GameNames.Get(TextKeys.WorldItem(selectedWorldItemType));

        return AppText.Format("ObjectCloned", itemName);
    }

    private static JObject? CreateClone(WorldItemType? selectedWorldItemType, Position playerPos)
    {
        if (selectedWorldItemType is not { } worldItemType || !CanClone(worldItemType))
        {
            return null;
        }

        var itemId = s_itemIdsToWorldItemTypes
            .Where(kvp => kvp.Value == worldItemType)
            .Select(kvp => kvp.Key)
            .FirstOrDefault(-1);

        if (itemId == -1)
        {
            return null;
        }

        return new JObject
        {
            { "ObjectNameId", GetObjectNameId(worldItemType) },
            { "ItemId", itemId },
            { "Position", JToken.FromObject(playerPos) },
            {
                "Rotation",
                new JObject
                {
                    { "x", 0f },
                    { "y", 0f },
                    { "z", 0f },
                }
            },
            { "RuntimeCreated", true },
        };
    }

    private static string GetObjectNameId(WorldItemType worldItemType)
    {
        return worldItemType switch
        {
            WorldItemType.Radio => "",
            _ => Guid.NewGuid().ToString(),
        };
    }

    private bool HasWorldItemTypeSelected()
    {
        return SelectedWorldItemType != null;
    }

    public bool HasModifiableWorldItemTypeSelected()
    {
        return HasWorldItemTypeSelected() && CanClone(SelectedWorldItemType);
    }

    private static bool CanClone(WorldItemType? worldItemType)
    {
        return worldItemType switch
        {
            WorldItemType.HangGlider => true,
            WorldItemType.KnightV => true,
            WorldItemType.Radio => true,
            _ => false,
        };
    }
}
