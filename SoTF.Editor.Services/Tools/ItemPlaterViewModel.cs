using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public sealed class ItemPlaterViewModel
{
    // Categories The Plater Offers, In Dialog Order
    public static IReadOnlyList<string> PlatableStructureCategories { get; } =
    ["storage", "furniture", "traps", "utility", "gardening", "misc"];

    public static IReadOnlyList<ElementProfileCategory> PlatableConstructionCategories { get; } =
    [
        ElementProfileCategory.Log,
        ElementProfileCategory.LogPlank,
        ElementProfileCategory.Stick,
        ElementProfileCategory.Item,
        ElementProfileCategory.Stone,
    ];

    private const int PlatingModuleId = 8;
    private const int StructurePlatingStateType = 2;

    private readonly HashSet<int> _platableItemIds;
    private readonly HashSet<int> _weaponHolderIds;
    private readonly Dictionary<string, HashSet<int>> _structureIdsByCategory;
    private readonly ILookup<ElementProfileCategory, ElementProfile> _profilesByCategory;

    public ItemPlaterViewModel(GameData gameData)
    {
        _platableItemIds =
        [
            .. gameData.Items.Where(item => item.IsPlatable).Select(item => item.Id),
        ];
        _weaponHolderIds =
        [
            .. gameData
                .ScrewStructures.Where(structure => structure.IsWeaponHolder)
                .Select(structure => structure.Id),
        ];
        _structureIdsByCategory = gameData
            .ScrewStructures.GroupBy(structure => structure.Category, structure => structure.Id)
            .ToDictionary(group => group.Key, group => group.ToHashSet());
        _profilesByCategory = gameData.ElementProfiles.ToLookup(profile => profile.Category);
    }

    public bool ItemsInInventory { get; set; } = true;

    public bool ItemsInWeaponRacks { get; set; } = true;

    public bool FollowerItems { get; set; } = true;

    public HashSet<string> StructureCategories { get; } = [];

    public HashSet<ElementProfileCategory> ConstructionCategories { get; } = [];

    public bool CanModifyPlating()
    {
        return ItemsInInventory
            || ItemsInWeaponRacks
            || FollowerItems
            || StructureCategories.Count > 0
            || ConstructionCategories.Count > 0;
    }

    public PlatingResult AddPlating(Savegame savegame)
    {
        return Apply(savegame, true);
    }

    public PlatingResult RemovePlating(Savegame savegame)
    {
        return Apply(savegame, false);
    }

    private PlatingResult Apply(Savegame savegame, bool plate)
    {
        var changedCount = 0;
        var wrappersToSave = new Dictionary<SavegameStore.FileType, SaveDataWrapper>();

        // Each Part Changes Its Own Document, Saved Only When Touched
        void Change(
            bool selected,
            SavegameStore.FileType fileType,
            string jsonKey,
            Func<JToken, int> change
        )
        {
            if (
                !selected
                || savegame.SavegameStore.LoadJsonRaw(fileType) is not { } saveDataWrapper
                || saveDataWrapper.GetJsonBasedToken(jsonKey) is not { } document
            )
            {
                return;
            }

            var count = change(document);
            if (count == 0)
            {
                return;
            }

            saveDataWrapper.MarkAsModified(jsonKey);
            wrappersToSave.TryAdd(fileType, saveDataWrapper);
            changedCount += count;
        }

        Change(
            ItemsInInventory,
            SavegameStore.FileType.PlayerInventorySaveData,
            Constants.JsonKeys.PlayerInventory,
            inventory =>
                ChangeItems(inventory.SelectToken("EquippedItems"), true, plate)
                + ChangeItems(
                    inventory.SelectToken("ItemInstanceManagerData.ItemBlocks"),
                    false,
                    plate
                )
        );
        Change(
            ItemsInWeaponRacks,
            SavegameStore.FileType.ScrewStructureInstancesSaveData,
            Constants.JsonKeys.ScrewStructureInstances,
            instances => ChangeWeaponRacks(instances, plate)
        );
        Change(
            FollowerItems,
            SavegameStore.FileType.SaveData,
            Constants.JsonKeys.NpcItemInstances,
            npcItems => ChangeFollowerItems(npcItems, plate)
        );
        Change(
            StructureCategories.Count > 0,
            SavegameStore.FileType.ScrewStructureInstancesSaveData,
            Constants.JsonKeys.ScrewStructureInstances,
            instances => ChangeStructures(instances, plate)
        );
        Change(
            ConstructionCategories.Count > 0,
            SavegameStore.FileType.ConstructionsSaveData,
            Constants.JsonKeys.Constructions,
            constructions => ChangeConstructions(constructions, plate)
        );

        return new PlatingResult(
            AppText.Format(plate ? "PlatingAdded" : "PlatingRemoved", changedCount),
            wrappersToSave
        );
    }

    private int ChangeItems(JToken? items, bool isEquipped, bool plate)
    {
        var changedCount = 0;
        foreach (var token in items as JArray ?? [])
        {
            if (
                token["ItemId"]?.Value<int>() is not { } itemId
                || !_platableItemIds.Contains(itemId)
            )
            {
                continue;
            }

            var platingModules = FindPlatingModules(token);
            if (plate && platingModules.Count == 0)
            {
                AddPlatingModule(token, isEquipped);
                changedCount++;
                continue;
            }

            foreach (
                var platingModule in platingModules.Where(module =>
                    (module["IsPlated"]?.Value<bool>() ?? false) != plate
                )
            )
            {
                platingModule["IsPlated"] = plate;
                changedCount++;
            }
        }

        return changedCount;
    }

    private int ChangeWeaponRacks(JToken instances, bool plate)
    {
        var changedCount = 0;
        foreach (
            var storage in (instances["_structures"] as JArray ?? [])
                .Where(structure =>
                    structure["Id"]?.Value<int>() is { } id && _weaponHolderIds.Contains(id)
                )
                .SelectMany(rack => rack["Storages"] as JArray ?? [])
        )
        {
            changedCount += ChangeItems(storage["ItemBlocks"], false, plate);
        }

        return changedCount;
    }

    private int ChangeFollowerItems(JToken npcItemInstances, bool plate)
    {
        var changedCount = 0;
        foreach (var actorData in npcItemInstances["ActorItems"] as JArray ?? [])
        {
            if (actorData["Items"] is JObject items)
            {
                changedCount += ChangeItems(items["ItemBlocks"], false, plate);
            }
        }

        return changedCount;
    }

    private int ChangeStructures(JToken instances, bool plate)
    {
        var selectedIds = StructureCategories
            .SelectMany(category => _structureIdsByCategory.GetValueOrDefault(category, []))
            .ToHashSet();
        var changedCount = 0;
        foreach (var structure in instances["_structures"] as JArray ?? [])
        {
            if (structure["Id"]?.Value<int>() is not { } id || !selectedIds.Contains(id))
            {
                continue;
            }

            if (structure["Modules"] is not JArray modules)
            {
                // Plating Needs Somewhere To Go, Removing Has Nothing To Do
                if (!plate)
                {
                    continue;
                }

                modules = [];
                structure["Modules"] = modules;
            }

            var platingModules = modules
                .Where(module =>
                    module["StateDataTypeId"]?.Value<int>() == StructurePlatingStateType
                )
                .ToList();
            if (plate && platingModules.Count == 0)
            {
                modules.Add(new JObject { ["StateDataTypeId"] = StructurePlatingStateType });
                changedCount++;
            }
            else if (!plate)
            {
                platingModules.ForEach(module => module.Remove());
                changedCount += platingModules.Count;
            }
        }

        return changedCount;
    }

    private int ChangeConstructions(JToken constructions, bool plate)
    {
        // Each Element Swaps Between Its Plain And Its Gold Profile
        var profiles = ConstructionCategories.SelectMany(category => _profilesByCategory[category]);
        var swaps = plate
            ? profiles
                .DistinctBy(profile => profile.Id)
                .ToDictionary(profile => profile.Id, profile => profile.GoldPlatedId)
            : profiles
                .DistinctBy(profile => profile.GoldPlatedId)
                .ToDictionary(profile => profile.GoldPlatedId, profile => profile.Id);
        var changedCount = 0;
        foreach (
            var element in (constructions["Structures"] as JArray ?? [])
                .OfType<JArray>()
                .SelectMany(blocks => blocks)
                .SelectMany(block => block["Elements"] as JArray ?? [])
        )
        {
            if (
                element["ProfileID"]?.Value<int>() is { } profileId
                && swaps.TryGetValue(profileId, out var newProfileId)
            )
            {
                element["ProfileID"] = newProfileId;
                changedCount++;
            }
        }

        return changedCount;
    }

    private static void AddPlatingModule(JToken token, bool isEquipped)
    {
        // Equipped Items Hold Modules Themselves, Others Per Unique Item
        if (isEquipped)
        {
            AddModuleTo(token);
            return;
        }

        if (token["UniqueItems"] is JArray { Count: > 0 } uniqueItems)
        {
            foreach (var uniqueItem in uniqueItems.OfType<JObject>())
            {
                AddModuleTo(uniqueItem);
            }

            return;
        }

        var count = Math.Max(1, token["TotalCount"]?.Value<int>() ?? 1);
        token["UniqueItems"] = new JArray(
            Enumerable
                .Range(0, count)
                .Select(_ => new JObject { ["Modules"] = new JArray { CreatePlatingModule() } })
        );
    }

    private static void AddModuleTo(JToken holder)
    {
        if (holder["Modules"] is JArray modules)
        {
            modules.Add(CreatePlatingModule());
        }
        else
        {
            holder["Modules"] = new JArray { CreatePlatingModule() };
        }
    }

    private static JObject CreatePlatingModule()
    {
        return new JObject
        {
            ["IsPlated"] = true,
            ["Version"] = "0.0.0",
            ["ModuleId"] = PlatingModuleId,
        };
    }

    private static List<JObject> FindPlatingModules(JToken token)
    {
        return
        [
            .. (token["UniqueItems"] as JArray ?? [])
                .OfType<JObject>()
                .Select(uniqueItem => uniqueItem["Modules"])
                .Append(token["Modules"])
                .OfType<JArray>()
                .SelectMany(modules => modules.OfType<JObject>())
                .Where(module => module["ModuleId"]?.Value<int>() == PlatingModuleId),
        ];
    }
}

public sealed record PlatingResult(
    string Message,
    Dictionary<SavegameStore.FileType, SaveDataWrapper> WrappersToSave
);
