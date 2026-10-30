using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Savegames;

public static partial class BackupDiff
{
    // A Change No Page Shows Is Listed By Its File
    public const string FilesKey = "Files";

    public static Dictionary<string, List<Change>> Describe(
        string savegamePath,
        string backup,
        GameData gameData,
        Func<string, string> setupLabel
    )
    {
        // Now Against What A Restore Would Bring Back
        var now = new SavegameStore(savegamePath);
        var then = new SavegameStore(backup);
        var describer = new Describer(gameData, setupLabel);
        var changes = new Dictionary<string, List<(string Name, ChangeKind Kind)>>();
        foreach (var (key, kind) in BackupManager.DifferingFiles(savegamePath, backup))
        {
            var found = false;
            foreach (
                var fileType in Enum.GetValues<SavegameStore.FileType>()
                    .Where(fileType => fileType.GetFilename() == Path.GetFileName(key))
            )
            {
                foreach (var (page, name, entryKind) in describer.Describe(fileType, now, then))
                {
                    found = true;
                    Add(changes, page, (name, entryKind));
                }
            }

            if (!found)
            {
                Add(changes, FilesKey, (BackupManager.DisplayNameOf(key), kind));
            }
        }

        // Repeats Read Once With How Many
        return changes.ToDictionary(
            pair => pair.Key,
            pair =>
                pair.Value.GroupBy(change => change)
                    .Select(group => new Change(
                        group.Key.Name,
                        group.Key.Kind,
                        group.Count() > 1 ? group.Count() : null
                    ))
                    .ToList()
        );
    }

    private static void Add<T>(Dictionary<string, List<T>> changes, string page, T change)
    {
        if (!changes.TryGetValue(page, out var list))
        {
            list = [];
            changes[page] = list;
        }

        list.Add(change);
    }

