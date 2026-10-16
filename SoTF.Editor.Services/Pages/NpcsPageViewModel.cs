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

    public void Load(Savegame? selectedSavegame)
    {
        AllActors.Clear();
        ActorsByType.Clear();
        ActorsByFamily.Clear();
        if (selectedSavegame != null)
        {
            LoadNpcs(selectedSavegame);
        }
    }

    private void LoadNpcs(Savegame? savegame)
    {
        if (
            savegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            is not { } saveDataWrapper
        )
        {
            return;
        }

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
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

        AllActors.Clear();
        AllActors.AddRange(
            vailWorldSim["Actors"]?.ToObject<List<Actor>>() ?? Enumerable.Empty<Actor>().ToList()
        );

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

            if (_actorTypes.TryGetValue(actor.TypeId, out var actorType))
            {
                actor.ActorType = actorType;
            }
            else
            {
                actor.ActorType = new ActorType(-1, AppText.Get("Unknown"));
                s_logger.Warn($"Found No Actor Type For {actor.TypeId}");
            }
        }

        ActorsByType.AddRange(
            AllActors
                .OrderBy(actor => actor.GraphMask)
                .ThenBy(actor => actor.FamilyId)
                .GroupBy(actor => actor.TypeId)
                .Select(typeGroup => new ActorCollection(
                    _actorTypes.GetValueOrDefault(typeGroup.Key)?.Name ?? AppText.Get("Unknown"),
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
        var uniqueNames = GetUniqueNamesOfActors(subActors);
        if (uniqueNames.Count == 1)
        {
            return uniqueNames.First();
        }

        if (subActors.TrueForAll(actor => actor.ActorType?.Classification == "animal"))
        {
            return AppText.Get("GroupAnimals");
        }

        if (subActors.TrueForAll(actor => actor.ActorType?.Classification == "creep"))
        {
            return AppText.Get("GroupCreeps");
        }

        if (subActors.TrueForAll(actor => actor.ActorType?.Classification == "misc"))
        {
            return AppText.Get("GroupMisc");
        }

        var allMale = subActors.TrueForAll(actor => actor.ActorType?.Gender == "male");
        var allFemale = subActors.TrueForAll(actor => actor.ActorType?.Gender == "female");

        if (subActors.TrueForAll(actor => actor.ActorType?.Classification == "cannibal"))
        {
            return allMale
                ? AppText.Get("GroupMaleCannibals")
                : AppText.Get(allFemale ? "GroupFemaleCannibals" : "GroupMixedCannibals");
        }

        if (subActors.TrueForAll(actor => actor.ActorType?.Classification == "muddyCannibal"))
        {
            return allMale
                ? AppText.Get("GroupMaleMuddyCannibals")
                : AppText.Get(allFemale ? "GroupFemaleMuddyCannibals" : "GroupMixedMuddyCannibals");
        }

        return AppText.Get(
            subActors.TrueForAll(actor => actor.FamilyId == 0) ? "GroupNoFamily" : "GroupMixed"
        );
    }

    private static HashSet<string> GetUniqueNamesOfActors(IEnumerable<Actor> subActors)
    {
        var uniqueNames = subActors
            .Where(actor => actor.ActorType?.Name != null)
            .Select(actor => actor.ActorType!.Name)
            .ToHashSet();
        return uniqueNames;
    }
}
