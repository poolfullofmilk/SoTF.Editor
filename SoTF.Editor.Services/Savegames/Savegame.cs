using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Savegames;

public partial class Savegame
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public Savegame(string fullPath, string dirName, SavegameStore savegameStore)
    {
        SavegameStore = savegameStore;
        FullPath = fullPath;
        Id = dirName;
        LastSaveTime = SavegameStore.LastWriteTime;
        GameName = FindGameName();
        _summary = new Lazy<SavegameSummary?>(ReadSummary);
    }

    private readonly Lazy<SavegameSummary?> _summary;

    public Savegame WithStore(SavegameStore savegameStore)
    {
        return new Savegame(FullPath, Id, savegameStore);
    }

    // Read Once, On The Thread That Lists The Savegames
    public SavegameSummary? Summary => _summary.Value;

    public string FullPath { get; }

    public SavegameStore SavegameStore { get; }

    public string Id { get; }

    public string Name => string.IsNullOrWhiteSpace(GameName) ? JoinedGameName ?? Id : GameName;

    // A Joined Game Has No Name File, Named Like Hosted
    private string? JoinedGameName =>
        IsMultiPlayerClient() && Summary is { GameType: { } gameType } summary
            ? AppText.Format(
                "JoinedGameName",
                AppText.Get(TextKeys.GameMode(gameType)),
                summary.Days
            )
            : null;

    public string? GameName { get; }

    public DateTime LastSaveTime { get; }
    public string? ThumbnailPath => SavegameStore.GetThumbPath();

    public bool ReviveFollower(int typeId, HashSet<int> itemIds, Outfit? outfit, Position? pos)
    {
        var reviveResult = ReviveFollowerInSaveData(typeId, itemIds, outfit, pos);
        var gameStateKey =
            typeId == KelvinTypeId
                ? Constants.GameStateKeys.IsRobbyDead
                : Constants.GameStateKeys.IsVirginiaDead;

        var modifyGameStateResult = ModifyGameState(
            new Dictionary<string, object> { { gameStateKey, false } }
        );

        return reviveResult || modifyGameStateResult;
    }

    private bool ReviveFollowerInSaveData(
        int typeId,
        HashSet<int> itemIds,
        Outfit? outfit,
        Position? pos
    )
    {
        if (SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData) is not { } saveDataWrapper)
        {
            return false;
        }

        var followerModifier = new FollowerModifier(saveDataWrapper);
        return followerModifier.Revive(typeId, itemIds, outfit, pos);
    }

    private SavegameSummary? ReadSummary()
    {
        // Opening Reads A Fresh Savegame, This One Only Lists
        var store = SavegameStore;
        try
        {
            // Only Shapes The Game Writes, Anything Else Marks It Damaged
            var gameState = ObjectOf(
                store
                    .LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
                    ?.GetJsonBasedToken(Constants.JsonKeys.GameState)
            );
            var strengthLevel = PlayerEntries
                .ByName(PlayerEntries.Of(store)?.Entries)
                .GetValueOrDefault("StrengthLevel")
                ?["IntValue"];
            var itemBlocks =
                ObjectOf(
                    store
                        .LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)
                        ?.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)
                )
                    ?.SelectToken("ItemInstanceManagerData.ItemBlocks") as JArray;
            return new SavegameSummary(
                IntOf(gameState?[Constants.GameStateKeys.GameDays]) ?? 0,
                TextOf(gameState?[Constants.GameStateKeys.GameType]),
                TextOf(gameState?[Constants.GameStateKeys.CrashSite]),
                IntOf(strengthLevel) ?? 0,
                itemBlocks?.Count(block => block is JObject && IntOf(block["TotalCount"]) > 0) ?? 0
            );
        }
        catch (Exception exception)
            when (exception
                    is JsonException
                        or IOException
                        or UnauthorizedAccessException
                        or InvalidDataException
                        or FormatException
                        or InvalidCastException
                        or InvalidOperationException
                        or ArgumentException
                        or OverflowException
            )
        {
            // A Damaged Savegame Still Lists, Opening It Shows The Error
            s_logger.Warn(exception, $"Failed To Read Summary Of {FullPath}");
            return null;
        }
    }

    private static int? IntOf(JToken? token)
    {
        // Missing Reads As Absent, Other Types Mark The Save Damaged
        return token is null || token.Type == JTokenType.Null ? null
            : token.Type == JTokenType.Integer ? token.Value<int>()
            : throw new InvalidDataException($"Found An Unexpected {token.Path}");
    }

    private static string? TextOf(JToken? token)
    {
        return token is null || token.Type == JTokenType.Null ? null
            : token.Type == JTokenType.String ? token.Value<string>()
            : throw new InvalidDataException($"Found An Unexpected {token.Path}");
    }

    private static JObject? ObjectOf(JToken? token)
    {
        return token is null or JObject
            ? (JObject?)token
            : throw new InvalidDataException($"Found An Unexpected {token.Path}");
    }

    private string? FindGameName()
    {
        // The Game Renames It Each Save, The Newest One Counts
        var nameFile = Directory
            .GetFiles(FullPath, "*.name")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (nameFile == null)
        {
            return null;
        }

        var patternMatch = NameFilePattern().Match(Path.GetFileName(nameFile));
        if (!patternMatch.Success)
        {
            s_logger.Warn($"Found Unexpected Name In {nameFile}, Using The File Name");
            return Path.GetFileNameWithoutExtension(nameFile).Replace("_", " ");
        }

        return patternMatch.Groups[1].Value.Replace("_", " ");
    }

    public bool ModifyGameState(Dictionary<string, object> values)
    {
        if (
            SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.GameState)
                is not JObject gameState
        )
        {
            return false;
        }

        var hasChanges = false;

        foreach (var keyValuePair in values)
        {
            var token = gameState[keyValuePair.Key];
            var valueAsToken = JToken.FromObject(keyValuePair.Value);
            if (token == null || JToken.DeepEquals(token, valueAsToken))
            {
                continue;
            }

            token.Replace(valueAsToken);
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return false;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.GameState);

        return true;
    }

    public bool IsMultiPlayer()
    {
        return ParentDirIs("multiplayer");
    }

    public bool IsMultiPlayerClient()
    {
        return ParentDirIs("multiplayerclient");
    }

    private bool ParentDirIs(string value)
    {
        return string.Equals(
            SavegameStore.GetParentDirectory()?.Name,
            value,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [GeneratedRegex(@"(.*)_\d{2}_\d{2}_\d{2}\.name$")]
    private static partial Regex NameFilePattern();
}
