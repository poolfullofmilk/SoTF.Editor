using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class StructuresPageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly List<ScrewStructure> _structureTypes;

    public StructuresPageViewModel(GameData gameData)
    {
        _structureTypes =
        [
            .. gameData
                .ScrewStructures.OrderBy(screwStructure => screwStructure.CategoryName)
                .ThenBy(screwStructure => screwStructure.Name),
        ];
    }

    public ScrewStructure? BatchSelectedStructureType { get; set; }

    public List<ScrewStructureWrapper> Structures { get; } = [];

    public IReadOnlyList<ScrewStructure> StructureTypes => _structureTypes;

    public static void ChangeType(
        ScrewStructureWrapper screwStructureWrapper,
        ScrewStructure? selectedScrewStructure
    )
    {
        screwStructureWrapper.Update(selectedScrewStructure);
    }

    public void Load(Savegame? savegame)
    {
        Structures.Clear();
        if (savegame is { } selectedSavegame)
        {
            Structures.AddRange(
                LoadStructures(selectedSavegame)
                    .OrderBy(wrapper => wrapper.Category)
                    .ThenBy(wrapper => wrapper.Name)
            );
        }
    }

    private List<ScrewStructureWrapper> LoadStructures(Savegame selectedSavegame)
    {
        var wrappers = new List<ScrewStructureWrapper>();
        wrappers.AddRange(LoadUnfinishedStructures(selectedSavegame));
        wrappers.AddRange(LoadFinishedStructures(selectedSavegame));
        return wrappers;
    }

    private List<ScrewStructureWrapper> LoadFinishedStructures(Savegame selectedSavegame)
    {
        var wrappers = new List<ScrewStructureWrapper>();
        if (
            selectedSavegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureInstancesSaveData
            )
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                is not { } screwStructureInstances
            || screwStructureInstances["_structures"] is not JArray structures
        )
        {
            return wrappers;
        }

        var screwStructuresById = _structureTypes.ToDictionary(screwStructure => screwStructure.Id);

        for (var i = 0; i < structures.Count; i++)
        {
            var structure = structures[i];
            if (structure.Type == JTokenType.Null)
            {
                continue;
            }

            ScrewStructure? screwStructure = null;

            var screwStructureId = structure["Id"]?.Value<int>();
            if (screwStructureId is { } id)
            {
                screwStructure = screwStructuresById.GetValueOrDefault(id);
            }

            if (screwStructure == null && screwStructureId != null)
            {
                s_logger.Debug($"Found Unknown Finished Structure {screwStructureId}");
            }

            var position = structure["Pos"]?.ToObject<Position>() ?? null;
            wrappers.Add(
                new ScrewStructureWrapper(
                    screwStructure,
                    structure,
                    screwStructure?.BuildCost ?? 0,
                    position,
                    ScrewStructureOrigin.Finished,
                    i
                )
            );
        }

        return wrappers;
    }

    private List<ScrewStructureWrapper> LoadUnfinishedStructures(Savegame selectedSavegame)
    {
        var wrappers = new List<ScrewStructureWrapper>();
        if (
            selectedSavegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureNodeInstancesSaveData
            )
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureNodeInstances)
                is not { } screwStructureNodeInstances
            || screwStructureNodeInstances["_structures"] is not JArray structures
        )
        {
            return wrappers;
        }

        var screwStructuresById = _structureTypes.ToDictionary(screwStructure => screwStructure.Id);

        for (var i = 0; i < structures.Count; i++)
        {
            var structure = structures[i];
            if (structure.Type == JTokenType.Null)
            {
                continue;
            }

            ScrewStructure? screwStructure = null;
            var screwStructureId = structure["Id"]?.Value<int>();
            if (screwStructureId is { } id)
            {
                screwStructure = screwStructuresById.GetValueOrDefault(id);
            }

            var added = structure["Added"]?.Value<int>() ?? 0;
            var position = structure["Pos"]?.ToObject<Position>() ?? null;

            if (screwStructure == null && screwStructureId != null)
            {
                s_logger.Debug(
                    $"Found Unknown Unfinished Structure {screwStructureId}, Added {added}"
                );
            }

            wrappers.Add(
                new ScrewStructureWrapper(
                    screwStructure,
                    structure,
                    added,
                    position,
                    ScrewStructureOrigin.Unfinished,
                    i
                )
            );
        }

        return wrappers;
    }

    public bool Update(Savegame savegame)
    {
        // A Row Already Moved Or Removed Has Left Its Array
        var modified = Structures
            .Where(wrapper =>
                wrapper.ModificationMode is not (null or ScrewStructureModificationMode.None)
                && wrapper.Token.Parent != null
            )
            .ToList();

        if (modified.Count == 0)
        {
            return false;
        }

        if (
            savegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureNodeInstancesSaveData
            )
                is not { } unfinishedSaveDataWrapper
            || unfinishedSaveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.ScrewStructureNodeInstances)
                ?["_structures"]
                is not JArray unfinishedStructures
        )
        {
            s_logger.Info("Skipped Saving Structures, Found No Node Instances");
            return false;
        }

        if (
            savegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureInstancesSaveData
            )
                is not { } finishedSaveDataWrapper
            || finishedSaveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"]
                is not JArray finishedStructures
        )
        {
            s_logger.Info("Skipped Saving Structures, Found No Instances");
            return false;
        }

        // Edits Change The Live Arrays, So Zipline Anchors Stay
        var unfinishedChanged = false;
        var finishedChanged = false;
        foreach (var wrapper in modified)
        {
            var (unfinished, finished) =
                wrapper.Origin == ScrewStructureOrigin.Unfinished
                    ? ApplyToUnfinished(wrapper, finishedStructures)
                    : ApplyToFinished(wrapper, unfinishedStructures);
            unfinishedChanged |= unfinished;
            finishedChanged |= finished;
        }

        if (unfinishedChanged)
        {
            unfinishedSaveDataWrapper.MarkAsModified(
                Constants.JsonKeys.ScrewStructureNodeInstances
            );
        }

        if (finishedChanged)
        {
            finishedSaveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return unfinishedChanged || finishedChanged;
    }

    private static int AlmostFinishedAdded(ScrewStructureWrapper wrapper)
    {
        // Free Structures Cost Nothing, Never A Negative Count
        return Math.Max(0, wrapper.BuildCost - 1);
    }

    private static (bool Unfinished, bool Finished) ApplyToUnfinished(
        ScrewStructureWrapper wrapper,
        JArray finishedStructures
    )
    {
        var token = wrapper.Token;
        var id = wrapper.ChangedTypeId ?? token["Id"]?.Value<int>();
        switch (wrapper.ModificationMode)
        {
            case ScrewStructureModificationMode.Remove:
                token.Remove();
                return (true, false);
            case ScrewStructureModificationMode.Finish when id is { } finishedId:
                finishedStructures.Add(
                    JToken.FromObject(
                        new StorageSaveData
                        {
                            Id = finishedId,
                            Pos = wrapper.Position,
                            Rot = token["Rot"],
                        }
                    )
                );
                token.Remove();
                return (true, true);
            case ScrewStructureModificationMode.AlmostFinish
            or ScrewStructureModificationMode.Unfinish when id is { } unfinishedId:
                token["Id"] = unfinishedId;
                token["Added"] =
                    wrapper.ModificationMode == ScrewStructureModificationMode.AlmostFinish
                        ? AlmostFinishedAdded(wrapper)
                        : 0;
                return (true, false);
            default:
                return (false, false);
        }
    }

    private static (bool Unfinished, bool Finished) ApplyToFinished(
        ScrewStructureWrapper wrapper,
        JArray unfinishedStructures
    )
    {
        var token = wrapper.Token;
        var id = wrapper.ChangedTypeId ?? token["Id"]?.Value<int>();
        switch (wrapper.ModificationMode)
        {
            case ScrewStructureModificationMode.Remove:
                token.Remove();
                return (false, true);
            case ScrewStructureModificationMode.AlmostFinish
            or ScrewStructureModificationMode.Unfinish
                when id is { } unfinishedId
                    && wrapper.Position is { } position
                    && token["Rot"] is { } rotation:
                var added =
                    wrapper.ModificationMode == ScrewStructureModificationMode.AlmostFinish
                        ? AlmostFinishedAdded(wrapper)
                        : 0;
                unfinishedStructures.Add(
                    JToken.FromObject(
                        new UnfinishedGenericScrewStructure(unfinishedId, position, rotation, added)
                    )
                );
                token.Remove();
                return (true, true);
            default:
                return (false, false);
        }
    }

    public void SetModificationMode(
        IEnumerable<ScrewStructureWrapper> rows,
        ScrewStructureOrigin origin,
        ScrewStructureModificationMode mode
    )
    {
        // Only The Rows The Search Shows
        foreach (
            var wrapper in rows.Where(structure =>
                structure.Origin == origin
                && (
                    BatchSelectedStructureType == null
                    || structure.ScrewStructure?.Id == BatchSelectedStructureType.Id
                )
            )
        )
        {
            wrapper.ModificationMode = mode;
        }
    }
}
