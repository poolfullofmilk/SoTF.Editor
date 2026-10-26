using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
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

    private readonly LoadedValues _loaded = new();
    private readonly HashSet<int> _loadedIds = [];
    private Savegame? _loadedFrom;

    // Changed Actors Are Worked Out Once Per Load
    private readonly HashSet<Actor> _changed = new(ReferenceEqualityComparer.Instance);

    public bool HasChanges => RemovedCount > 0 || _changed.Count > 0;

    private int RemovedCount =>
        _loadedIds.Count - AllActors.Count(actor => _loadedIds.Contains(actor.UniqueId));

    // Spawned And Changed Actors By Type, Then The Removed Count
    public IEnumerable<Change> Changes =>
        AllActors
            .Where(_changed.Contains)
            .GroupBy(actor =>
                (
                    Name: actor.ActorType?.Name ?? "???",
                    Kind: _loadedIds.Contains(actor.UniqueId)
                        ? ChangeKind.Changed
                        : ChangeKind.Added
                )
            )
            .Select(group => new Change(
                group.Count() > 1 ? $"{group.Key.Name} ({group.Count()})" : group.Key.Name,
                group.Key.Kind
            ))
            .Concat(
                RemovedCount > 0
                    ?
                    [
                        new Change(
                            AppText.Format("ChangeRemoved", RemovedCount),
                            ChangeKind.Removed
                        ),
                    ]
                    : []
            );

    public bool IsChanged(Actor actor)
    {
        return _changed.Contains(actor);
    }

    public bool IsChanged(ActorCollection collection)
    {
        return collection.Actors.Any(_changed.Contains);
    }

    private static string ValueOf(Actor actor)
    {
        var stats = string.Join(
            " ",
            (actor.Stats ?? [])
                .OrderBy(stat => stat.Key)
                .Select(stat => $"{stat.Key} {LoadedValues.Format(stat.Value)}")
        );
        var influences = string.Join(
            " ",
            (actor.Influences ?? [])
                .OrderBy(influence => influence.TypeId)
                .Select(influence =>
                    FormattableString.Invariant(
                        $"{influence.TypeId} {influence.Anger} {influence.Fear} {influence.Sentiment}"
                    )
                )
        );
        return string.Join(
            "|",
            actor.TypeId,
            actor.FamilyId,
            actor.State,
            actor.OutfitId,
            actor.GraphMask,
            actor.SpawnerId,
            LoadedValues.Format(actor.Position),
            stats,
            influences
        );
    }

    public void Load(Savegame? savegame)
    {
        LoadActors(savegame);

        _changed.Clear();

        // Actor Changes Reload The Same Savegame, Keep The First Load
        if (!ReferenceEquals(savegame, _loadedFrom))
        {
            _loadedFrom = savegame;
            _loaded.Clear();
            _loadedIds.Clear();
            _loadedIds.UnionWith(AllActors.Select(actor => actor.UniqueId));
            foreach (var actor in AllActors)
            {
                _loaded.Remember(LoadedValues.Format(actor.UniqueId), ValueOf(actor));
            }

            return;
        }

        _changed.UnionWith(
            AllActors.Where(actor =>
                _loaded.IsChanged(LoadedValues.Format(actor.UniqueId), ValueOf(actor))
            )
        );
    }

    private void LoadActors(Savegame? savegame)
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
                .ToDictionary(group => group.Key, group => group.Last())
            ?? [];
        var killStatsByType =
            vailWorldSim["KillStatsList"]
                ?.ToObject<List<KillStat>>()
                ?.GroupBy(killStat => killStat.TypeId)
                .ToDictionary(group => group.Key, group => group.First())
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

            // The Newest Memory Wins, A Repeated Type Counts Once
            if (influenceMemoryByUniqueId.GetValueOrDefault(actor.UniqueId) is { } influenceMemory)
            {
                actor.Influences =
                [
                    .. influenceMemory.Influences.DistinctBy(influence => influence.TypeId),
                ];
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
