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

        GetAllUniqueAndSpawnerIds(vailWorldSim, out var allUniqueIds);
        var uniqueId = GetNextFreeUniqueId(allUniqueIds);

        var templateText = ResourceFile
            .Read("ActorTemplate.txt")
            .Replace("{uniqueId}", uniqueId.ToString())
            .Replace("{typeId}", typeId.ToString())
            .Replace("{actorSeed}", new Random().Next().ToString());
        var actorTemplate = JToken.Parse(templateText);

        actorTemplate["Position"]?.Replace(JToken.FromObject(pos));

        if (typeId == VirginiaTypeId)
        {
            actorTemplate["NextGiftTime"]?.Replace("1000.0");
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

    private static void GetAllUniqueAndSpawnerIds(
        JToken vailWorldSim,
        out HashSet<int> allUniqueIds
    )
    {
        allUniqueIds = [];

        foreach (var actor in vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
        {
            if (actor["UniqueId"]?.Value<int>() is { } uniqueId)
            {
                allUniqueIds.Add(uniqueId);
            }
        }
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

        GetAllUniqueAndSpawnerIds(vailWorldSim, out var allUniqueIds);

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

        var random = new Random();

        for (var i = 0; i < spawnCount; i++)
        {
            var cloned = actorTemplate.DeepClone();

            nextUniqueId = GetNextFreeUniqueId(allUniqueIds, nextUniqueId);
            allUniqueIds.Add(nextUniqueId);
            newUniqueIds.Add(nextUniqueId);
            cloned["UniqueId"] = nextUniqueId;
            cloned["ActorSeed"] = random.Next();

            if (distributedCoordinates != null)
            {
                if (i < distributedCoordinates.Count)
                {
                    var (newX, newZ) = distributedCoordinates[i];
                    cloned["Position"] = JToken.FromObject(new Position(newX, position.Y, newZ));
                }
            }

            actorArray.Add(cloned);
        }

        return newUniqueIds;
    }
}
