using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.Map.Static;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Services.Map;

public sealed class PoiLoader(
    GameData gameData,
    InventoryPageViewModel inventoryPageViewModel,
    PlayerPageViewModel playerPageViewModel,
    NpcsPageViewModel npcsPageViewModel,
    StructuresPageViewModel structuresPageViewModel
)
{
    private readonly AreaMaskManager _areaMaskManager = gameData.AreaManager;
    private readonly ItemList _items = gameData.Items;

    // Static Points Never Change, Parsed Once Instead Of Every Visit
    private readonly Dictionary<string, RawPoiGroup> _rawPois =
        SaveJson.DeserializeFromString<Dictionary<string, RawPoiGroup>>(
            ResourceFile.Read("Pois.json")
        ) ?? [];
    private readonly RawItemPoiCollection _rawItemPois =
        SaveJson.DeserializeFromString<RawItemPoiCollection>(ResourceFile.Read("ItemPois.json"))
        ?? throw new InvalidDataException("Found No Item Points");

    public IEnumerable<IPoiGrouper> Load(HashSet<int> inventoryItems)
    {
        return [.. LoadRawPois(inventoryItems), LoadItemPois(inventoryItems)];
    }

    public Position? PlacePosition(string title)
    {
        return _rawPois
            .Values.SelectMany(group => group.Pois)
            .FirstOrDefault(poi => poi.Title == title)
            ?.Teleport?.ToPosition(_areaMaskManager);
    }

    public Position? ItemPosition(int itemId)
    {
        return _rawItemPois
            .Items.GetValueOrDefault(itemId)
            ?.Pois.FirstOrDefault()
            ?.Teleport.ToPosition(_areaMaskManager);
    }

    // An Item Lying Where The Description Says
    public Position? ItemPositionAt(string description)
    {
        return _rawItemPois
            .Items.Values.SelectMany(group => group.Pois)
            .FirstOrDefault(poi => poi.Description == description)
            ?.Teleport.ToPosition(_areaMaskManager);
    }

    // Owned Items, The Map Rebuilds When They Change
    public HashSet<int> OwnedItemIds()
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
        var groups = _rawItemPois
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
                return new PoiGroup(
                    false,
                    pois,
                    AppText.Get(TextKeys.ItemType(group.Key)),
                    PoiGroupKeys.Items + group.Key,
                    PoiGroupType.Items,
                    _rawItemPois.OverridingTypeIcons.GetValueOrDefault(group.Key)?.TrimStart('/')
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
        // Every Group In Pois Json Is A Static Place
        foreach (var group in _rawPois.Values)
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
                        : new InformationalPoi(
                            poi,
                            _items,
                            group.Icon!,
                            inventoryItems,
                            _areaMaskManager
                        )
                )
                .ToList();
            yield return new PoiGroup(
                false,
                pois,
                AppText.Get(TextKeys.PoiGroup(group.Type)),
                group.Type,
                pois[0].Icon
            );
        }
    }

    public List<IPoiGrouper> BuildPoiGroups(Savegame? savegame, HashSet<int> ownedItemIds)
    {
        // A Joined Save Has No Actors Or Structures
        var poiGroups = new[]
        {
            new PoiGroupCollection(
                false,
                AppText.Get("Actors"),
                PoiGroupKeys.Actors,
                GetActorPois(),
                PoiGroupType.Actors
            ),
            new PoiGroupCollection(
                false,
                AppText.Get("Structures"),
                PoiGroupKeys.Structures,
                GetStructurePois(),
                PoiGroupType.Structures
            ),
        }
            .Where(collection => collection.PoiGroups.Count > 0)
            .ToList<IPoiGrouper>();

        if (savegame != null)
        {
            poiGroups.AddRange(GetWorldItemPois(savegame));

            var ziplinePois = GetZiplinePois(savegame);
            if (ziplinePois.Count > 0)
            {
                poiGroups.Add(ZiplineGroup(ziplinePois));
            }
        }

        poiGroups.AddRange(Load(ownedItemIds));

        poiGroups.Add(
            new PoiGroup(
                true,
                [new PlayerPoi(playerPageViewModel.PlayerState.Pos) { Enabled = true }],
                AppText.Get("Player"),
                PoiGroupType.Player,
                "images/icons/" + PlayerPoi.IconFile
            )
        );

        return [.. poiGroups.OrderBy(group => group.Title)];
    }

    // Each Line Shows Both Anchors And The Line Between
    public static PoiGroup ZiplineGroup(IEnumerable<ZiplinePoi> ziplines)
    {
        return new PoiGroup(
            false,
            ziplines.SelectMany(zipline => new IPoi[] { zipline.PointA, zipline.PointB, zipline }),
            AppText.Get("Ziplines"),
            PoiGroupType.ZipLines,
            "images/icons/" + ZipPointPoi.IconFile
        );
    }

    private List<PoiGroup> GetActorPois()
    {
        // Followers Show At Start, Other Actors Start Hidden
        var actorPois = npcsPageViewModel
            .AllActors.Select(actor => new ActorPoi(actor)
            {
                Enabled = actor.ActorType?.IsFollower() ?? false,
            })
            .ToList();
        var result = new List<PoiGroup>();
        var followerPois = actorPois
            .Where(poi => poi.Enabled)
            .OrderBy(poi => poi.Actor.ActorType?.Name ?? "???")
            .ToList();
        if (followerPois.Count > 0)
        {
            result.Add(
                new PoiGroup(
                    true,
                    followerPois,
                    AppText.Get("Followers"),
                    PoiGroupKeys.Actors + "_followers",
                    PoiGroupType.Followers,
                    gameData
                        .ActorTypes.FirstOrDefault(type => type.Id == Constants.Actors.KelvinTypeId)
                        ?.IconPath
                )
            );
        }

        result.AddRange(
            actorPois
                .Where(poi => !poi.Enabled)
                .GroupBy(poi => poi.Actor.ActorType ?? new ActorType(-1, "???"))
                .OrderBy(group => group.Key.Name)
                .Select(group => new PoiGroup(
                    false,
                    [.. group],
                    group.Key.Name,
                    PoiGroupKeys.Actors + group.Key.Id,
                    PoiGroupType.Actors,
                    group.Key.IconPath
                ))
        );
        return result;
    }

    private IEnumerable<PoiGroup> GetStructurePois()
    {
        return structuresPageViewModel
            .Structures.Where(wrapper =>
                wrapper.Position != null && (wrapper.ScrewStructure?.ShowOnMap ?? false)
            )
            .Select(structure => new StructurePoi(structure))
            .GroupBy(poi => poi.ScrewStructureWrapper.Name)
            .OrderBy(group => group.Key)
            .Select(group => new PoiGroup(
                false,
                [.. group],
                group.Key,
                PoiGroupKeys.Structures
                    + (group.First().ScrewStructureWrapper.ScrewStructure?.Id.ToString() ?? ""),
                PoiGroupType.Structures,
                group.First().Icon
            ));
    }

    private static IEnumerable<PoiGroup> GetWorldItemPois(Savegame savegame)
    {
        return WorldItemClonerViewModel
            .Load(savegame)
            .Select(state => new WorldItemPoi(state))
            .GroupBy(poi => poi.State.Group)
            .Select(group => new PoiGroup(
                false,
                [.. group],
                group.Key,
                PoiGroupKeys.WorldItems + group.Key,
                PoiGroupType.WorldItems,
                group.First().Icon
            ));
    }

    private static List<ZiplinePoi> GetZiplinePois(Savegame savegame)
    {
        if (
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.ZipLineManagerSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.ZipLineManager)
                ?["Ziplines"]
            is not { } ziplines
        )
        {
            return [];
        }

        return
        [
            .. from token in ziplines
            let posA = token["_anchorAPosition"]?.ToObject<Position>()
            let posB = token["_anchorBPosition"]?.ToObject<Position>()
            where posA != null && posB != null
            select new ZiplinePoi(token, posA, posB),
        ];
    }
}
