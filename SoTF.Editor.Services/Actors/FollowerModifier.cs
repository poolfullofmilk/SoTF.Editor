using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Services.Savegames;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Actors;

public class FollowerModifier(SaveDataWrapper saveDataWrapper)
{
    public bool Revive(int typeId, HashSet<int> itemIds, Outfit? outfit, Position? pos)
    {
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
            is not { } vailWorldSim
        )
        {
            return false;
        }

        var actorsByUniqueId = (vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
            .Where(actor =>
                actor["TypeId"]?.Value<int>() == typeId && actor["UniqueId"]?.Value<int>() != null
            )
            .DistinctBy(actor => actor["UniqueId"]!.Value<int>())
            .ToDictionary(actor => actor["UniqueId"]!.Value<int>());

        var vailWorldSimChanged = false;
        foreach (var (uniqueId, actor) in actorsByUniqueId)
        {
            if (actor["State"] is { } state && state.Value<int>() != StateAlive)
            {
                state.Replace(StateAlive);
                vailWorldSimChanged = true;
            }

            if (actor["Stats"] is { } stats)
            {
                // Every Bar Full, No Fear Left
                foreach (
                    var (key, full) in new (string, float)[]
                    {
                        ("Health", FullHealth),
                        ("Hydration", FullHydration),
                        ("Energy", FullEnergy),
                        ("Affection", FullAffection),
                    }
                )
                {
                    vailWorldSimChanged |= JsonModifier.CompareAndModify(
                        stats,
                        key,
                        value => value < full,
                        full
                    );
                }

                vailWorldSimChanged |= JsonModifier.CompareAndModify(
                    stats,
                    "Fear",
                    value => value > NoFear,
                    NoFear
                );
            }

            vailWorldSimChanged |= ResetPlayerInfluence(vailWorldSim, uniqueId);
        }

        vailWorldSimChanged |= ResetPlayerKillStatsForType(typeId, vailWorldSim);

        // A Follower Gone From The Save Is Created Anew
        if (
            actorsByUniqueId.Count == 0
            && pos is not null
            && ActorCreator.CreateFollower(typeId, vailWorldSim, pos) is (var newId, var newActor)
        )
        {
            actorsByUniqueId.Add(newId, newActor);
            AddInfluencesForNewFollower(vailWorldSim, newId);
            vailWorldSimChanged = true;
        }

        var npcItemInstancesChanged = false;
        foreach (var (uniqueId, actor) in actorsByUniqueId)
        {
            vailWorldSimChanged |= EquipItemsInActor(actor, itemIds);
            npcItemInstancesChanged |= EquipItemsInNpcItemInstances(uniqueId, itemIds);
            vailWorldSimChanged |= EquipOutfit(actor, outfit);
        }

        return MarkModified(vailWorldSimChanged, npcItemInstancesChanged);
    }

    public bool Remove(int uniqueId)
    {
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not { } vailWorldSim
            || !RemoveEntryOf(vailWorldSim["Actors"], uniqueId)
        )
        {
            return false;
        }