    public static string Words(string name)
    {
        // A Trailing Id Goes, isNewItem_0042 Reads As Is New Item
        var trimmed = TrailingId().Replace(name.TrimStart('_'), "");
        var words = WordStart()
            .Replace(trimmed.Replace('_', ' ').Replace('.', ' '), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);
        return string.Join(' ', words) is { Length: > 0 } title ? title : name;
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    internal static partial Regex WordStart();

    [GeneratedRegex(@"[._]?\d+$")]
    private static partial Regex TrailingId();

    private sealed class Describer(GameData gameData, Func<string, string> setupLabel)
    {
        public IEnumerable<(string Page, string Name, ChangeKind Kind)> Describe(
            SavegameStore.FileType fileType,
            SavegameStore now,
            SavegameStore then
        )
        {
            JToken? Document(SavegameStore store, string key)
            {
                return store.LoadJsonRaw(fileType)?.GetJsonBasedToken(key);
            }

            IEnumerable<(string, string, ChangeKind)> Each(
                string key,
                Func<JToken?, JToken?, IEnumerable<(string, string, ChangeKind)>> describe
            )
            {
                var nowDocument = Document(now, key);
                var thenDocument = Document(then, key);
                return JToken.DeepEquals(nowDocument, thenDocument)
                    ? []
                    : describe(nowDocument, thenDocument);
            }

            return fileType switch
            {
                SavegameStore.FileType.GameStateSaveData => Each(
                    Constants.JsonKeys.GameState,
                    (nowDocument, thenDocument) =>
                        Properties("Game", nowDocument, thenDocument, SettingLabel)
                            .Concat(
                                Entries(
                                    "Game",
                                    nowDocument?["NamedIntDatas"],
                                    thenDocument?["NamedIntDatas"],
                                    entry => entry["SaveObjectNameId"]?.ToString(),
                                    entry => entry["SaveObjectNameId"]?.ToString() ?? "?"
                                )
                            )
                ),
                SavegameStore.FileType.GameSetupSaveData => Each(
                    Constants.JsonKeys.GameSetup,
                    (nowDocument, thenDocument) =>
                        Entries(
                            "Game",
                            nowDocument?["_settings"],
                            thenDocument?["_settings"],
                            entry => entry["Name"]?.ToString(),
                            entry => setupLabel(entry["Name"]?.ToString() ?? "")
                        )
                ),
                SavegameStore.FileType.WeatherSystemSaveData => Each(
                    Constants.JsonKeys.WeatherSystem,
                    (nowDocument, thenDocument) =>
                        Properties("Game", nowDocument, thenDocument, SettingLabel)
                ),
                SavegameStore.FileType.PlayerStateSaveData => Each(
                    Constants.JsonKeys.PlayerState,
                    (nowDocument, thenDocument) =>
                        Entries(
                            "Player",
                            nowDocument?["_entries"],
                            thenDocument?["_entries"],
                            entry => entry["Name"]?.ToString(),
                            entry => PlayerLabel(entry["Name"]?.ToString() ?? "")
                        )
                ),
                SavegameStore.FileType.PlayerArmourSystemSaveData => Each(
                    Constants.JsonKeys.PlayerArmourSystem,
                    (nowDocument, thenDocument) =>
                        Entries(
                            "Player",
                            nowDocument?["ArmourPieces"],
                            thenDocument?["ArmourPieces"],
                            entry => entry["Slot"]?.ToString(),
                            entry => ItemName(entry["ItemId"]?.Value<int>())
                        )
                ),
                SavegameStore.FileType.PlayerClothingSystemSaveData => Each(
                    Constants.JsonKeys.PlayerClothingSystem,
                    (_, _) => [("Player", AppText.Get("Outfit"), ChangeKind.Changed)]
                ),
                SavegameStore.FileType.PlayerInventorySaveData => Each(
                    Constants.JsonKeys.PlayerInventory,
                    (nowDocument, thenDocument) =>
                        Entries(
                            "Inventory",
                            nowDocument?.SelectToken("ItemInstanceManagerData.ItemBlocks"),
                            thenDocument?.SelectToken("ItemInstanceManagerData.ItemBlocks"),
                            entry => entry["ItemId"]?.ToString(),
                            entry => ItemName(entry["ItemId"]?.Value<int>())
                        )
                ),
                SavegameStore.FileType.SaveData => Each(Constants.JsonKeys.VailWorldSim, Actors),
                SavegameStore.FileType.ScrewStructureInstancesSaveData => Each(
                    Constants.JsonKeys.ScrewStructureInstances,
                    (nowDocument, thenDocument) =>
                        Structures(nowDocument?["_structures"], thenDocument?["_structures"])
                ),
                SavegameStore.FileType.ScrewStructureNodeInstancesSaveData => Each(
                    Constants.JsonKeys.ScrewStructureNodeInstances,
                    (nowDocument, thenDocument) =>
                        Structures(nowDocument?["_structures"], thenDocument?["_structures"])
                ),
                SavegameStore.FileType.GardenPlotManagerSaveData => Each(
                    Constants.JsonKeys.GardenPlotManager,
                    (_, _) => [("Tools", AppText.Get("GardenPlots"), ChangeKind.Changed)]
                ),
                _ when fileType.ToString().StartsWith("Resin3dPrinter", StringComparison.Ordinal) =>
                    Each(
                        MainViewModel.PrinterKeyOf(fileType),
                        (_, _) => [("Tools", AppText.Get("PrinterResin"), ChangeKind.Changed)]
                    ),
                SavegameStore.FileType.ZipLineManagerSaveData => Each(
                    Constants.JsonKeys.ZipLineManager,
                    (nowDocument, thenDocument) =>
                        Entries(
                            "Map",
                            nowDocument?["Ziplines"],
                            thenDocument?["Ziplines"],
                            entry => $"{entry["_anchorAPosition"]}{entry["_anchorBPosition"]}",
                            _ => AppText.Get("Ziplines")
                        )
                ),
                _ => [],
            };
        }

        private static string SettingLabel(string name)
        {
            return AppText.Get(TextKeys.Setting(name), Words(name));
        }

        private static string PlayerLabel(string name)
        {
            var words = Words(name);
            return AppText.Get(words.Replace(" ", ""), words);
        }

        private string ItemName(int? itemId)
        {
            return itemId is { } id ? gameData.Items.GetItem(id)?.Name ?? $"#{id}" : "?";
        }

        private static IEnumerable<(string, string, ChangeKind)> Properties(
            string page,
            JToken? now,
            JToken? then,
            Func<string, string> label
        )
        {
            var nowObject = now as JObject ?? [];
            var thenObject = then as JObject ?? [];
            foreach (
                var name in thenObject
                    .Properties()
                    .Select(property => property.Name)
                    .Union(nowObject.Properties().Select(property => property.Name))
                    .Where(name => name is not ("NamedIntDatas" or "Version"))
            )
            {
                if (!JToken.DeepEquals(nowObject[name], thenObject[name]))
                {
                    yield return (page, label(name), KindOf(nowObject[name], thenObject[name]));
                }
            }
        }

        private static IEnumerable<(string, string, ChangeKind)> Entries(
            string page,
            JToken? now,
            JToken? then,
            Func<JToken, string?> keyOf,
            Func<JToken, string> nameOf
        )
        {
            var nowByKey = ByKey(now, keyOf);
            var thenByKey = ByKey(then, keyOf);
            foreach (var key in thenByKey.Keys.Union(nowByKey.Keys))
            {
                var nowEntry = nowByKey.GetValueOrDefault(key);
                var thenEntry = thenByKey.GetValueOrDefault(key);
                if (!JToken.DeepEquals(nowEntry, thenEntry))
                {
                    yield return (
                        page,
                        nameOf(thenEntry ?? nowEntry!),
                        KindOf(nowEntry, thenEntry)
                    );
                }
            }
        }

        private static ChangeKind KindOf(JToken? now, JToken? then)
        {
            // Since The Backup, Like The List Saved With It
            if (then is null)
            {
                return ChangeKind.Added;
            }

            return now is null ? ChangeKind.Removed : ChangeKind.Changed;
        }

        private static Dictionary<string, JToken> ByKey(
            JToken? entries,
            Func<JToken, string?> keyOf
        )
        {
            // Repeated Keys Count In Order, Like Numbered Storages
            var byKey = new Dictionary<string, JToken>();
            var seen = new Dictionary<string, int>();
            foreach (var entry in entries as JArray ?? [])
            {
                if (keyOf(entry) is not { } key)
                {
                    continue;
                }

                var ordinal = seen.GetValueOrDefault(key);
                seen[key] = ordinal + 1;
                byKey[$"{key}#{ordinal}"] = entry;
            }

            return byKey;
        }

        private IEnumerable<(string, string, ChangeKind)> Actors(JToken? now, JToken? then)
        {
            // An Actor Changes With Its Memory, Counted Once Per Page
            var nowActors = ById(now?["Actors"]);
            var thenActors = ById(then?["Actors"]);
            var nowMemories = ById(now?["InfluenceMemory"]);
            var thenMemories = ById(then?["InfluenceMemory"]);
            foreach (var uniqueId in thenActors.Keys.Union(nowActors.Keys))
            {
                var nowActor = nowActors.GetValueOrDefault(uniqueId);
                var thenActor = thenActors.GetValueOrDefault(uniqueId);
                if (
                    JToken.DeepEquals(nowActor, thenActor)
                    && JToken.DeepEquals(
                        nowMemories.GetValueOrDefault(uniqueId),
                        thenMemories.GetValueOrDefault(uniqueId)
                    )
                )
                {
                    continue;
                }

                var typeId = (thenActor ?? nowActor)?["TypeId"]?.Value<int>();
                var actorType = gameData.ActorTypes.FirstOrDefault(type => type.Id == typeId);
                yield return (
                    actorType?.IsFollower() == true ? "Followers" : "Npcs",
                    actorType?.Name ?? "???",
                    KindOf(nowActor, thenActor)
                );
            }
        }

        private static Dictionary<int, JToken> ById(JToken? entries)
        {
            var byId = new Dictionary<int, JToken>();
            foreach (var entry in entries as JArray ?? [])
            {
                if (entry["UniqueId"]?.Value<int>() is { } uniqueId)
                {
                    byId[uniqueId] = entry;
                }
            }

            return byId;
        }

        private static IEnumerable<(string, string, ChangeKind)> Structures(
            JToken? now,
            JToken? then
        )
        {
            // Matched By Type And Spot, A Removal Moves No Neighbour
            static string? SpotOf(JToken entry)
            {
                return $"{entry["Id"]}@{entry["Pos"]?.ToString(Newtonsoft.Json.Formatting.None)}";
            }

            var nowBySpot = ByKey(now, SpotOf);
            var thenBySpot = ByKey(then, SpotOf);
            var numbers = NumbersOf(now);
            foreach (var key in thenBySpot.Keys.Union(nowBySpot.Keys))
            {
                var nowEntry = nowBySpot.GetValueOrDefault(key);
                var thenEntry = thenBySpot.GetValueOrDefault(key);
                if (JToken.DeepEquals(nowEntry, thenEntry))
                {
                    continue;
                }

                var entry = thenEntry ?? nowEntry!;
                var name = GameNames.Get(TextKeys.Structure(entry["Id"]?.Value<int>() ?? -1));
                var isStorage =
                    nowEntry?["Storages"] is JArray { Count: > 0 }
                    || thenEntry?["Storages"] is JArray { Count: > 0 };
                yield return isStorage && nowEntry != null && thenEntry != null
                    ? ("Storage", $"{name} #{numbers[nowEntry]}", ChangeKind.Changed)
                    : ("Structures", name, KindOf(nowEntry, thenEntry));
            }
        }

        private static Dictionary<JToken, int> NumbersOf(JToken? entries)
        {
            // Storages Numbered Per Type In Save Order, Like Their Page
            var numbers = new Dictionary<JToken, int>(ReferenceEqualityComparer.Instance);
            var perType = new Dictionary<string, int>();
            foreach (var entry in entries as JArray ?? [])
            {
                var type = entry["Id"]?.ToString() ?? "";
                perType[type] = perType.GetValueOrDefault(type) + 1;
                numbers[entry] = perType[type];
            }

            return numbers;
        }
    }
}
