using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public sealed class WorldItemClonerViewModel(Savegame savegame)
{
    // Items That Clone, By The Item Id A Clone Carries
    private static readonly Dictionary<int, WorldItemType> s_typesByItemId = new()
    {
        [626] = WorldItemType.HangGlider,
        [630] = WorldItemType.KnightV,
        [590] = WorldItemType.Radio,
    };

    private static readonly Dictionary<string, WorldItemType> s_typesByObjectName = new()
    {
        ["GolfCart"] = WorldItemType.GolfCart,
        ["HangGlider"] = WorldItemType.HangGlider,
        ["KnightV"] = WorldItemType.KnightV,
    };

    public static IEnumerable<WorldItemType> CloneableTypes => s_typesByItemId.Values;

    public WorldItemType? SelectedWorldItemType { get; set; }

    public static List<WorldItemState> Load(Savegame savegame)
    {
        if (WorldItemStatesOf(savegame) is not (_, var worldItemStates))
        {
            return [];
        }

        var result = new List<WorldItemState>();
        var worldItemTypeCounts = new Dictionary<WorldItemType, int>();
        foreach (var worldItemState in worldItemStates)
        {
            if (worldItemState["Position"]?.ToObject<Position>() is not { } position)
            {
                continue;
            }

            var worldItemType = TypeOf(worldItemState);
            var group =
                worldItemType == WorldItemType.Unknown
                    ? AppText.Format(
                        "UnknownWorldItem",
                        worldItemState["ObjectNameId"]?.Value<string>()
                            ?? worldItemState["ItemId"]?.Value<int>().ToString()
                            ?? ""
                    )
                    : GameNames.Get(TextKeys.WorldItem(worldItemType));
            worldItemTypeCounts[worldItemType] =
                worldItemTypeCounts.GetValueOrDefault(worldItemType) + 1;
            result.Add(
                new WorldItemState(
                    $"{group} #{worldItemTypeCounts[worldItemType]}",
                    group,
                    position,
                    worldItemType,
                    IsRuntimeCreated(worldItemState)
                )
            );
        }

        return result;
    }

    private static (SaveDataWrapper Wrapper, JArray States)? WorldItemStatesOf(Savegame savegame)
    {
        return
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.WorldItemManagerSaveData)
                is { } saveDataWrapper
            && saveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.WorldItemManager)
                ?["WorldItemStates"]
                is JArray worldItemStates
            ? (saveDataWrapper, worldItemStates)
            : null;
    }

    private static bool IsRuntimeCreated(JToken worldItemState)
    {
        return (worldItemState["RuntimeCreated"]?.Value<bool>() ?? false)
            || (worldItemState["Unnamed"]?.Value<bool>() ?? false);
    }

    private static WorldItemType TypeOf(JToken worldItemState)
    {
        // The Item Id Wins, Else The Second Name Part
        if (
            worldItemState["ItemId"]?.Value<int>() is { } itemId
            && s_typesByItemId.TryGetValue(itemId, out var typeByItemId)
        )
        {
            return typeByItemId;
        }

        return
            worldItemState["ObjectNameId"]?.Value<string>()?.Split('.') is [_, var name, ..]
            && s_typesByObjectName.TryGetValue(name, out var typeByName)
            ? typeByName
            : WorldItemType.Unknown;
    }

    public string RemoveAllOfThisType()
    {
        if (SelectedWorldItemType is not { } selectedType)
        {
            return "";
        }

        if (WorldItemStatesOf(savegame) is not (var saveDataWrapper, var worldItemStates))
        {
            return AppText.Get("NothingToRemove");
        }

        // Only Clones Go, The Original Stays
        var clones = worldItemStates
            .Where(worldItemState =>
                TypeOf(worldItemState) == selectedType && IsRuntimeCreated(worldItemState)
            )
            .ToList();
        if (clones.Count == 0)
        {
            return AppText.Get("NothingToRemove");
        }

        clones.ForEach(clone => clone.Remove());
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.WorldItemManager);
        return AppText.Format(
            "ClonesRemoved",
            clones.Count,
            GameNames.Get(TextKeys.WorldItem(selectedType))
        );
    }

    public string CloneObjectAtPlayerPos(Position playerPos)
    {
        if (!HasModifiableWorldItemTypeSelected())
        {
            return "";
        }

        var selectedType = SelectedWorldItemType!.Value;
        if (WorldItemStatesOf(savegame) is not (var saveDataWrapper, var worldItemStates))
        {
            return AppText.Get("NothingToMove");
        }

        // A Radio Has No Name, Others Get A Fresh One
        var objectNameId = selectedType == WorldItemType.Radio ? "" : Guid.NewGuid().ToString();
        worldItemStates.Add(
            new JObject
            {
                { "ObjectNameId", objectNameId },
                { "ItemId", s_typesByItemId.First(pair => pair.Value == selectedType).Key },
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
            }
        );
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.WorldItemManager);
        return AppText.Format("ObjectCloned", GameNames.Get(TextKeys.WorldItem(selectedType)));
    }

    public bool HasModifiableWorldItemTypeSelected()
    {
        return SelectedWorldItemType is { } selectedType
            && s_typesByItemId.ContainsValue(selectedType);
    }
}
