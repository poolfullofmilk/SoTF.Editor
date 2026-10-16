using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public class ItemPlaterViewModel
{
    private readonly Dictionary<
        ElementProfileCategory,
        List<ElementProfile>
    > _elementProfileCategories;
    private readonly Dictionary<int, Item> _platableItems;

    private readonly Dictionary<string, HashSet<int>> _screwStructureCategoryToIds;

    private readonly HashSet<int> _weaponHolderScrewStructureIds;

    public bool FollowerItems { get; set; } = true;

    public bool FurnitureStructures { get; set; }

    public bool GardeningStructures { get; set; }

    public bool ItemConstruction { get; set; }

    public bool ItemsInInventory { get; set; } = true;

    public bool ItemsInWeaponRacks { get; set; } = true;

    public bool LogConstruction { get; set; }

    public bool LogPlankConstruction { get; set; }

    public bool MiscStructures { get; set; }

    public bool StickConstruction { get; set; }

    public bool StoneConstruction { get; set; }

    public bool StorageStructures { get; set; }

    public bool TrapsStructures { get; set; }

    public bool UtilityStructures { get; set; }

    public ItemPlaterViewModel(GameData gameData)
    {
        _platableItems = gameData
            .Items.Where(item => item.Value.IsPlatable)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        _weaponHolderScrewStructureIds =
        [
            .. gameData
                .ScrewStructures.Where(screwStructure => screwStructure.IsWeaponHolder)
                .Select(screwStructure => screwStructure.Id),
        ];
        _screwStructureCategoryToIds = gameData
            .ScrewStructures.GroupBy(structure => structure.Category, structure => structure.Id)
            .ToDictionary(ints => ints.Key, ints => ints.ToHashSet());
        _elementProfileCategories = gameData
            .ElementProfiles.GroupBy(profile => profile.Category)
            .ToDictionary(profiles => profiles.Key, profiles => profiles.ToList());
    }

    public bool CanModifyPlating()
    {
        return ItemsInInventory
            || ItemsInWeaponRacks
            || FollowerItems
            || StorageStructures
            || FurnitureStructures
            || TrapsStructures
            || UtilityStructures
            || MiscStructures
            || GardeningStructures
            || LogConstruction
            || LogPlankConstruction
            || StickConstruction
            || StoneConstruction
            || ItemConstruction;
    }

    public PlatingResult AddPlating(Savegame savegame)
    {
        var modifiedCount = 0;

        var wrappersToSave = new Dictionary<SavegameStore.FileType, SaveDataWrapper>();

        if (
            ItemsInInventory
            && GetPlayerInventorySaveDataWrapper(savegame) is { } playerInventorySaveDataWrapper
        )
        {
            var inventoryPlatings = AddPlatingForInventory(playerInventorySaveDataWrapper);
            if (inventoryPlatings > 0)
            {
                wrappersToSave.TryAdd(
                    SavegameStore.FileType.PlayerInventorySaveData,
                    playerInventorySaveDataWrapper
                );
            }

            modifiedCount += inventoryPlatings;
        }

        if (
            ItemsInWeaponRacks
            && GetScrewStructuresSaveDataWrapper(savegame) is { } screwStructuresSaveDataWrapper
        )
        {
            var weaponRackPlatings = AddPlatingInWeaponRacks(screwStructuresSaveDataWrapper);
            if (weaponRackPlatings > 0)
            {
                wrappersToSave.TryAdd(
                    SavegameStore.FileType.ScrewStructureInstancesSaveData,
                    screwStructuresSaveDataWrapper
                );
            }

            modifiedCount += weaponRackPlatings;
        }

        if (FollowerItems && GetVailSaveDataWrapper(savegame) is { } vailSaveDataWrapper)
        {
            var followerItemPlatings = AddPlatingToFollowers(vailSaveDataWrapper);
            if (followerItemPlatings > 0)
            {
                wrappersToSave.TryAdd(SavegameStore.FileType.SaveData, vailSaveDataWrapper);
            }

            modifiedCount += followerItemPlatings;
        }

        if (
            (
                StorageStructures
                || FurnitureStructures
                || TrapsStructures
                || UtilityStructures
                || MiscStructures
                || GardeningStructures
            ) && GetScrewStructuresSaveDataWrapper(savegame) is { } screwStructuresSaveDataWrapper2
        )
        {
            var structurePlatings = AddPlatingToStructures(screwStructuresSaveDataWrapper2);
            if (structurePlatings > 0)
            {
                wrappersToSave.TryAdd(
                    SavegameStore.FileType.ScrewStructureInstancesSaveData,
                    screwStructuresSaveDataWrapper2
                );
            }

            modifiedCount += structurePlatings;
        }

        if (
            (
                LogConstruction
                || LogPlankConstruction
                || StickConstruction
                || StoneConstruction
                || ItemConstruction
            ) && GetConstructionsSaveDataWrapper(savegame) is { } constructionsSaveDataWrapper
        )
        {
            var constructionPlatings = AddPlatingToConstructions(constructionsSaveDataWrapper);
            if (constructionPlatings > 0)
            {
                wrappersToSave.TryAdd(
                    SavegameStore.FileType.ConstructionsSaveData,
                    constructionsSaveDataWrapper
                );
            }

            modifiedCount += constructionPlatings;
        }

        return new PlatingResult(AppText.Format("PlatingAdded", modifiedCount), wrappersToSave);
    }

    private int AddPlatingToConstructions(SaveDataWrapper saveDataWrapper)
    {
        var numPlated = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.Constructions)
                is not { } constructions
            || constructions["Structures"] is not JArray structures
        )
        {
            return numPlated;
        }

        var selectedUnplatedProfilesById = GetSelectedCategoryProfileIds(false);

        foreach (var structure in structures)
        {
            if (structure is not JArray structureBlocks)
            {
                continue;
            }

            foreach (var structureBlock in structureBlocks)
            {
                if (structureBlock["Elements"] is not JArray elements)
                {
                    continue;
                }

                foreach (var element in elements)
                {
                    if (
                        element["ProfileID"]?.Value<int>() is not { } profileId
                        || !selectedUnplatedProfilesById.TryGetValue(profileId, out var profile)
                    )
                    {
                        continue;
                    }

                    numPlated++;
                    element["ProfileID"] = profile.GoldPlatedId;
                }
            }
        }

        if (numPlated > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.Constructions);
        }

        return numPlated;
    }

    private int AddPlatingToStructures(SaveDataWrapper saveDataWrapper)
    {
        var numPlated = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                is not { } screwStructureInstances
            || screwStructureInstances["_structures"] is not JArray structures
        )
        {
            return numPlated;
        }

        var selectedStructureTypeIds = GetSelectedStructureTypeIds();

        foreach (var structure in structures)
        {
            if (
                structure["Id"]?.Value<int>() is not { } id
                || !selectedStructureTypeIds.Contains(id)
            )
            {
                continue;
            }

            if (structure["Modules"] is not JArray modules)
            {
                modules = [];
                structure["Modules"] = modules;
            }

            if (FindStructurePlatingModule(modules) != null)
            {
                continue;
            }

            modules.Add(CreateStructurePlatingModule());
            numPlated++;
        }

        if (numPlated > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return numPlated;
    }

    private Dictionary<int, ElementProfile> GetSelectedCategoryProfileIds(bool byPlatedId)
    {
        var result = new List<ElementProfile>();
        if (LogConstruction)
        {
            result.AddRange(
                _elementProfileCategories.GetValueOrDefault(ElementProfileCategory.Log, [])
            );
        }

        if (LogPlankConstruction)
        {
            result.AddRange(
                _elementProfileCategories.GetValueOrDefault(ElementProfileCategory.LogPlank, [])
            );
        }

        if (StickConstruction)
        {
            result.AddRange(
                _elementProfileCategories.GetValueOrDefault(ElementProfileCategory.Stick, [])
            );
        }

        if (ItemConstruction)
        {
            result.AddRange(
                _elementProfileCategories.GetValueOrDefault(ElementProfileCategory.Item, [])
            );
        }

        if (StoneConstruction)
        {
            result.AddRange(
                _elementProfileCategories.GetValueOrDefault(ElementProfileCategory.Stone, [])
            );
        }

        return result.DistinctBy(KeySelector).ToDictionary(KeySelector);

        int KeySelector(ElementProfile profile)
        {
            return byPlatedId ? profile.GoldPlatedId : profile.Id;
        }
    }

    private HashSet<int> GetSelectedStructureTypeIds()
    {
        var result = new List<int>();
        if (StorageStructures)
        {
            result.AddRange(_screwStructureCategoryToIds.GetValueOrDefault("storage", []));
        }

        if (FurnitureStructures)
        {
            result.AddRange(_screwStructureCategoryToIds.GetValueOrDefault("furniture", []));
        }

        if (TrapsStructures)
        {
            result.AddRange(_screwStructureCategoryToIds.GetValueOrDefault("traps", []));
        }

        if (UtilityStructures)
        {
            result.AddRange(_screwStructureCategoryToIds.GetValueOrDefault("utility", []));
        }

        if (MiscStructures)
        {
            result.AddRange(_screwStructureCategoryToIds.GetValueOrDefault("misc", []));
        }

        if (GardeningStructures)
        {
            result.AddRange(_screwStructureCategoryToIds.GetValueOrDefault("gardening", []));
        }

        return [.. result];
    }

    private static JObject CreateStructurePlatingModule()
    {
        return new JObject { ["StateDataTypeId"] = 2 };
    }

    private static JToken? FindStructurePlatingModule(JArray modules)
    {
        return modules.FirstOrDefault(module => module["StateDataTypeId"]?.Value<int>() == 2);
    }

    private int AddPlatingToFollowers(SaveDataWrapper saveDataWrapper)
    {
        var numPlated = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.NpcItemInstances)
                is not { } npcItemInstances
            || npcItemInstances["ActorItems"] is not JArray actorItems
        )
        {
            return numPlated;
        }

        foreach (var actorData in actorItems)
        {
            if (
                actorData["Items"] is not JObject items
                || items["ItemBlocks"] is not JArray itemBlocks
            )
            {
                continue;
            }

            numPlated += AddPlatings(itemBlocks, false);
        }

        if (numPlated > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.NpcItemInstances);
        }

        return numPlated;
    }

    private int AddPlatingInWeaponRacks(SaveDataWrapper saveDataWrapper)
    {
        var numPlated = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                is not { } screwStructureInstances
            || screwStructureInstances["_structures"] is not JArray structures
        )
        {
            return numPlated;
        }

        var weaponRacks =
            from structure in structures
            let structureId = structure["Id"]?.Value<int>()
            where structureId.HasValue && _weaponHolderScrewStructureIds.Contains(structureId.Value)
            select structure;

        foreach (var weaponRack in weaponRacks)
        {
            if (weaponRack["Storages"] is not JArray storages)
            {
                continue;
            }

            foreach (var storage in storages)
            {
                if (storage["ItemBlocks"] is not JArray itemBlocks)
                {
                    continue;
                }

                numPlated += AddPlatings(itemBlocks, false);
            }
        }

        if (numPlated > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return numPlated;
    }

    private int AddPlatingForInventory(SaveDataWrapper saveDataWrapper)
    {
        var modifiedCount = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)
            is not { } playerInventory
        )
        {
            return modifiedCount;
        }

        if (playerInventory.SelectToken("EquippedItems") is JArray equippedItems)
        {
            modifiedCount += AddPlatings(equippedItems, true);
        }

        if (
            playerInventory.SelectToken("ItemInstanceManagerData.ItemBlocks")
            is JArray inventoryItems
        )
        {
            modifiedCount += AddPlatings(inventoryItems, false);
        }

        if (modifiedCount > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerInventory);
        }

        return modifiedCount;
    }

    private static SaveDataWrapper? GetPlayerInventorySaveDataWrapper(Savegame savegame)
    {
        return savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData);
    }

    private static SaveDataWrapper? GetScrewStructuresSaveDataWrapper(Savegame savegame)
    {
        return savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.ScrewStructureInstancesSaveData
        );
    }

    private static SaveDataWrapper? GetConstructionsSaveDataWrapper(Savegame savegame)
    {
        return savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.ConstructionsSaveData);
    }

    private static SaveDataWrapper? GetVailSaveDataWrapper(Savegame savegame)
    {
        return savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData);
    }

    private int AddPlatings(JArray items, bool isEquippedItems)
    {
        var numPlated = 0;
        foreach (var token in items)
        {
            if (
                token["ItemId"]?.Value<int>() is not { } itemId
                || !_platableItems.ContainsKey(itemId)
            )
            {
                continue;
            }

            if (FindPlatingModules(token) is { Count: > 0 } platingModules)
            {
                foreach (var platingModule in platingModules)
                {
                    var isPlated = platingModule["IsPlated"]?.Value<bool>() ?? false;
                    if (isPlated)
                    {
                        continue;
                    }

                    platingModule["IsPlated"] = true;
                    numPlated++;
                }
            }
            else
            {
                if (isEquippedItems)
                {
                    AddPlatingModuleToEquippedItem(token);
                }
                else
                {
                    AddPlatingModuleToInventoryItem(token);
                }

                numPlated++;
            }
        }

        return numPlated;
    }

    private static void AddPlatingModuleToEquippedItem(JToken token)
    {
        if (token["Modules"] is JArray modules)
        {
            modules.Add(CreatePlatingModule());
        }
        else
        {
            token["Modules"] = new JArray { CreatePlatingModule() };
        }
    }

    private static void AddPlatingModuleToInventoryItem(JToken token)
    {
        // Each Unique Item Carries Its Own Modules, None Is Added
        if (token["UniqueItems"] is JArray { Count: > 0 } uniqueItems)
        {
            foreach (var uniqueItem in uniqueItems.OfType<JObject>())
            {
                if (uniqueItem["Modules"] is JArray modules)
                {
                    modules.Add(CreatePlatingModule());
                }
                else
                {
                    uniqueItem["Modules"] = new JArray { CreatePlatingModule() };
                }
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

    private static JObject CreatePlatingModule()
    {
        return new JObject
        {
            ["IsPlated"] = true,
            ["Version"] = "0.0.0",
            ["ModuleId"] = 8,
        };
    }

    private static List<JObject> FindPlatingModules(JToken token)
    {
        var foundModules = new List<JObject>();

        if (token["UniqueItems"] is JArray uniqueItems)
        {
            foreach (var uniqueItem in uniqueItems)
            {
                if (uniqueItem is not JObject jObject || jObject["Modules"] is not JArray modules)
                {
                    continue;
                }

                foundModules.AddRange(FindPlatingModules(modules));
            }
        }

        if (token["Modules"] is JArray otherModules)
        {
            foundModules.AddRange(FindPlatingModules(otherModules));
        }

        return foundModules;
    }

    private static List<JObject> FindPlatingModules(JArray modules)
    {
        return
        [
            .. modules
                .Where(module =>
                    module is JObject moduleData && moduleData["ModuleId"]?.Value<int>() == 8
                )
                .Cast<JObject>(),
        ];
    }

    public PlatingResult RemovePlating(Savegame savegame)
    {
        var modifiedCount = 0;

        var wrappersToSave = new Dictionary<SavegameStore.FileType, SaveDataWrapper>();

        if (
            ItemsInInventory
            && GetPlayerInventorySaveDataWrapper(savegame) is { } playerInventorySaveDataWrapper
        )
        {
            var inventoryPlatingsRemoved = RemovePlatingFromInventoryItems(
                playerInventorySaveDataWrapper
            );
            if (inventoryPlatingsRemoved > 0)
            {
                wrappersToSave.Add(
                    SavegameStore.FileType.PlayerInventorySaveData,
                    playerInventorySaveDataWrapper
                );
            }

            modifiedCount += inventoryPlatingsRemoved;
        }

        if (
            ItemsInWeaponRacks
            && GetScrewStructuresSaveDataWrapper(savegame) is { } screwStructuresSaveDataWrapper
        )
        {
            var weaponRackPlatingsRemoved = RemovePlatingFromWeaponRacks(
                screwStructuresSaveDataWrapper
            );
            if (weaponRackPlatingsRemoved > 0)
            {
                wrappersToSave.Add(
                    SavegameStore.FileType.ScrewStructureInstancesSaveData,
                    screwStructuresSaveDataWrapper
                );
            }

            modifiedCount += weaponRackPlatingsRemoved;
        }

        if (FollowerItems && GetVailSaveDataWrapper(savegame) is { } vailSaveDataWrapper)
        {
            var followerPlatingsRemoved = RemovePlatingsFromFollowers(vailSaveDataWrapper);
            if (followerPlatingsRemoved > 0)
            {
                wrappersToSave.Add(SavegameStore.FileType.SaveData, vailSaveDataWrapper);
            }

            modifiedCount += followerPlatingsRemoved;
        }

        if (
            (
                StorageStructures
                || FurnitureStructures
                || TrapsStructures
                || UtilityStructures
                || MiscStructures
                || GardeningStructures
            ) && GetScrewStructuresSaveDataWrapper(savegame) is { } screwStructuresSaveDataWrapper2
        )
        {
            var structurePlatingsRemoved = RemovePlatingFromStructures(
                screwStructuresSaveDataWrapper2
            );
            if (structurePlatingsRemoved > 0)
            {
                wrappersToSave.TryAdd(
                    SavegameStore.FileType.ScrewStructureInstancesSaveData,
                    screwStructuresSaveDataWrapper2
                );
            }

            modifiedCount += structurePlatingsRemoved;
        }

        if (
            (
                LogConstruction
                || LogPlankConstruction
                || StickConstruction
                || StoneConstruction
                || ItemConstruction
            ) && GetConstructionsSaveDataWrapper(savegame) is { } constructionsSaveDataWrapper
        )
        {
            var constructionPlatingsRemoved = RemovePlatingFromConstructions(
                constructionsSaveDataWrapper
            );
            if (constructionPlatingsRemoved > 0)
            {
                wrappersToSave.TryAdd(
                    SavegameStore.FileType.ConstructionsSaveData,
                    constructionsSaveDataWrapper
                );
            }

            modifiedCount += constructionPlatingsRemoved;
        }

        return new PlatingResult(AppText.Format("PlatingRemoved", modifiedCount), wrappersToSave);
    }

    private int RemovePlatingFromConstructions(SaveDataWrapper saveDataWrapper)
    {
        var modifiedCount = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.Constructions)
                is not { } constructions
            || constructions["Structures"] is not JArray structures
        )
        {
            return modifiedCount;
        }

        var selectedProfiles = GetSelectedCategoryProfileIds(true);

        foreach (var structure in structures)
        {
            if (structure is not JArray structureBlocks)
            {
                continue;
            }

            foreach (var structureBlock in structureBlocks)
            {
                if (structureBlock["Elements"] is not JArray elements)
                {
                    continue;
                }

                foreach (var element in elements)
                {
                    if (
                        element["ProfileID"]?.Value<int>() is not { } profileId
                        || !selectedProfiles.TryGetValue(profileId, out var profile)
                    )
                    {
                        continue;
                    }

                    modifiedCount++;
                    element["ProfileID"] = profile.Id;
                }
            }
        }

        if (modifiedCount > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.Constructions);
        }

        return modifiedCount;
    }

    private int RemovePlatingFromStructures(SaveDataWrapper saveDataWrapper)
    {
        var modifiedCount = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                is not { } screwStructureInstances
            || screwStructureInstances["_structures"] is not JArray structures
        )
        {
            return modifiedCount;
        }

        var selectedStructureTypeIds = GetSelectedStructureTypeIds();

        foreach (var structure in structures)
        {
            if (
                structure["Id"]?.Value<int>() is not { } id
                || !selectedStructureTypeIds.Contains(id)
            )
            {
                continue;
            }

            if (structure["Modules"] is not JArray modules)
            {
                continue;
            }

            structure["Modules"] = RemoveStructurePlatingModule(modules, out var countRemoved);

            modifiedCount += countRemoved;
        }

        if (modifiedCount > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return modifiedCount;
    }

    private static JArray RemoveStructurePlatingModule(JArray modules, out int countRemoved)
    {
        countRemoved = 0;

        if (modules.Count == 0)
        {
            return modules;
        }

        var newArray = new JArray();

        foreach (var module in modules)
        {
            if (module["StateDataTypeId"]?.Value<int>() == 2)
            {
                countRemoved++;
                continue;
            }

            newArray.Add(module);
        }

        return newArray;
    }

    private int RemovePlatingsFromFollowers(SaveDataWrapper saveDataWrapper)
    {
        var modifiedCount = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.NpcItemInstances)
                is not { } npcItemInstances
            || npcItemInstances["ActorItems"] is not JArray actorItems
        )
        {
            return modifiedCount;
        }

        foreach (var actorData in actorItems)
        {
            if (
                actorData["Items"] is not JObject items
                || items["ItemBlocks"] is not JArray itemBlocks
            )
            {
                continue;
            }

            modifiedCount += RemovePlatings(itemBlocks);
        }

        if (modifiedCount > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.NpcItemInstances);
        }

        return modifiedCount;
    }

    private int RemovePlatingFromWeaponRacks(SaveDataWrapper saveDataWrapper)
    {
        var modifiedCount = 0;

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                is not { } screwStructureInstances
            || screwStructureInstances["_structures"] is not JArray structures
        )
        {
            return modifiedCount;
        }

        var weaponRacks =
            from structure in structures
            let structureId = structure["Id"]?.Value<int>()
            where structureId.HasValue && _weaponHolderScrewStructureIds.Contains(structureId.Value)
            select structure;

        foreach (var weaponRack in weaponRacks)
        {
            if (weaponRack["Storages"] is not JArray storages)
            {
                continue;
            }

            foreach (var storage in storages)
            {
                if (storage["ItemBlocks"] is not JArray itemBlocks)
                {
                    continue;
                }

                modifiedCount += RemovePlatings(itemBlocks);
            }
        }

        if (modifiedCount > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return modifiedCount;
    }

    private int RemovePlatingFromInventoryItems(SaveDataWrapper saveDataWrapper)
    {
        var modifiedCount = 0;
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)
            is not { } playerInventory
        )
        {
            return modifiedCount;
        }

        if (playerInventory.SelectToken("EquippedItems") is JArray equippedItems)
        {
            modifiedCount += RemovePlatings(equippedItems);
        }

        if (
            playerInventory.SelectToken("ItemInstanceManagerData.ItemBlocks")
            is JArray inventoryItems
        )
        {
            modifiedCount += RemovePlatings(inventoryItems);
        }

        if (modifiedCount > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerInventory);
        }

        return modifiedCount;
    }

    private int RemovePlatings(JArray items)
    {
        var numPlatingRemoved = 0;
        foreach (var token in items)
        {
            if (
                token["ItemId"]?.Value<int>() is not { } itemId
                || !_platableItems.ContainsKey(itemId)
            )
            {
                continue;
            }

            foreach (var platingModule in FindPlatingModules(token))
            {
                var isPlated = platingModule["IsPlated"]?.Value<bool>() ?? false;
                if (!isPlated)
                {
                    continue;
                }

                platingModule["IsPlated"] = false;
                numPlatingRemoved++;
            }
        }

        return numPlatingRemoved;
    }
}

public record PlatingResult(
    string Message,
    Dictionary<SavegameStore.FileType, SaveDataWrapper> WrappersToSave
);
