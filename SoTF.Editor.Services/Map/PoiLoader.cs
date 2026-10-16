using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.Map.Static;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Map;

public class PoiLoader(
    GameData gameData,
    InventoryPageViewModel inventoryPageViewModel,
    PlayerPageViewModel playerPageViewModel
)
{
    private readonly AreaMaskManager _areaMaskManager = gameData.AreaManager;
    private readonly ItemList _items = gameData.Items;

    public IEnumerable<IPoiGrouper> Load()
    {
        var inventoryItems = GetInventoryItems();
        var rawPois = LoadRawPois(inventoryItems);

        return [.. rawPois, LoadItemPois(inventoryItems)];
    }

    private HashSet<int> GetInventoryItems()
    {
        var itemsWithHashes = _items.GetItemsWithHashes();

        var inventoryItems = inventoryPageViewModel
            .Inventory.SelectMany(item =>
            {
                var itemIds = new HashSet<int> { item.Id };
                AddItemIdsFromWeaponMods(item, itemIds, itemsWithHashes);

                return itemIds;
            })
            .ToHashSet();

        if (playerPageViewModel.PlayerState.SelectedCloth is { } selectedCloth)
        {
            inventoryItems.Add(selectedCloth.Id);
        }

        return inventoryItems;
    }

    private static void AddItemIdsFromWeaponMods(
        InventoryItem item,
        HashSet<int> itemIds,
        IReadOnlyCollection<Item> itemsWithHashes
    )
    {
        List<JToken> modules;

        if (item.ItemBlock.Modules is { Count: > 0 } equippedModules)
        {
            modules = equippedModules;
        }
        else if (item.ItemBlock.UniqueItems is { Count: > 0 } uniqueItems)
        {
            modules = [];
            foreach (var uniqueItem in uniqueItems)
            {
                if (uniqueItem["Modules"] is JArray uniqueModules)
                {
                    modules.AddRange(uniqueModules);
                }
            }
        }
        else
        {
            return;
        }

        foreach (var module in modules)
        {
            if (module["WeaponModIds"] is not JArray weaponModIds)
            {
                continue;
            }

            foreach (var weaponModId in weaponModIds)
            {
                var modItemId = weaponModId.Value<int>();

                foreach (
                    var itemId in from itemWithHashes in itemsWithHashes
                    where itemWithHashes.ModHashes?.Contains(modItemId) ?? false
                    select itemWithHashes.Id
                )
                {
                    itemIds.Add(itemId);
                    break;
                }
            }
        }
    }

    private PoiGroupCollection LoadItemPois(HashSet<int> inventoryItems)
    {
        var rawPoiCollection =
            SaveJson.DeserializeFromString<RawItemPoiCollection>(ResourceFile.Read("ItemPois.json"))
            ?? throw new InvalidDataException("Found No Item Points");

        var poisByType = rawPoiCollection
            .Items.SelectMany(pair =>
                pair.Value.Pois.Select(poi =>
                    ItemPoi.Of(
                        pair.Key,
                        pair.Value,
                        poi,
                        _items,
                        inventoryItems,
                        _areaMaskManager,
                        false
                    )
                )
            )
            .Where(poi => poi != null)
            .Select(poi => poi!)
            .GroupBy(poi => poi.Item.Item.Type)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.ToList());

        var groups = new List<PoiGroup>();

        foreach (var (type, pois) in poisByType)
        {
            var icon =
                rawPoiCollection.OverridingTypeIcons.GetValueOrDefault(type)?.TrimStart('/')
                ?? pois.First().Icon;

            var isEnabled = rawPoiCollection.DefaultEnabledGroups.Contains(type);
            if (isEnabled)
            {
                pois.ForEach(poi => poi.Enabled = isEnabled);
            }

            groups.Add(
                new PoiGroup(
                    isEnabled,
                    pois,
                    AppText.Get(TextKeys.ItemType(type)),
                    PoiGroupKeys.Items + type,
                    PoiGroupType.Items,
                    icon
                )
            );
        }

        return new PoiGroupCollection(
            false,
            AppText.Get("PoiGroupItems"),
            PoiGroupKeys.Items,
            groups,
            PoiGroupType.Items
        );
    }

    private List<IPoiGrouper> LoadRawPois(HashSet<int> inventoryItems)
    {
        var result = new List<IPoiGrouper>();

        var rawPois =
            SaveJson.DeserializeFromString<Dictionary<string, RawPoiGroup>>(
                ResourceFile.Read("Pois.json")
            ) ?? [];

        foreach (var (_, group) in rawPois)
        {
            var title = AppText.Get(TextKeys.PoiGroup(group.Type));

            switch (group.Type)
            {
                case PoiGroupType.Bunkers:
                case PoiGroupType.Caves:
                    var caveOrBunkerPois = group
                        .Pois.Select(poi =>
                            CaveOrBunkerPoi.Of(
                                poi,
                                _items,
                                group.Icon!,
                                inventoryItems,
                                _areaMaskManager,
                                group.AlwaysEnabled
                            )
                        )
                        .ToList();
                    result.Add(
                        new PoiGroup(
                            group.AlwaysEnabled,
                            caveOrBunkerPois,
                            title,
                            group.Type,
                            caveOrBunkerPois.First().Icon
                        )
                    );
                    break;
                case PoiGroupType.Printers:
                case PoiGroupType.Laptops:
                case PoiGroupType.Camps:
                case PoiGroupType.Villages:
                case PoiGroupType.Helicopters:
                case PoiGroupType.Info:
                case PoiGroupType.Doors:
                case PoiGroupType.Crates:
                case PoiGroupType.Supply:
                case PoiGroupType.Ammo:
                case PoiGroupType.CannibalVillages:
                case PoiGroupType.Ponds:
                case PoiGroupType.Lakes:
                    var informationalPois = group
                        .Pois.Select(poi =>
                            DefaultGenericInformationalPoi.Of(
                                poi,
                                _items,
                                group.Icon!,
                                inventoryItems,
                                _areaMaskManager,
                                group.AlwaysEnabled
                            )
                        )
                        .ToList();
                    result.Add(
                        new PoiGroup(
                            group.AlwaysEnabled,
                            informationalPois,
                            title,
                            group.Type,
                            informationalPois.First().Icon
                        )
                    );
                    break;
                case PoiGroupType.Generic:
                case PoiGroupType.Items:
                case PoiGroupType.Actors:
                case PoiGroupType.WorldItems:
                case PoiGroupType.Structures:
                case PoiGroupType.ZipLines:
                case PoiGroupType.Player:
                case PoiGroupType.Followers:
                default:
                    continue;
            }
        }

        return result;
    }
}
