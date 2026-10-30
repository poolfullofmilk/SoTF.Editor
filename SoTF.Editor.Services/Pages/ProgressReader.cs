using System.Globalization;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels.Progress;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public static class ProgressReader
{
    // The Game's Own Names, From Its Assets
    private static readonly string[] s_bunkers =
    [
        "Bunker A",
        "Bunker B",
        "Bunker C",
        "Bunker Entertainment",
        "Bunker Food",
        "Bunker Luxury",
        "Bunker Residential",
    ];

    private static readonly (string Flag, string Name)[] s_videos =
    [
        ("Cultists1", "Cultists"),
        ("CultistsArrival", "Cultists Arrival"),
        ("Estate Agent", "Estate Agent"),
        ("Sahara Confidential", "Sahara Confidential"),
    ];

    private static readonly (string Flag, string NameKey)[] s_soldiers =
    [
        ("Buried", "SoldierBuried"),
        ("Hanging", "SoldierHanging"),
        ("Raft", "SoldierRaft"),
    ];

    public static PlayerProgress Read(Savegame savegame, GameData gameData)
    {
        // The Newest Entry Wins When The Game Repeats A Name
        var entries = new Dictionary<string, JToken>();
        foreach (
            var entry in savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.PlayerState)
                ?["_entries"] as JArray
                ?? []
        )
        {
            if (entry["Name"]?.ToString() is { } name)
            {
                entries[name] = entry;
            }
        }

        bool IsSet(string name)
        {
            return entries.TryGetValue(name, out var entry)
                && entry["BoolValue"]?.Value<bool>() == true;
        }

        IReadOnlyList<Tally> ItemCounts(string prefix)
        {
            return
            [
                .. entries
                    .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(pair =>
                        int.TryParse(
                            pair.Key[prefix.Length..],
                            CultureInfo.InvariantCulture,
                            out var itemId
                        )
                        && gameData.Items.GetItem(itemId) is { } item
                        && pair.Value["IntValue"]?.Value<int>() is > 0 and var count
                            ? new Tally(item.Name, count, item.ImageUrl)
                            : null
                    )
                    .OfType<Tally>()
                    .OrderByDescending(tally => tally.Count)
                    .ThenBy(tally => tally.Name),
            ];
        }

        var vailWorldSim = savegame
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim);
        return new PlayerProgress(
            [
                new(
                    "Bunkers",
                    [
                        .. s_bunkers.Select(bunker => new Collectible(
                            bunker,
                            IsSet("PointOfInterestFound_" + bunker)
                        )),
                    ]
                ),
                new(
                    "ActionCameraVideos",
                    [
                        .. s_videos.Select(video => new Collectible(
                            video.Name,
                            IsSet("ActionCameraVideoCollected_" + video.Flag)
                        )),
                    ]
                ),
                new(
                    "MissingSoldiers",
                    [
                        .. s_soldiers.Select(soldier => new Collectible(
                            AppText.Get(soldier.NameKey),
                            IsSet("MissingTacticalFound_" + soldier.Flag)
                        )),
                    ]
                ),
                new(
                    "BlueprintPages",
                    [
                        .. InventoryPageViewModel
                            .BlueprintItemIds.Select(gameData.Items.GetItem)
                            .OfType<Item>()
                            .OrderBy(item => item.Name)
                            .Select(item => new Collectible(
                                item.Name,
                                IsSet(InventoryPageViewModel.BlueprintPagePrefix + item.Id),
                                item.ImageUrl
                            )),
                    ]
                ),
            ],
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.GameState)
                ?["GameDays"]?.Value<int>()
                ?? 0,
            vailWorldSim is null
                ? null
                : vailWorldSim["PlayerStats"]?["CutTrees"]?.Value<int>() ?? 0,
            vailWorldSim is null ? null : KillsOf(vailWorldSim, gameData),
            ItemCounts("crafted."),
            ItemCounts("consumed.")
        );
    }

    private static List<Tally> KillsOf(JToken vailWorldSim, GameData gameData)
    {
        // Kills Per Creature, Most First
        return
        [
            .. (vailWorldSim["KillStatsList"] ?? Enumerable.Empty<JToken>())
                .Select(killStat =>
                    (
                        Type: gameData.ActorTypes.Find(type =>
                            type.Id == killStat["TypeId"]?.Value<int>()
                        ),
                        Count: killStat["PlayerKilled"]?.Value<int>() ?? 0
                    )
                )
                .Where(kill => kill.Type != null && kill.Count > 0)
                .GroupBy(kill => kill.Type!.Name)
                .Select(group => new Tally(
                    group.Key,
                    group.Sum(kill => kill.Count),
                    group.First().Type!.IconPath
                ))
                .OrderByDescending(tally => tally.Count)
                .ThenBy(tally => tally.Name),
        ];
    }
}
