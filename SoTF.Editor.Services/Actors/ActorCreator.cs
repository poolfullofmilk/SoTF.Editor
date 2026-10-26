using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Resources;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Actors;

internal static class ActorCreator
{
    private static readonly string s_template = ResourceFile.Read("ActorTemplate.txt");

    public static (int UniqueId, JToken Actor)? CreateFollower(
        int typeId,
        JToken vailWorldSim,
        Position pos
    )
    {
        if (vailWorldSim["Actors"] is not JArray actorArray)
        {
            return null;
        }

        var uniqueId = GetNextFreeUniqueId(UniqueIdsOf(vailWorldSim));
        var actor = NewActor(uniqueId, typeId, Random.Shared.Next(), pos);
        if (typeId == VirginiaTypeId)
        {
            actor["NextGiftTime"] = 1000f;
        }

        actorArray.Add(actor);
        return (uniqueId, actor);
    }

    private static JToken NewActor(int uniqueId, int typeId, int actorSeed, Position pos)
    {
        var actor = JToken.Parse(
            s_template
                .Replace("{uniqueId}", uniqueId.ToString())
                .Replace("{typeId}", typeId.ToString())
                .Replace("{actorSeed}", actorSeed.ToString())
        );
        actor["Position"] = JToken.FromObject(pos);
        actor["GraphMask"] = pos.Area.GraphMask;
        return actor;
    }

    private static int GetNextFreeUniqueId(HashSet<int> allUniqueIds, int uniqueId = 1)
    {
        while (allUniqueIds.Contains(uniqueId) && uniqueId < int.MaxValue)
        {
            uniqueId++;
        }

        return uniqueId;
    }

    private static HashSet<int> UniqueIdsOf(JToken vailWorldSim)
    {
        // Deleted Actors Leave Memories, New Ones Never Reuse Their Ids
        return
        [
            .. (vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
                .Concat(vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>())
                .Select(entry => entry["UniqueId"]?.Value<int>() ?? 0),
        ];
    }

    public static HashSet<int> CreateActors(
        JToken vailWorldSim,
        Position position,
        ActorType actorType,
        int spawnCount,
        int? familyId,
        int spaceBetween,
        SpawnPattern spawnPattern
    )
    {
        if (vailWorldSim["Actors"] is not JArray actorArray)
        {
            return [];
        }

        var actorTemplate = NewActor(0, actorType.Id, 0, position);
        if (familyId != null)
        {
            actorTemplate["FamilyId"] = familyId;
        }

        // Spread Out Only With Room, Else All On The Spot
        var coordinates =
            spaceBetween > 0 && spawnCount > 0
                ? position.DistributeCoordinates(spawnCount, spaceBetween, spawnPattern)
                : [];

        var allUniqueIds = UniqueIdsOf(vailWorldSim);
        var newUniqueIds = new HashSet<int>();
        var nextUniqueId = 1;
        for (var spawned = 0; spawned < spawnCount; spawned++)
        {
            var cloned = actorTemplate.DeepClone();
            nextUniqueId = GetNextFreeUniqueId(allUniqueIds, nextUniqueId);
            allUniqueIds.Add(nextUniqueId);
            newUniqueIds.Add(nextUniqueId);
            cloned["UniqueId"] = nextUniqueId;
            cloned["ActorSeed"] = Random.Shared.Next();

            if (spawned < coordinates.Count)
            {
                var (newX, newZ) = coordinates[spawned];
                cloned["Position"] = JToken.FromObject(new Position(newX, position.Y, newZ));
            }

            actorArray.Add(cloned);
        }

        return newUniqueIds;
    }
}
