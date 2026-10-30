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

public sealed class ActorModifier(PlayerPageViewModel playerPageViewModel)
{
    public void Modify(Savegame selectedSavegame, EditActorViewModel data)
    {
        if (
            selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not { } vailWorldSim
            || vailWorldSim["Actors"] is not { } actors
        )
        {
            return;
        }

        List<JToken> matchedActors =
        [
            .. actors.Where(actor =>
                data.Matches(
                    actor["TypeId"]?.Value<int>(),
                    actor["FamilyId"]?.Value<int>(),
                    actor["GraphMask"]?.Value<int>(),
                    actor["UniqueId"]?.Value<int>()
                )
            ),
        ];
        if (matchedActors.Count == 0)
        {
            return;
        }

        // Spawners Go Only When Asked, Either Way
        RemoveWhere(
            vailWorldSim["Spawners"],
            data.ModifyOptions.RemoveSpawner
                ? [.. matchedActors.Select(actor => actor["SpawnerId"]?.Value<int>()).OfType<int>()]
                : []
        );

        if (data.ModificationMode == ActorModificationMode.Remove)
        {
            matchedActors.ForEach(actor => actor.Remove());

            // Their Feelings And Carried Items Go With Them
            var uniqueIds = UniqueIdsOf(matchedActors);
            RemoveWhere(vailWorldSim["InfluenceMemory"], uniqueIds);
            if (
                saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.NpcItemInstances) is
                { } npcItemInstances
            )
            {
                RemoveWhere(npcItemInstances["ActorItems"], uniqueIds);
                saveDataWrapper.MarkAsModified(Constants.JsonKeys.NpcItemInstances);
            }
        }
        else
        {
            Modify(vailWorldSim, matchedActors, data);
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
    }

    private static HashSet<int> UniqueIdsOf(IEnumerable<JToken> actors)
    {
        return [.. actors.Select(actor => actor["UniqueId"]?.Value<int>()).OfType<int>()];
    }

    private void Modify(JToken vailWorldSim, List<JToken> matchedActors, EditActorViewModel data)
    {
        var options = data.ModifyOptions;
        foreach (var actor in matchedActors)
        {
            if (actor["Stats"] is { } stats)
            {
                if (options.UpdateEnergy)
                {
                    stats["Energy"] = options.ActorEnergy;
                }

                if (options.UpdateHealth)
                {
                    stats["Health"] = options.ActorHealth;
                }
            }

            if (options.ReplaceType is { } replaceType)
            {
                actor["TypeId"] = replaceType.Id;
            }

            if (options.TeleportMode == "NpcToPlayer")
            {
                var newPos = playerPageViewModel.PlayerState.Pos.ForTeleport();
                actor["Position"] = JToken.FromObject(newPos);
                actor["GraphMask"] = newPos.Area.GraphMask;
            }
        }

        if (options.UpdateInfluences)
        {
            UpdateInfluences(vailWorldSim, data.Influences, UniqueIdsOf(matchedActors));
        }

        if (options.TeleportMode == "PlayerToNpc")
        {
            // The Player Takes The Spot, The Actor Steps Up Beside
            var actorPos = data.Actor.Position!;
            matchedActors
                .LastOrDefault(actor => actor["UniqueId"]?.Value<int>() == data.Actor.UniqueId)
                ?["Position"] = JToken.FromObject(actorPos.ForTeleport());
            playerPageViewModel.PlayerState.Pos = actorPos.WithoutOffset();
        }
    }

    private static void UpdateInfluences(
        JToken vailWorldSim,
        List<Influence> influences,
        HashSet<int> uniqueIds
    )
    {
        if (vailWorldSim["InfluenceMemory"] is not JArray influenceMemories)
        {
            return;
        }

        // One Memory Per Actor, Repeats Go
        var handled = new HashSet<int>();
        foreach (var influenceMemory in influenceMemories.ToList())
        {
            if (
                influenceMemory["UniqueId"]?.Value<int>() is not { } uniqueId
                || !uniqueIds.Contains(uniqueId)
            )
            {
                continue;
            }

            if (handled.Add(uniqueId))
            {
                influenceMemory["Influences"] = JToken.FromObject(influences);
            }
            else
            {
                influenceMemory.Remove();
            }
        }

        foreach (var uniqueId in uniqueIds.Where(uniqueId => !handled.Contains(uniqueId)))
        {
            influenceMemories.Add(JToken.FromObject(new InfluenceMemory(uniqueId, influences)));
        }
    }

    private static void RemoveWhere(JToken? entries, HashSet<int> uniqueIds)
    {
        foreach (
            var entry in (entries ?? Enumerable.Empty<JToken>())
                .Where(entry =>
                    entry["UniqueId"]?.Value<int>() is { } uniqueId && uniqueIds.Contains(uniqueId)
                )
                .ToList()
        )
        {
            entry.Remove();
        }
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
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
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
