using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Services.Map;

public class MapManager(
    NpcsPageViewModel npcsPageViewModel,
    StructuresPageViewModel structuresPageViewModel,
    PlayerPageViewModel playerPageViewModel,
    PoiLoader poiLoader,
    GameData gameData
)
{
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

        poiGroups.AddRange(poiLoader.Load(ownedItemIds));

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
