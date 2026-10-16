using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
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

    public string PrintableType
    {
        get
        {
            if (IsSinglePlayer())
            {
                return "SP";
            }

            if (IsMultiPlayer())
            {
                return "MP";
            }

            return IsMultiPlayerClient() ? "MP_Client" : AppText.Get("Unknown");
        }
    }

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

        var serializedStates =
            worldObjectLocatorManager["SerializedStates"]?.ToList() ?? Enumerable.Empty<JToken>();

        var countRegrown = 0;

        var candidates = new List<JToken>();

        foreach (var serializedState in serializedStates)
        {
            var valueToken = serializedState["Value"];
            var value = valueToken?.Value<short>();
            if (value == null)
            {
                continue;
            }

            var shiftedValue = (short)(1 << (value - 1));

            if (
                !Enum.IsDefined(typeof(VegetationState), shiftedValue)
                || !vegetationStateSelected.HasFlag((VegetationState)shiftedValue)
            )
            {
                continue;
            }

            candidates.Add(serializedState);
        }

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
        var nameFile = Directory.GetFiles(FullPath, "*.name").FirstOrDefault();
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
        return SavegameStore.GetParentDirectory()?.Name.ToLower().Equals(value) ?? false;
    }

    [GeneratedRegex(@"(.*)_\d{2}_\d{2}_\d{2}.name")]
    private static partial Regex NameFilePattern();
}
