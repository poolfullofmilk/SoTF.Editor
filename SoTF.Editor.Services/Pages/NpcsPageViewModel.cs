using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class NpcsPageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private readonly Dictionary<int, ActorType> _actorTypes;
    private readonly AreaMaskManager _areaManager;

    public NpcsPageViewModel(GameData gameData)
    {
        _actorTypes = gameData.ActorTypes.ToDictionary(actorType => actorType.Id);
        _areaManager = gameData.AreaManager;
    }

    public List<ActorGrouping> ActorsByFamily { get; } = [];
    public List<ActorCollection> ActorsByType { get; } = [];

    public List<Actor> AllActors { get; } = [];

    public void Load(Savegame? savegame)
    {
        AllActors.Clear();
        ActorsByType.Clear();
        ActorsByFamily.Clear();
        if (
            savegame
                ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
            is not { } vailWorldSim
        )
        {
            return;
        }

        var spawnersBySpawnerUniqueId =
            vailWorldSim["Spawners"]
                ?.ToObject<List<Spawner>>()
                ?.GroupBy(spawner => spawner.UniqueId)
                .ToDictionary(group => group.Key, group => group.First())
            ?? [];
        var influenceMemoryByUniqueId =
            vailWorldSim["InfluenceMemory"]
                ?.ToObject<List<InfluenceMemory>>()
                ?.GroupBy(memory => memory.UniqueId)
                .ToDictionary(group => group.Key, group => group.ToList())
            ?? [];
        var killStatsByType =
            vailWorldSim["KillStatsList"]
                ?.ToObject<List<KillStat>>()
                ?.ToDictionary(killStat => killStat.TypeId)
            ?? [];

        AllActors.AddRange(vailWorldSim["Actors"]?.ToObject<List<Actor>>() ?? []);
        foreach (var actor in AllActors)
        {
            if (actor is { GraphMask: { } graphMask, Position: { } position })
            {
                actor.Position = position.WithoutOffset(
                    _areaManager.GetAreaForGraphMask(graphMask)
                );
            }

            if (actor.SpawnerId is { } spawnerId)
            {
                actor.Spawner = spawnersBySpawnerUniqueId.GetValueOrDefault(spawnerId);
            }

            if (influenceMemoryByUniqueId.GetValueOrDefault(actor.UniqueId) is { } influenceMemory)
            {
                actor.Influences = [.. influenceMemory.SelectMany(memory => memory.Influences)];
            }

            actor.KillStats = killStatsByType.GetValueOrDefault(actor.TypeId);
            if (!_actorTypes.TryGetValue(actor.TypeId, out var actorType))
            {
                actorType = new ActorType(-1, AppText.Get("Unknown"));
                s_logger.Warn($"Found No Actor Type For {actor.TypeId}");
            }

            actor.ActorType = actorType;
        }

        ActorsByType.AddRange(
            AllActors
                .OrderBy(actor => actor.GraphMask)
                .ThenBy(actor => actor.FamilyId)
                .GroupBy(actor => actor.TypeId)
                .Select(typeGroup => new ActorCollection(
                    typeGroup.First().ActorType!.Name,
                    [.. typeGroup]
                ))
                .OrderBy(collection => collection.Name)
        );
        ActorsByFamily.AddRange(
            AllActors
                .Where(actor => actor.FamilyId != null)
                .OrderBy(actor => actor.GraphMask)
                .ThenBy(actor => actor.TypeId)
                .GroupBy(actor => actor.FamilyId)
                .GroupBy(familyGroup => BuildNameForActorList([.. familyGroup]))
                .Select(nameGroup =>
                {
                    var grouping = new ActorGrouping(nameGroup.Key);
                    grouping.ActorCollections.AddRange(
                        nameGroup
                            .Select(familyGroup => new ActorCollection(
                                familyGroup.Key.ToString() ?? "",
                                [.. familyGroup]
                            ))
                            .OrderByDescending(actorCollection => actorCollection.Actors.Count)
                    );
                    return grouping;
                })
                .OrderBy(grouping => grouping.Name)
        );
    }

    private static string BuildNameForActorList(List<Actor> subActors)
    {
        var uniqueNames = subActors
            .Select(actor => actor.ActorType?.Name)
            .OfType<string>()
            .ToHashSet();
        if (uniqueNames.Count == 1)
        {
            return uniqueNames.First();
        }

        // A Kind Or Gender Every Actor Shares Names The Group
        var key = (
            SharedValue(subActors.Select(actor => actor.ActorType?.Classification)),
            SharedValue(subActors.Select(actor => actor.ActorType?.Gender))
        ) switch
        {
            ("animal", _) => "GroupAnimals",
            ("creep", _) => "GroupCreeps",
            ("misc", _) => "GroupMisc",
            ("cannibal", "male") => "GroupMaleCannibals",
            ("cannibal", "female") => "GroupFemaleCannibals",
            ("cannibal", _) => "GroupMixedCannibals",
            ("muddyCannibal", "male") => "GroupMaleMuddyCannibals",
            ("muddyCannibal", "female") => "GroupFemaleMuddyCannibals",
            ("muddyCannibal", _) => "GroupMixedMuddyCannibals",
            _ when subActors.TrueForAll(actor => actor.FamilyId == 0) => "GroupNoFamily",
            _ => "GroupMixed",
        };
        return AppText.Get(key);
    }

    private static string? SharedValue(IEnumerable<string?> values)
    {
        return values.Distinct().Take(2).ToList() is [var sharedValue] ? sharedValue : null;
    }
}
