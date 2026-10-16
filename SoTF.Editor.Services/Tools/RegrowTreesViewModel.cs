using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public class RegrowTreesViewModel
{
    private int _countGone;
    private int _countHalfChopped;
    private int _countStumps;

    private readonly Savegame _selectedSavegame;

    public RegrowTreesViewModel(Savegame selectedSavegame)
    {
        _selectedSavegame = selectedSavegame;
        LoadTreeCounts(selectedSavegame);
    }

    public int PctRegrow { get; set; } = 100;

    public VegetationState VegetationStateSelected { get; set; } =
        VegetationState.Gone | VegetationState.HalfChopped | VegetationState.Stumps;

    public string PrintAll =>
        $"{AppText.Get("All")} ({_countGone + _countStumps + _countHalfChopped})";

    public string PrintStumps => $"{AppText.Get("Stumps")} ({_countStumps})";

    public string PrintHalfChopped => $"{AppText.Get("HalfChopped")} ({_countHalfChopped})";

    public string PrintGone => $"{AppText.Get("Gone")} ({_countGone})";

    public bool VegetationStateIsAllSelected
    {
        get =>
            VegetationStateSelected.HasFlag(VegetationState.Gone)
            && VegetationStateSelected.HasFlag(VegetationState.HalfChopped)
            && VegetationStateSelected.HasFlag(VegetationState.Stumps);
        set
        {
            var vegetationState = !value
                ? VegetationState.None
                : VegetationState.Gone | VegetationState.HalfChopped | VegetationState.Stumps;
            VegetationStateSelected = vegetationState;
        }
    }

    private void LoadTreeCounts(Savegame selectedSavegame)
    {
        if (
            selectedSavegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.WorldObjectLocatorManagerSaveData
            )
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.WorldObjectLocatorManager)
                is not { } worldObjectLocatorManager
        )
        {
            return;
        }

        var serializedStates =
            worldObjectLocatorManager["SerializedStates"]?.ToList() ?? Enumerable.Empty<JToken>();

        foreach (var serializedState in serializedStates)
        {
            var valueToken = serializedState["Value"];
            var value = valueToken?.Value<short>();
            if (value == null)
            {
                continue;
            }

            var shiftedValue = (short)(1 << (value - 1));

            if ((shiftedValue & (int)VegetationState.Gone) != 0)
            {
                _countGone++;
            }

            if ((shiftedValue & (int)VegetationState.HalfChopped) != 0)
            {
                _countHalfChopped++;
            }

            if ((shiftedValue & (int)VegetationState.Stumps) != 0)
            {
                _countStumps++;
            }
        }
    }

    public string Save()
    {
        var countRegrown = _selectedSavegame.RegrowTrees(VegetationStateSelected, PctRegrow);

        var statesPrintable = new List<string>();
        if ((VegetationStateSelected & VegetationState.Gone) != 0)
        {
            statesPrintable.Add(AppText.Get("Gone"));
        }

        if ((VegetationStateSelected & VegetationState.HalfChopped) != 0)
        {
            statesPrintable.Add(AppText.Get("HalfChopped"));
        }

        if ((VegetationStateSelected & VegetationState.Stumps) != 0)
        {
            statesPrintable.Add(AppText.Get("Stumps"));
        }

        var resultMessage =
            countRegrown == 0
                ? AppText.Format("NoTreesRegrown", string.Join(", ", statesPrintable))
                : AppText.Format("TreesRegrown", countRegrown, string.Join(", ", statesPrintable));
        return resultMessage;
    }

    public bool CanRegrowTrees()
    {
        return VegetationStateSelected != VegetationState.None;
    }
}
