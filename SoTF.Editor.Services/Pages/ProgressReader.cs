using System.Globalization;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels.Progress;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public static class ProgressReader
{
    // The Game's Own Names, With Their Map Point
    private static readonly (string Flag, string Place)[] s_bunkers =
    [
        ("Bunker A", "Maintenance A"),
        ("Bunker B", "Maintenance B"),
        ("Bunker C", "Maintenance C"),
        ("Bunker Entertainment", "Cave leading to Entertainment Bunker"),
        ("Bunker Food", "Food and Dining Bunker"),
        ("Bunker Luxury", "Luxury Bunker"),
        ("Bunker Residential", "Residential Bunker"),
    ];

    private static readonly (string Flag, string Name)[] s_videos =
    [
        ("Cultists1", "Cultists"),
        ("CultistsArrival", "Cultists Arrival"),
        ("Estate Agent", "Estate Agent"),
        ("Sahara Confidential", "Sahara Confidential"),
    ];

    // The Map Knows Items Lying On Two Of Them
    private static readonly (string Flag, string NameKey, string? Spot)[] s_soldiers =
    [
        ("Buried", "SoldierBuried", null),
        ("Hanging", "SoldierHanging", "On hanging soldier"),
        ("Raft", "SoldierRaft", "On Emergency Raft"),
    ];

    public static PlayerProgress Read(Savegame savegame, GameData gameData, PoiLoader poiLoader)
    {
        var entries = PlayerEntries.ByName(PlayerEntries.Of(savegame.SavegameStore)?.Entries);

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
                            bunker.Flag,
                            IsSet("PointOfInterestFound_" + bunker.Flag),
                            Position: poiLoader.PlacePosition(bunker.Place)
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
                            IsSet("MissingTacticalFound_" + soldier.Flag),
                            Position: soldier.Spot is null
                                ? null
                                : poiLoader.ItemPositionAt(soldier.Spot)
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
                                item.ImageUrl,
                                poiLoader.ItemPosition(item.Id)
                            )),
                    ]
                ),
            ],
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.GameState)
                ?[Constants.GameStateKeys.GameDays]?.Value<int>()
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