        // Its Feelings And Carried Items Go With It
        RemoveEntryOf(vailWorldSim["InfluenceMemory"], uniqueId);
        var npcItemInstancesChanged = RemoveEntryOf(
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.NpcItemInstances)?["ActorItems"],
            uniqueId
        );
        return MarkModified(true, npcItemInstancesChanged);
    }

    private static bool RemoveEntryOf(JToken? entries, int uniqueId)
    {
        if (
            entries?.Children().FirstOrDefault(entry => entry["UniqueId"]?.Value<int>() == uniqueId)
            is not { } entry
        )
        {
            return false;
        }

        entry.Remove();
        return true;
    }

    private bool MarkModified(bool vailWorldSimChanged, bool npcItemInstancesChanged)
    {
        if (vailWorldSimChanged)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.VailWorldSim);
        }

        if (npcItemInstancesChanged)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.NpcItemInstances);
        }

        return vailWorldSimChanged || npcItemInstancesChanged;
    }

    private static void AddInfluencesForNewFollower(JToken vailWorldSim, int uniqueId)
    {
        if (vailWorldSim["InfluenceMemory"] is not JArray influenceMemory)
        {
            influenceMemory = [];
            vailWorldSim["InfluenceMemory"] = influenceMemory;
        }

        // A New Follower Likes The Player And Fears Everyone Else
        influenceMemory.Add(
            JToken.FromObject(
                new InfluenceMemory(
                    uniqueId,
                    [
                        new Influence
                        {
                            TypeId = Influence.Type.Player,
                            Anger = NoAnger,
                            Fear = NoFear,
                            Sentiment = FullSentiment,
                        },
                        new Influence
                        {
                            TypeId = Influence.Type.Cannibal,
                            Anger = FullAnger,
                            Fear = FullFear,
                            Sentiment = LowestSentiment,
                        },
                        new Influence
                        {
                            TypeId = Influence.Type.Creepy,
                            Anger = FullAnger,
                            Fear = FullFear,
                            Sentiment = LowestSentiment,
                        },
                    ]
                )
            )
        );
    }

    private static bool EquipItemsInActor(JToken actor, HashSet<int> itemIds)
    {
        if (itemIds.SetEquals(actor["EquippedItems"]?.ToObject<HashSet<int>>() ?? []))
        {
            return false;
        }

        actor["EquippedItems"] = JToken.FromObject(itemIds);
        return true;
    }

    private bool EquipItemsInNpcItemInstances(int uniqueId, HashSet<int> itemIds)
    {
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.NpcItemInstances)
            is not { } npcItemInstances
        )
        {
            return false;
        }

        var hasChanges = false;
        if (npcItemInstances["ActorItems"] == null)
        {
            npcItemInstances["ActorItems"] = new JArray();
            hasChanges = true;
        }

        var actorItems = npcItemInstances["ActorItems"]!;
        var actorItemsForActor = actorItems
            .Children()
            .FirstOrDefault(token => token["UniqueId"]?.Value<int>() == uniqueId);
        if (actorItemsForActor is null)
        {
            actorItemsForActor = new JObject
            {
                ["UniqueId"] = uniqueId,
                ["Items"] = new JObject { ["Version"] = "0.0.0", ["ItemBlocks"] = new JArray() },
            };

            if (actorItems is JArray actorItemsArray)
            {
                actorItemsArray.Add(actorItemsForActor);
                hasChanges = true;
            }
        }

        if (actorItemsForActor.SelectToken("Items.ItemBlocks") is not JArray itemBlocks)
        {
            return hasChanges;
        }

        // Unequipped Items Leave, Every Kept Block Holds At Least One
        foreach (
            var itemBlock in itemBlocks
                .Where(token =>
                    token["ItemId"]?.Value<int>() is { } itemId && !itemIds.Contains(itemId)
                )
                .ToList()
        )
        {
            itemBlock.Remove();
            hasChanges = true;
        }

        var itemIdsExisting = new HashSet<int>();
        foreach (var itemBlock in itemBlocks)
        {
            if (itemBlock["TotalCount"] is { } totalCountToken && totalCountToken.Value<int>() < 1)
            {
                totalCountToken.Replace(1);
                hasChanges = true;
            }

            if (itemBlock["ItemId"]?.Value<int>() is { } itemId)
            {
                itemIdsExisting.Add(itemId);
            }
        }

        foreach (var itemId in itemIds.Where(itemId => !itemIdsExisting.Contains(itemId)))
        {
            itemBlocks.Add(JToken.FromObject(new ActorItemBlock(itemId, 1, [])));
            hasChanges = true;
        }

        return hasChanges;
    }

    private static bool EquipOutfit(JToken actor, Outfit? outfit)
    {
        // The Game Leaves Out The Default Outfit
        var outfitIdToken = actor["OutfitId"];
        var newOutfitId = outfit?.Id ?? 0;
        if ((outfitIdToken?.Value<int>() ?? 0) == newOutfitId)
        {
            return false;
        }

        if (newOutfitId == 0)
        {
            outfitIdToken?.Parent?.Remove();
        }
        else
        {
            actor["OutfitId"] = newOutfitId;
        }

        return true;
    }

    private static bool ResetPlayerKillStatsForType(int typeId, JToken vailWorldSim)
    {
        if (
            (vailWorldSim["KillStatsList"] ?? Enumerable.Empty<JToken>())
                .Where(killStat => killStat["TypeId"]?.Value<int>() == typeId)
                .Select(killStat => killStat["PlayerKilled"])
                .FirstOrDefault(playerKilled => playerKilled?.Value<int>() > 0)
            is not { } playerKilledToken
        )
        {
            return false;
        }

        playerKilledToken.Replace(0);
        return true;
    }

    private static bool ResetPlayerInfluence(JToken vailWorldSim, int uniqueId)
    {
        // The Newest Memory Of This Follower, Its First Player Influence
        if (
            (vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>())
                .LastOrDefault(memory => memory["UniqueId"]?.Value<int>() == uniqueId)
                ?["Influences"]?.FirstOrDefault(influence =>
                    influence["TypeId"]?.ToString() == Influence.Type.Player
                )
            is not { } playerInfluence
        )
        {
            return false;
        }

        var hasChanges = JsonModifier.CompareAndModify(
            playerInfluence,
            "Sentiment",
            value => value < FullSentiment,
            FullSentiment
        );
        hasChanges |= JsonModifier.CompareAndModify(
            playerInfluence,
            "Anger",
            value => value > NoAnger,
            NoAnger
        );
        hasChanges |= JsonModifier.CompareAndModify(
            playerInfluence,
            "Fear",
            value => value > NoFear,
            NoFear
        );
        return hasChanges;
    }

    public bool Update(IEnumerable<FollowerState> followerStates)
    {
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
            is not { } vailWorldSim
        )
        {
            return false;
        }

        var vailWorldSimChanged = false;
        var npcItemInstancesChanged = false;
        var statesByUniqueId = followerStates
            .Where(state => state.UniqueId != null)
            .ToDictionary(state => state.UniqueId!.Value);

        foreach (var actor in vailWorldSim["Actors"]?.ToList() ?? [])
        {
            // Each Loaded Follower Writes Only Its Own Actor
            if (
                actor["UniqueId"]?.Value<int>() is not { } uniqueId
                || !statesByUniqueId.Remove(uniqueId, out var followerModel)
                || actor["TypeId"]?.Value<int>() != followerModel.TypeId
            )
            {
                continue;
            }

            if (
                actor["Position"] is { } position
                && position.ToObject<Position>() is { } oldPosition
                && !oldPosition.HasSameCoordinates(followerModel.Pos)
            )
            {
                position.Replace(JToken.FromObject(followerModel.Pos));
                vailWorldSimChanged = true;
            }

            // Entering Or Leaving A Cave Changes The Graph Mask
            if (
                actor["GraphMask"]?.Value<int>() is { } graphMask
                && graphMask != followerModel.Pos.Area.GraphMask
            )
            {
                actor["GraphMask"] = followerModel.Pos.Area.GraphMask;
                vailWorldSimChanged = true;
            }

            // The Item Instances Follow Only A Real Change
            var itemIds = followerModel.GetSelectedInventoryItemIds();
            if (EquipItemsInActor(actor, itemIds))
            {
                vailWorldSimChanged = true;
                npcItemInstancesChanged |= EquipItemsInNpcItemInstances(uniqueId, itemIds);
            }

            vailWorldSimChanged |= UpdateInfluenceMemory(
                vailWorldSim,
                followerModel.Influences,
                uniqueId
            );
            vailWorldSimChanged |= EquipOutfit(actor, followerModel.Outfit);

            if (actor["Stats"] is not { } stats)
            {
                continue;
            }

            foreach (
                var (key, value) in new (string, float)[]
                {
                    ("Health", followerModel.Health),
                    ("Anger", followerModel.Anger),
                    ("Fear", followerModel.Fear),
                    ("Fullness", followerModel.Fullness),
                    ("Hydration", followerModel.Hydration),
                    ("Energy", followerModel.Energy),
                    ("Affection", followerModel.Affection),
                }
            )
            {
                vailWorldSimChanged |= JsonModifier.SetFloat(stats, key, value);
            }
        }

        return MarkModified(vailWorldSimChanged, npcItemInstancesChanged);
    }

    private static bool UpdateInfluenceMemory(
        JToken vailWorldSim,
        List<Influence> influences,
        int uniqueId
    )
    {
        if (vailWorldSim["InfluenceMemory"] is not JArray influenceMemory)
        {
            return false;
        }

        List<JToken> memories =
        [
            .. influenceMemory.Where(memory => memory["UniqueId"]?.Value<int>() == uniqueId),
        ];
        if (memories.Count == 0)
        {
            if (influences.Count == 0)
            {
                return false;
            }

            influenceMemory.Add(JToken.FromObject(new InfluenceMemory(uniqueId, [.. influences])));
            return true;
        }

        // The Newest Memory Counts, An Edit Folds Repeats Into It
        if (
            memories[^1]["Influences"] is JArray followersInfluences
            && !HasDifferencesInMemories(followersInfluences, influences)
        )
        {
            return false;
        }

        memories.SkipLast(1).ToList().ForEach(memory => memory.Remove());
        memories[^1]["Influences"] = JToken.FromObject(influences);
        return true;
    }

    private static bool HasDifferencesInMemories(
        JArray existingInfluences,
        List<Influence> newInfluences
    )
    {
        // A Repeated Type Counts Once, As The Page Loaded It
        var existingByType = (existingInfluences.ToObject<List<Influence>>() ?? [])
            .DistinctBy(influence => influence.TypeId)
            .ToDictionary(influence => influence.TypeId);
        return existingByType.Count != newInfluences.Count
            || newInfluences.Exists(influence =>
                !existingByType.TryGetValue(influence.TypeId, out var existing)
                || Math.Abs(existing.Anger - influence.Anger) > 0.001
                || Math.Abs(existing.Fear - influence.Fear) > 0.001
                || Math.Abs(existing.Sentiment - influence.Sentiment) > 0.001
            );
    }

    public bool CreateFollower(int typeId, HashSet<int> itemIds, Outfit? outfit, Position pos)
    {
        // A Step Beside The Player And A Little Above
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
                is not { } vailWorldSim
            || ActorCreator.CreateFollower(
                typeId,
                vailWorldSim,
                new Position(pos.X + 1, pos.Y + 5, pos.Z + 1)
            )
                is not
                (var newId, var newActor)
        )
        {
            return false;
        }

        AddInfluencesForNewFollower(vailWorldSim, newId);
        EquipItemsInActor(newActor, itemIds);
        var npcItemInstancesChanged = EquipItemsInNpcItemInstances(newId, itemIds);
        EquipOutfit(newActor, outfit);
        return MarkModified(true, npcItemInstancesChanged);
    }
}
