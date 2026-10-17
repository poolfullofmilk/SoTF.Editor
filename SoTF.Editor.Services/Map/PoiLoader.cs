using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.Map.Static;
using SoTF.Editor.Data.Resources;
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
        return [.. LoadRawPois(inventoryItems), LoadItemPois(inventoryItems)];
    }

    private HashSet<int> GetInventoryItems()
    {
        // A Weapon Mod Counts As Owned, Found By Its Hash
        var itemIdsByModHash = new Dictionary<int, int>();
        foreach (var item in _items)
        {
            foreach (var modHash in item.ModHashes ?? [])
            {
                itemIdsByModHash.TryAdd(modHash, item.Id);
            }
        }

        var inventoryItems = new HashSet<int>();
        foreach (var item in inventoryPageViewModel.Inventory)
        {
            inventoryItems.Add(item.Id);
            var modules = item.ItemBlock.Modules is { Count: > 0 } equippedModules
                ? equippedModules
                : item
                    .ItemBlock.UniqueItems.Select(uniqueItem => uniqueItem["Modules"])
                    .OfType<JArray>()
                    .SelectMany(uniqueModules => uniqueModules);
            foreach (
                var weaponModId in modules
                    .Select(module => module["WeaponModIds"])
                    .OfType<JArray>()
                    .SelectMany(weaponModIds => weaponModIds)
            )
            {
                if (itemIdsByModHash.TryGetValue(weaponModId.Value<int>(), out var modItemId))
                {
                    inventoryItems.Add(modItemId);
                }
            }
        }

        // A Worn Rebreather Counts As Owned
        inventoryItems.UnionWith(playerPageViewModel.WornItemIds);
        return inventoryItems;
    }

    private PoiGroupCollection LoadItemPois(HashSet<int> inventoryItems)
    {
        var rawPoiCollection =
            SaveJson.DeserializeFromString<RawItemPoiCollection>(ResourceFile.Read("ItemPois.json"))
            ?? throw new InvalidDataException("Found No Item Points");

        var groups = rawPoiCollection
            .Items.SelectMany(pair =>
                pair.Value.Pois.Select(poi =>
                    ItemPoi.Of(pair.Key, pair.Value, poi, _items, inventoryItems, _areaMaskManager)
                )
            )
            .OfType<ItemPoi>()
            .GroupBy(poi => poi.Item.Item.Type)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var pois = group.ToList();
                var isEnabled = rawPoiCollection.DefaultEnabledGroups.Contains(group.Key);
                pois.ForEach(poi => poi.Enabled = isEnabled);
                return new PoiGroup(
                    isEnabled,
                    pois,
                    AppText.Get(TextKeys.ItemType(group.Key)),
                    PoiGroupKeys.Items + group.Key,
                    PoiGroupType.Items,
                    rawPoiCollection
                        .OverridingTypeIcons.GetValueOrDefault(group.Key)
                        ?.TrimStart('/')
                        ?? pois[0].Icon
                );
            })
            .ToList();

        return new PoiGroupCollection(
            false,
            AppText.Get("PoiGroupItems"),
            PoiGroupKeys.Items,
            groups,
            PoiGroupType.Items
        );
    }

    private IEnumerable<PoiGroup> LoadRawPois(HashSet<int> inventoryItems)
    {
        var rawPois =
            SaveJson.DeserializeFromString<Dictionary<string, RawPoiGroup>>(
                ResourceFile.Read("Pois.json")
            ) ?? [];

        // Every Group In Pois Json Is A Static Place
        foreach (var group in rawPois.Values)
        {
            var isCaveOrBunker = group.Type is PoiGroupType.Bunkers or PoiGroupType.Caves;
            var pois = group
                .Pois.Select(poi =>
                    isCaveOrBunker
                        ? new CaveOrBunkerPoi(
                            poi,
                            _items,
                            group.Icon!,
                            inventoryItems,
                            _areaMaskManager
                        )
                        : new DefaultGenericInformationalPoi(
                            poi,
                            _items,
                            group.Icon!,
                            inventoryItems,
                            _areaMaskManager
                        )
                )
                .ToList();
            pois.ForEach(poi => poi.Enabled = group.AlwaysEnabled);
            yield return new PoiGroup(
                group.AlwaysEnabled,
                pois,
                AppText.Get(TextKeys.PoiGroup(group.Type)),
                group.Type,
                pois[0].Icon
            );
        }
    }
}
