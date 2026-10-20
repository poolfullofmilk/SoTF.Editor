using System.Collections.Immutable;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models;

public class ScrewStructureWrapper
{
    // What Was Loaded, So Setting None Undoes A Type Change
    private readonly ScrewStructure? _loadedStructure;
    private readonly int _loadedAdded;
    private ScrewStructureModificationMode _modificationMode = ScrewStructureModificationMode.None;

    public int Added { get; set; }

    public ScrewStructure? ScrewStructure { get; set; }

    public ScrewStructureWrapper(
        ScrewStructure? screwStructure,
        JToken token,
        int added,
        Position? position,
        ScrewStructureOrigin origin,
        int index
    )
    {
        Token = token;
        Added = added;
        Position = position;
        Origin = origin;
        Index = index;
        ScrewStructure = screwStructure;
        _loadedStructure = screwStructure;
        _loadedAdded = added;
        var canFinish = screwStructure?.CanFinish ?? false;
        var canEdit = screwStructure?.CanEdit ?? false;
        ModificationModes = GetModificationModes(origin, canFinish, canEdit);
    }

    public ScrewStructureModificationMode? ModificationMode
    {
        get => _modificationMode;
        set
        {
            if (value is null or ScrewStructureModificationMode.None)
            {
                _modificationMode = ScrewStructureModificationMode.None;
                ScrewStructure = _loadedStructure;
                Added = _loadedAdded;
                ChangedTypeId = null;
                return;
            }

            if (ModificationModes.Contains(value.Value))
            {
                _modificationMode = value.Value;
            }
        }
    }

    public ImmutableSortedSet<ScrewStructureModificationMode> ModificationModes { get; }

    // Changed Once A Mode Or A New Type Is Picked
    public bool IsChanged =>
        _modificationMode != ScrewStructureModificationMode.None || ChangedTypeId != null;

    public JToken Token { get; }
    public Position? Position { get; }
    public ScrewStructureOrigin Origin { get; }
    public int Index { get; }

    public string Name => ScrewStructure?.Name ?? "???";
    public string Category => ScrewStructure?.CategoryName ?? AppText.Get("Unknown");
    public int BuildCost => ScrewStructure?.BuildCost ?? -1;

    // Free Structures Cost Nothing, Never A Negative Count
    public int AlmostFinishedAdded => Math.Max(0, BuildCost - 1);

    public int PctDone => PctOf(_loadedStructure, _loadedAdded);

    public string PctDonePrintable => Printable(PctDone);

    // What A Save Would Write, Null While It Stays The Same
    public int? NewPctDone
    {
        get
        {
            int? newPctDone = _modificationMode switch
            {
                ScrewStructureModificationMode.Finish => 100,
                ScrewStructureModificationMode.AlmostFinish => PctOf(
                    ScrewStructure,
                    AlmostFinishedAdded
                ),
                ScrewStructureModificationMode.Unfinish => 0,
                _ => null,
            };
            return newPctDone == PctDone ? null : newPctDone;
        }
    }

    public static string Printable(int pctDone)
    {
        return pctDone == -1 ? "???" : $"{pctDone}%";
    }

    private static int PctOf(ScrewStructure? screwStructure, int added)
    {
        return screwStructure?.BuildCost is { } buildCost
            ? buildCost == 0
                ? 100
                : 100 * added / buildCost
            : -1;
    }

    public int? ChangedTypeId { get; private set; }

    private static ImmutableSortedSet<ScrewStructureModificationMode> GetModificationModes(
        ScrewStructureOrigin origin,
        bool canFinish,
        bool canEdit
    )
    {
        return
        [
            .. Enum.GetValues<ScrewStructureModificationMode>()
                .Where(mode => IsModeAllowed(mode, origin, canFinish, canEdit)),
        ];
    }

    private static bool IsModeAllowed(
        ScrewStructureModificationMode mode,
        ScrewStructureOrigin origin,
        bool canFinish,
        bool canEdit
    )
    {
        if (!canEdit)
        {
            return mode == ScrewStructureModificationMode.None;
        }

        return mode switch
        {
            ScrewStructureModificationMode.None or ScrewStructureModificationMode.Remove => true,
            ScrewStructureModificationMode.AlmostFinish
            or ScrewStructureModificationMode.Unfinish => origin == ScrewStructureOrigin.Unfinished
                || canFinish,
            ScrewStructureModificationMode.Finish => origin == ScrewStructureOrigin.Unfinished
                && canFinish,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    public bool CanChangeType()
    {
        // A Type Change Writes An Almost Finished Structure
        return ModificationModes.Contains(ScrewStructureModificationMode.AlmostFinish);
    }

    public void Update(ScrewStructure? selectedScrewStructure)
    {
        if (
            selectedScrewStructure == null
            || selectedScrewStructure.Id == ScrewStructure?.Id
            || !CanChangeType()
        )
        {
            return;
        }

        ModificationMode = ScrewStructureModificationMode.AlmostFinish;
        ScrewStructure = selectedScrewStructure;
        Added = AlmostFinishedAdded;
        ChangedTypeId = selectedScrewStructure.Id;
    }
}
