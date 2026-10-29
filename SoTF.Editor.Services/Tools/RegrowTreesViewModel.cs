using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public class RegrowTreesViewModel
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
    private readonly Savegame _savegame;

    public RegrowTreesViewModel(Savegame savegame)
    {
        _savegame = savegame;
        _counts = (
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.WorldObjectLocatorManagerSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.WorldObjectLocatorManager)
                ?["SerializedStates"]
            ?? Enumerable.Empty<JToken>()
        )
            .GroupBy(Savegame.VegetationStateOf)
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
        var countRegrown = _savegame.RegrowTrees(VegetationStateSelected, PctRegrow);
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
}
