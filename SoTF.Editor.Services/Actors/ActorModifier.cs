using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Actors;

public class ActorModifier(PlayerPageViewModel playerPageViewModel)
{
    public void Modify(Savegame selectedSavegame, EditActorViewModel data)
    {
        if (
            selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            is not { } saveDataWrapper
        )
        {
            return;
        }

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not { } vailWorldSim
            || vailWorldSim["Actors"] is not { } actors
        )
        {
            return;
        }

        var matchedActors = GetMatchedActors(actors, data);

        if (matchedActors.Count <= 0)
        {
            return;
        }

        switch (data.ModificationMode)
        {
            case ActorModificationMode.Remove:
            {
                Remove(vailWorldSim, matchedActors, data);
                break;
            }
            case ActorModificationMode.Modify:
                Modify(vailWorldSim, matchedActors, data);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    data.ModificationMode,
                    "Unexpected modification mode"
                );
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
    }

    private static void Remove(
        JToken vailWorldSim,
        List<JToken> matchedActors,
        EditActorViewModel data
    )
    {
        var spawnerIdsToBeRemoved = new HashSet<int>();

        foreach (var actor in matchedActors)
        {
            if (
                data.ModifyOptions.RemoveSpawner
                && actor["SpawnerId"]?.Value<int>() is { } spawnerId
            )
            {
                spawnerIdsToBeRemoved.Add(spawnerId);
            }

            actor.Remove();
        }

        RemoveSpawners(vailWorldSim, spawnerIdsToBeRemoved);
        RemoveInfluenceMemories(vailWorldSim, matchedActors);
    }

    private static List<JToken> GetMatchedActors(JToken actors, EditActorViewModel data)
    {
        return [.. actors.Where(actor => ActorMatches(actor, data))];
    }

    private static bool ActorMatches(JToken actor, EditActorViewModel data)
    {
        var typeId = actor["TypeId"]?.Value<int>();
        if (data.SkipKelvin && typeId == Constants.Actors.KelvinTypeId)
        {
            return false;
        }

        if (data.OnlyInSameAreaAsActor && actor["GraphMask"]?.Value<int>() != data.Actor.GraphMask)
        {
            return false;
        }

        if (data.SkipVirginia && typeId == Constants.Actors.VirginiaTypeId)
        {
            return false;
        }

        switch (data.ActorSelection)
        {
            case 3:
            case 2 when typeId == data.Actor.TypeId:
            case 1 when actor["FamilyId"]?.Value<int>() == data.Actor.FamilyId:
            case 0 when actor["UniqueId"]?.Value<int>() == data.Actor.UniqueId:
                return true;
            default:
                return false;
        }
    }

    private void Modify(JToken vailWorldSim, List<JToken> matchedActors, EditActorViewModel data)
    {
        var spawnerIdsToBeRemoved = new HashSet<int>();

        var uniqueIds = new HashSet<int>();

        JToken? actorTokenForActorInData = null;

        foreach (var actor in matchedActors)
        {
            if (
                data.ModifyOptions.RemoveSpawner
                && actor["SpawnerId"]?.Value<int>() is { } spawnerId
            )
            {
                spawnerIdsToBeRemoved.Add(spawnerId);
            }

            if (actor["Stats"] is { } stats)
            {
                if (data.ModifyOptions.UpdateEnergy)
                {
                    stats["Energy"] = data.ModifyOptions.ActorEnergy;
                }

                if (data.ModifyOptions.UpdateHealth)
                {
                    stats["Health"] = data.ModifyOptions.ActorHealth;
                }
            }

            if (data.ModifyOptions.ReplaceType is { } replaceType and not EmptyActorType)
            {
                actor["TypeId"] = replaceType.Id;
            }

            if (actor["UniqueId"]?.Value<int>() is { } uniqueId)
            {
                uniqueIds.Add(uniqueId);

                if (data.Actor.UniqueId == uniqueId)
                {
                    actorTokenForActorInData = actor;
                }
            }

            if (data.ModifyOptions.TeleportMode == "NpcToPlayer")
            {
                var newPos = Teleporter.MoveToPos(playerPageViewModel.PlayerState.Pos);
                actor["Position"] = JToken.FromObject(newPos);
                actor["GraphMask"] = newPos.Area.GraphMask;
            }
        }

        RemoveSpawners(vailWorldSim, spawnerIdsToBeRemoved);

        if (data.ModifyOptions.UpdateInfluences)
        {
            UpdateInfluences(vailWorldSim, data.Influences, uniqueIds);
        }

        if (data.ModifyOptions.TeleportMode == "PlayerToNpc")
        {
            var actorPos = data.Actor.Position!;
            var playerPos = playerPageViewModel.PlayerState.Pos;
            Teleporter.MovePlayerToPos(ref playerPos, ref actorPos);

            actorTokenForActorInData?["Position"] = JToken.FromObject(actorPos);

            playerPageViewModel.PlayerState.Pos = playerPos;
        }
    }

    private static void UpdateInfluences(
        JToken vailWorldSim,
        List<Influence> influences,
        HashSet<int> uniqueIds
    )
    {
        var handledInfluenceMemories = new HashSet<int>();

        if (vailWorldSim["InfluenceMemory"] is not JArray influenceMemories)
        {
            return;
        }

        foreach (var influenceMemory in influenceMemories)
        {
            if (
                influenceMemory["UniqueId"]?.Value<int>() is not { } uniqueId
                || !uniqueIds.Contains(uniqueId)
            )
            {
                continue;
            }

            influenceMemory["Influences"] = JToken.FromObject(influences);
            handledInfluenceMemories.Add(uniqueId);
        }

        foreach (
            var uniqueId in uniqueIds.Where(uniqueId =>
                !handledInfluenceMemories.Contains(uniqueId)
            )
        )
        {
            influenceMemories.Add(JToken.FromObject(new InfluenceMemory(uniqueId, influences)));
        }
    }

    private static void RemoveInfluenceMemories(
        JToken vailWorldSim,
        IEnumerable<JToken> matchedActors
    )
    {
        var influenceMemoriesToBeRemoved = new List<JToken>();

        var uniqueIds = matchedActors
            .Select(token => token["UniqueId"]?.Value<int>())
            .Where(uniqueId => uniqueId != null)
            .Select(uniqueId => uniqueId!.Value)
            .ToHashSet();

        foreach (
            var influenceMemory in vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>()
        )
        {
            if (
                influenceMemory["UniqueId"]?.Value<int>() is { } uniqueId
                && uniqueIds.Contains(uniqueId)
            )
            {
                influenceMemoriesToBeRemoved.Add(influenceMemory);
            }
        }

        influenceMemoriesToBeRemoved.ForEach(influenceMemory => influenceMemory.Remove());
    }

    private static void RemoveSpawners(JToken vailWorldSim, HashSet<int> spawnerIdsToBeRemoved)
    {
        if (spawnerIdsToBeRemoved.Count <= 0)
        {
            return;
        }

        var spawnersToBeRemoved = new List<JToken>();

        foreach (var spawner in vailWorldSim["Spawners"] ?? Enumerable.Empty<JToken>())
        {
            if (
                spawner["UniqueId"]?.Value<int>() is { } spawnerId
                && spawnerIdsToBeRemoved.Contains(spawnerId)
            )
            {
                spawnersToBeRemoved.Add(spawner);
            }
        }

        spawnersToBeRemoved.ForEach(spawner => spawner.Remove());
    }

    public static void Spawn(
        Savegame selectedSavegame,
        Position position,
        ActorType actorType,
        int spawnCount,
        int? familyId,
        List<Influence> influences,
        int spaceBetween,
        SpawnPattern spawnPattern
    )
    {
        if (
            selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
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

        var newUniqueIds = ActorCreator.CreateActors(
            vailWorldSim,
            position,
            actorType,
            spawnCount,
            familyId,
            spaceBetween,
            spawnPattern
        );
        UpdateInfluences(vailWorldSim, influences, newUniqueIds);

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
    }
}
