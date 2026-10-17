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
    public List<PoiGroup> GetActorPois()
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
        return WorldItemClonerViewModel
            .Load(savegame)
            .Select(state => new WorldItemPoi(state))
            .GroupBy(poi => poi.State.Group)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public static List<ZiplinePoi> GetZiplinePois(Savegame savegame)
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
