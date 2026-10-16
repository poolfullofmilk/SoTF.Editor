using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Actors;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Savegames;

public partial class Savegame
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private readonly Regex _nameFilePattern = NameFilePattern();

    public Savegame(string fullPath, string dirName, SavegameStore savegameStore)
    {
        SavegameStore = savegameStore;
        FullPath = fullPath;
        Title = dirName;
        ReadSaveData();
    }

    public string FullPath { get; }

    public SavegameStore SavegameStore { get; }

    public string Title => !string.IsNullOrWhiteSpace(GameName) ? $"{GameName} ({field})" : field;

    public string? GameName { get; private set; }

    public DateTime LastSaveTime { get; private set; }
    public string? ThumbnailPath => SavegameStore.GetThumbPath();

    public long RegrowTrees(VegetationState vegetationStateSelected, int pctRegrow)
    {
        if (
            SavegameStore.LoadJsonRaw(SavegameStore.FileType.WorldObjectLocatorManagerSaveData)
            is not { } saveDataWrapper
        )
        {
            return 0;
        }

        var worldObjectLocatorManager = saveDataWrapper.GetJsonBasedToken(
            Constants.JsonKeys.WorldObjectLocatorManager
        );
        if (worldObjectLocatorManager is not JObject)
        {
            return 0;
        }

        var countRegrown = 0;

        var candidates = (
            worldObjectLocatorManager["SerializedStates"] ?? Enumerable.Empty<JToken>()
        )
            .Where(serializedState =>
                VegetationStateOf(serializedState) is var state and not VegetationState.None
                && vegetationStateSelected.HasFlag(state)
            )
            .ToList();

        if (candidates.Count > 0)
        {
            // Only The Requested Share Regrows, Spread Over The Island
            countRegrown = (int)Math.Round(pctRegrow / 100.0 * candidates.Count);
            var regrown = candidates.ToArray();
            Random.Shared.Shuffle(regrown);
            regrown.Take(countRegrown).ToList().ForEach(candidate => candidate.Remove());
        }

        if (countRegrown == 0)
        {
            return 0;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.WorldObjectLocatorManager);

        return countRegrown;
    }

    public static VegetationState VegetationStateOf(JToken serializedState)
    {
        // Saved Values Run From 1 To 3, One Flag Each
        return serializedState["Value"]?.Value<short>() is short value and >= 1 and <= 3
            ? (VegetationState)(1 << (value - 1))
            : VegetationState.None;
    }

    public bool ReviveFollower(int typeId, HashSet<int> itemIds, Outfit? outfit, Position pos)
    {
        var reviveResult = ReviveFollowerInSaveData(typeId, itemIds, outfit, pos);
        var gameStateKey = typeId == KelvinTypeId ? "IsRobbyDead" : "IsVirginiaDead";

        var modifyGameStateResult = ModifyGameState(
            new Dictionary<string, object> { { gameStateKey, false } }
        );

        return reviveResult || modifyGameStateResult;
    }

    private bool ReviveFollowerInSaveData(
        int typeId,
        HashSet<int> itemIds,
        Outfit? outfit,
        Position pos
    )
    {
        if (SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData) is not { } saveDataWrapper)
        {
            return false;
        }

        var followerModifier = new FollowerModifier(saveDataWrapper);
        return followerModifier.Revive(typeId, itemIds, outfit, pos);
    }

    private void ReadSaveData()
    {
        LastSaveTime = SavegameStore.LastWriteTime;
        GameName = FindGameName();
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
            s_logger.Info($"Found No Name File In {FullPath}");
            return null;
        }

        var patternMatch = _nameFilePattern.Match(Path.GetFileName(nameFile));
        if (!patternMatch.Success || patternMatch.Groups.Count < 2)
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
            if (token == null || token.Equals(valueAsToken))
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

    public bool IsSinglePlayer()
    {
        return ParentDirIs("singleplayer");
    }

    public bool IsMultiPlayer()
    {
        return ParentDirIs("multiplayer");
    }

    public bool IsMultiPlayerClient()
    {
        return ParentDirIs("multiplayerclient");
    }

    public bool HasUnknownParentDir()
    {
        return !IsSinglePlayer() && !IsMultiPlayer() && !IsMultiPlayerClient();
    }

    private bool ParentDirIs(string value)
    {
        return string.Equals(
            SavegameStore.GetParentDirectory()?.Name,
            value,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [GeneratedRegex(@"(.*)_\d{2}_\d{2}_\d{2}.name")]
    private static partial Regex NameFilePattern();
}
