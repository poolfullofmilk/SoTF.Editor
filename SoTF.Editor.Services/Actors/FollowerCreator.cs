using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Resources;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Actors;

internal static class ActorCreator
{
    public static KeyValuePair<int, JToken>? CreateFollower(
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

        var templateText = ResourceFile
            .Read("ActorTemplate.txt")
            .Replace("{uniqueId}", uniqueId.ToString())
            .Replace("{typeId}", typeId.ToString())
            .Replace("{actorSeed}", Random.Shared.Next().ToString());
        var actorTemplate = JToken.Parse(templateText);

        actorTemplate["Position"] = JToken.FromObject(pos);
        actorTemplate["GraphMask"] = pos.Area.GraphMask;

        if (typeId == VirginiaTypeId)
        {
            actorTemplate["NextGiftTime"] = 1000f;
        }

        actorArray.Add(actorTemplate);

        return new KeyValuePair<int, JToken>(uniqueId, actorTemplate);
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
        return
        [
            .. (vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>()).Select(actor =>
                actor["UniqueId"]?.Value<int>() ?? 0
            ),
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
        var newUniqueIds = new HashSet<int>();

        if (vailWorldSim["Actors"] is not JArray actorArray)
        {
            return newUniqueIds;
        }

        var allUniqueIds = UniqueIdsOf(vailWorldSim);

        var templateText = ResourceFile
            .Read("ActorTemplate.txt")
            .Replace("{uniqueId}", "0")
            .Replace("{typeId}", actorType.Id.ToString())
            .Replace("{actorSeed}", "0");
        var actorTemplate = JToken.Parse(templateText);

        actorTemplate["Position"] = JToken.FromObject(position);
        if (familyId != null)
        {
            actorTemplate["FamilyId"] = familyId;
        }

        actorTemplate["GraphMask"] = position.Area.GraphMask;

        var nextUniqueId = 1;

        List<Tuple<float, float>>? distributedCoordinates = null;

        if (spaceBetween > 0 && spawnCount > 0)
        {
            distributedCoordinates = position.DistributeCoordinates(
                spawnCount,
                spaceBetween,
                spawnPattern
            );
        }

        for (var spawned = 0; spawned < spawnCount; spawned++)
        {
            var cloned = actorTemplate.DeepClone();

            nextUniqueId = GetNextFreeUniqueId(allUniqueIds, nextUniqueId);
            allUniqueIds.Add(nextUniqueId);
            newUniqueIds.Add(nextUniqueId);
            cloned["UniqueId"] = nextUniqueId;
            cloned["ActorSeed"] = Random.Shared.Next();

            if (distributedCoordinates != null)
            {
                if (spawned < distributedCoordinates.Count)
                {
                    var (newX, newZ) = distributedCoordinates[spawned];
                    cloned["Position"] = JToken.FromObject(new Position(newX, position.Y, newZ));
                }
            }

            actorArray.Add(cloned);
        }

        return newUniqueIds;
    }
}
