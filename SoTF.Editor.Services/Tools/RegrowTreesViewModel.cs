using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public sealed class RegrowTreesViewModel
{
    private const VegetationState AllStates =
        VegetationState.Gone | VegetationState.HalfChopped | VegetationState.Stumps;

    // Each State With Its Text Key, In Dialog Order
    private static readonly Dictionary<VegetationState, string> s_stateKeys = new()
    {
        [VegetationState.Gone] = "Gone",
        [VegetationState.HalfChopped] = "HalfChopped",
        [VegetationState.Stumps] = "Stumps",
    };

    private readonly Dictionary<VegetationState, int> _counts;
    private readonly SaveDataWrapper? _wrapper;

    public RegrowTreesViewModel(Savegame savegame)
    {
        _wrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.WorldObjectLocatorManagerSaveData
        );
        _counts = SerializedStates()
            .GroupBy(VegetationStateOf)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public static IEnumerable<VegetationState> States => s_stateKeys.Keys;

    public int PctRegrow { get; set; } = 100;

    public VegetationState VegetationStateSelected { get; set; } = AllStates;

    public static string Label(VegetationState state)
    {
        return AppText.Get(s_stateKeys[state]);
    }

    public int CountOf(VegetationState state)
    {
        return _counts.GetValueOrDefault(state);
    }

    public string Save()
    {
        var countRegrown = Regrow();
        var states = string.Join(
            ", ",
            States
                .Where(state => VegetationStateSelected.HasFlag(state))
                .Select(state => AppText.Get(s_stateKeys[state]))
        );
        return countRegrown == 0
            ? AppText.Format("NoTreesRegrown", states)
            : AppText.Format("TreesRegrown", countRegrown, states);
    }

    public bool CanRegrowTrees()
    {
        return VegetationStateSelected != VegetationState.None;
    }

    private IEnumerable<JToken> SerializedStates()
    {
        return _wrapper
                ?.GetJsonBasedToken(Constants.JsonKeys.WorldObjectLocatorManager)
                ?["SerializedStates"]
            ?? Enumerable.Empty<JToken>();
    }

    private int Regrow()
    {
        var candidates = SerializedStates()
            .Where(serializedState =>
                VegetationStateOf(serializedState) is var state and not VegetationState.None
                && VegetationStateSelected.HasFlag(state)
            )
            .ToArray();

        // Only The Requested Share Regrows, Spread Over The Island
        var countRegrown = (int)Math.Round(PctRegrow / 100.0 * candidates.Length);
        if (countRegrown == 0)
        {
            return 0;
        }

        Random.Shared.Shuffle(candidates);
        foreach (var candidate in candidates.Take(countRegrown))
        {
            candidate.Remove();
        }

        _wrapper!.MarkAsModified(Constants.JsonKeys.WorldObjectLocatorManager);
        return countRegrown;
    }

    private static VegetationState VegetationStateOf(JToken serializedState)
    {
        // Saved Values Run From 1 To 3, One Flag Each
        return serializedState["Value"]?.Value<short>() is short value and >= 1 and <= 3
            ? (VegetationState)(1 << (value - 1))
            : VegetationState.None;
    }
}
