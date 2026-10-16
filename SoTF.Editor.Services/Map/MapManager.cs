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
    GameData gameData
)
{
    private readonly List<ActorType> _actorTypes = gameData.ActorTypes;

    public List<PoiGroup> GetActorPois()
    {
        var followerNonFollowerActors = npcsPageViewModel
            .AllActors.Select(actor =>
            {
                var poi = new ActorPoi(actor) { Enabled = actor.ActorType?.IsFollower() ?? false };
                return poi;
            })
            .GroupBy(actorPoi => actorPoi.Actor.ActorType?.IsFollower() ?? false)
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = new List<PoiGroup>();

        if (followerNonFollowerActors.TryGetValue(true, out var followerPois))
        {
            var followerPoisSorted = followerPois
                .OrderBy(followerPoi => followerPoi.Actor.ActorType?.Name ?? "???")
                .ToList();

            var icon = _actorTypes
                .FirstOrDefault(type => type.Id == Constants.Actors.KelvinTypeId)
                ?.IconPath;

            result.Add(
                new PoiGroup(
                    true,
                    followerPoisSorted,
                    AppText.Get("Followers"),
                    PoiGroupKeys.Actors + "_followers",
                    PoiGroupType.Followers,
                    icon
                )
            );
        }

        if (followerNonFollowerActors.TryGetValue(false, out var actorPois))
        {
            var actorPoiGroups = actorPois
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
                .ToList();
            result.AddRange(actorPoiGroups);
        }

        return result;
    }

    public Dictionary<string, List<StructurePoi>> GetStructurePois()
    {
        return structuresPageViewModel
            .Structures.Where(wrapper =>
                wrapper.Position != null && (wrapper.ScrewStructure?.ShowOnMap ?? false)
            )
            .Select(structure => new StructurePoi(structure))
            .GroupBy(poi => poi.ScrewStructureWrapper.Name)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public static Dictionary<string, List<WorldItemPoi>> GetWorldItemPois(Savegame savegame)
    {
        return WorldItemTeleporterViewModel
            .Load(savegame, true)
            .Select(state => new WorldItemPoi(state))
            .GroupBy(poi => poi.State.Group)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public static List<ZiplinePoi> GetZiplinePois(Savegame savegame)
    {
        var result = new List<ZiplinePoi>();

        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.ZipLineManagerSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ZipLineManager)?["Ziplines"]
                is not { } ziplines
        )
        {
            return result;
        }

        result.AddRange(
            from token in ziplines
            let posA = token["_anchorAPosition"]?.ToObject<Position>()
            let posB = token["_anchorBPosition"]?.ToObject<Position>()
            where posA != null && posB != null
            select new ZiplinePoi(token, posA, posB)
        );

        return result;
    }
}
